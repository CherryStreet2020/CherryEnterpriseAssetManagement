using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Voice;

// CODE-REVIEW FIXES (applied 2026-05-21 before first ship):
//   - #5  Cancellation propagation: catch OperationCanceledException
//         BEFORE the generic Exception catch so client cancellations
//         propagate (instead of silently becoming Fallback + a stale 200).
//   - #6  Voyage backpressure: cap concurrent EmbedQueryAsync calls via
//         a process-wide SemaphoreSlim (default 16). At 2K req/min paid
//         tier, this gives plenty of headroom while preventing
//         thread-pool starvation under burst load.
//   - Telemetry: routed.Reason (which carries the cosine distance) is
//         now included in the AuditLog toolName so the threshold can be
//         tuned from real usage data.

// Sprint 12C / ADR-021 — Hybrid Intent Router implementation (PR #3).
//
// FLOW
// ====
// 1. Try keyword fast-path via IntentClassifier.Classify.
//    - On Kind != Unknown → return immediately with Source=Keyword,
//      Confidence=1.0. (~99% of utterances we've seen so far in dev.)
// 2. If Unknown: embed the utterance via IVoyageClient.EmbedQueryAsync
//    and search the Embeddings table for the closest intent prototype.
//    - Pgvector cosine distance via the .CosineDistance LINQ extension
//      (Pgvector.EntityFrameworkCore 0.2.1).
//    - Take top-K (=5), keep matches with cosine_distance <= 0.45
//      (similarity >= 0.55).
//    - Majority-vote across the surviving rows to pick the IntentKind.
//    - Re-run IntentClassifier.ExtractNaturalKey on the raw utterance
//      to fill ParsedIntent.NaturalKey.
//    - Confidence = 1 - (cosine distance of best match).
//    - Source=Vector.
// 3. If vector also fails (no rows or all below threshold) → return
//    Unknown with Source=Fallback, Confidence=0.
//
// FAILURE MODES
// =============
// - Voyage timeout / 429 / outage: catch + log + return Fallback. We
//   never let the vector layer break the voice round-trip — keyword
//   already works for the common phrasings.
// - Pgvector cast errors (test contexts on Sqlite/InMemory): caught
//   by HasVectorStore check. Tests cover this via the existing
//   `if (Database.IsNpgsql())` provider guard pattern from AppDbContext.

public sealed class HybridIntentRouter : IHybridIntentRouter
{
    /// <summary>
    /// Cosine-distance threshold for the vector fallback. Distances at or
    /// below this count as a confident match; distances above land in
    /// Fallback. 0.45 ≈ similarity 0.55. Tuned to be permissive enough
    /// that good paraphrases hit, but tight enough to keep nonsense out.
    /// </summary>
    public const double VectorThreshold = 0.45;

    /// <summary>How many prototypes to retrieve before majority-voting.</summary>
    private const int TopK = 5;

    /// <summary>The Voyage model version that intent prototypes are embedded against.</summary>
    public const string ModelVersion = "voyage-3-large/v1";

    /// <summary>
    /// Process-wide cap on concurrent Voyage query calls. Above this, additional
    /// callers wait briefly for a slot before falling through to Fallback.
    /// 16 keeps thread-pool sane while leaving ~2K-rpm headroom at the paid tier.
    /// </summary>
    private const int VoyageQueryConcurrency = 16;

    /// <summary>How long to wait for a concurrency slot before returning Fallback.</summary>
    private static readonly TimeSpan VoyageQueryWaitTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly SemaphoreSlim VoyageQueryGate = new SemaphoreSlim(
        VoyageQueryConcurrency, VoyageQueryConcurrency);

    private readonly AppDbContext _db;
    private readonly IVoyageClient _voyage;
    private readonly ILogger<HybridIntentRouter> _logger;

    public HybridIntentRouter(
        AppDbContext db,
        IVoyageClient voyage,
        ILogger<HybridIntentRouter> logger)
    {
        _db = db;
        _voyage = voyage;
        _logger = logger;
    }

    public async Task<RoutedIntent> RouteAsync(string utterance, int tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return new RoutedIntent(
                new ParsedIntent(IntentKind.Unknown, null),
                RoutingSource.Fallback,
                Confidence: 0,
                Reason: "empty utterance");
        }

        // ---------- Layer 1: keyword fast-path ----------
        var kw = IntentClassifier.Classify(utterance);
        if (kw.Kind != IntentKind.Unknown)
        {
            return new RoutedIntent(
                kw,
                RoutingSource.Keyword,
                Confidence: 1.0,
                Reason: "keyword match");
        }

        // ---------- Layer 2: vector cosine fallback ----------
        // Only meaningful on Postgres — non-Postgres contexts (test
        // doubles using Sqlite/InMemory) ignore the Embeddings DbSet
        // entirely (see AppDbContext.OnModelCreating provider guard).
        if (!_db.Database.IsNpgsql())
        {
            return new RoutedIntent(
                new ParsedIntent(IntentKind.Unknown, IntentClassifier.ExtractNaturalKey(utterance)),
                RoutingSource.Fallback,
                Confidence: 0,
                Reason: "vector layer disabled on non-Postgres provider");
        }

        // Backpressure: cap concurrent Voyage calls. If the gate is saturated
        // and we can't get a slot within VoyageQueryWaitTimeout, drop to Fallback
        // instead of stalling the voice round-trip. Better to give the user a
        // "I didn't catch that, can you rephrase?" than to keep them hanging.
        var gotSlot = await VoyageQueryGate.WaitAsync(VoyageQueryWaitTimeout, ct);
        if (!gotSlot)
        {
            _logger.LogWarning("Voyage concurrency gate saturated; falling back to Unknown");
            return new RoutedIntent(
                new ParsedIntent(IntentKind.Unknown, IntentClassifier.ExtractNaturalKey(utterance)),
                RoutingSource.Fallback,
                Confidence: 0,
                Reason: "voyage gate saturated");
        }

        float[] queryFloats;
        try
        {
            queryFloats = await _voyage.EmbedQueryAsync(utterance, ct);
        }
        catch (OperationCanceledException)
        {
            // Client canceled the HTTP request. Propagate the cancellation
            // instead of silently turning it into a Fallback + bogus 200.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voyage EmbedQueryAsync failed; falling back to Unknown");
            return new RoutedIntent(
                new ParsedIntent(IntentKind.Unknown, IntentClassifier.ExtractNaturalKey(utterance)),
                RoutingSource.Fallback,
                Confidence: 0,
                Reason: "voyage call failed");
        }
        finally
        {
            VoyageQueryGate.Release();
        }

        if (queryFloats is null || queryFloats.Length != 1024)
        {
            _logger.LogWarning("Voyage returned unexpected dimensionality ({Dim})", queryFloats?.Length ?? 0);
            return new RoutedIntent(
                new ParsedIntent(IntentKind.Unknown, IntentClassifier.ExtractNaturalKey(utterance)),
                RoutingSource.Fallback,
                Confidence: 0,
                Reason: "voyage dimensionality mismatch");
        }

        var halves = new Half[queryFloats.Length];
        for (int i = 0; i < queryFloats.Length; i++) halves[i] = (Half)queryFloats[i];
        var queryVec = new HalfVector(halves);

        // Pgvector LINQ extension. Pgvector.EntityFrameworkCore 0.2.1 emits
        // `"Embedding_" <=> @p` against the halfvec column. Tenant-scoping:
        // accept either the caller's tenant rows OR system-seeded (TenantId=0).
        List<IntentRow> matches;
        try
        {
            matches = await _db.Embeddings
                .Where(e => e.EntityType == IntentPrototypes.EntityType
                            && e.ModelVersion == ModelVersion
                            && (e.TenantId == tenantId || e.TenantId == IntentPrototypes.SystemTenantId))
                .OrderBy(e => e.Embedding_.CosineDistance(queryVec))
                .Take(TopK)
                .Select(e => new IntentRow
                {
                    EntityId = e.EntityId,
                    Distance = e.Embedding_.CosineDistance(queryVec),
                })
                .ToListAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Client canceled the HTTP request mid-DB-roundtrip. Propagate.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pgvector cosine query failed; falling back to Unknown");
            return new RoutedIntent(
                new ParsedIntent(IntentKind.Unknown, IntentClassifier.ExtractNaturalKey(utterance)),
                RoutingSource.Fallback,
                Confidence: 0,
                Reason: "vector query failed");
        }

        var survivors = matches.Where(m => m.Distance <= VectorThreshold).ToList();
        if (survivors.Count == 0)
        {
            // No prototype came in close enough.
            return new RoutedIntent(
                new ParsedIntent(IntentKind.Unknown, IntentClassifier.ExtractNaturalKey(utterance)),
                RoutingSource.Fallback,
                Confidence: matches.Count == 0 ? 0 : Math.Max(0, 1 - matches[0].Distance),
                Reason: matches.Count == 0 ? "no intent prototypes loaded" : "all matches below threshold");
        }

        // Majority vote over the survivors. Tie-break: the survivor closest to
        // zero distance wins. (Survivors are already ordered by distance asc.)
        var votes = survivors
            .GroupBy(s => (IntentKind)s.EntityId)
            .Select(g => new
            {
                Kind = g.Key,
                Count = g.Count(),
                BestDistance = g.Min(x => x.Distance),
            })
            .OrderByDescending(v => v.Count)
            .ThenBy(v => v.BestDistance)
            .ToList();

        var winner = votes[0];
        if (!Enum.IsDefined(typeof(IntentKind), winner.Kind) || winner.Kind == IntentKind.Unknown)
        {
            // Defensive: a stray Unknown-tagged prototype slipped into the table.
            return new RoutedIntent(
                new ParsedIntent(IntentKind.Unknown, IntentClassifier.ExtractNaturalKey(utterance)),
                RoutingSource.Fallback,
                Confidence: 0,
                Reason: "vector winner was Unknown");
        }

        // Per-intent natural-key extraction. Intents that resolve a domain-specific
        // ref (item / production order / project) need their OWN extractor even when
        // VECTOR routing wins — ExtractNaturalKey doesn't match part numbers, PRO-
        // numbers, or project codes like DEMO-COO-PROJ-001, so a vector-only phrasing
        // would otherwise lose its ref and the handler would ask "which …?".
        var key = winner.Kind switch
        {
            IntentKind.ExplainMakeBuyDecision   => IntentClassifier.ExtractMakeBuyItemRef(utterance),
            IntentKind.CrystallizeJobToStandard => IntentClassifier.ExtractProductionOrderRef(utterance),
            IntentKind.ProjectPromiseStatus     => IntentClassifier.ExtractProjectRef(utterance),
            IntentKind.ShowProjectGraph         => IntentClassifier.ExtractProjectRef(utterance),
            _                                   => IntentClassifier.ExtractNaturalKey(utterance),
        };
        var parsed = new ParsedIntent(winner.Kind, key);

        return new RoutedIntent(
            parsed,
            RoutingSource.Vector,
            Confidence: Math.Max(0, 1 - winner.BestDistance),
            Reason: $"vector top-{survivors.Count}/{matches.Count} dist={winner.BestDistance:0.000}");
    }

    /// <summary>Local DTO shape for the projection. Internal to this service.</summary>
    private sealed class IntentRow
    {
        public long EntityId { get; set; }
        public double Distance { get; set; }
    }
}
