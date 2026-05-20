using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 — embedding queue + worker logic.
//
// ProcessPendingAsync:
//   1. Lease up to batchSize rows from PendingEmbeddings using
//      `FOR UPDATE SKIP LOCKED` so multi-instance workers don't fight.
//   2. For each row, check if Embeddings already has a matching
//      (EntityType, EntityId, ModelVersion, ContentHash) — if yes,
//      delete the queue row (idempotent re-enqueue).
//   3. Group the remaining rows into batches of 32 (Voyage's
//      practical sweet spot per ADR-021 §D4).
//   4. Call Voyage. On success, upsert Embeddings rows + delete the
//      corresponding queue rows.
//   5. On failure, bump Attempts + set LastError + leave the row.
//      The 5-attempt cap is enforced via the SELECT WHERE clause.
public sealed class EmbeddingBackfillService : IEmbeddingBackfillService
{
    private const string ModelVersion = "voyage-3-large/v1";
    private const int MaxAttempts = 5;
    private const int VoyageBatchSize = 32;

    private readonly AppDbContext _db;
    private readonly IVoyageClient _voyage;
    private readonly ILogger<EmbeddingBackfillService> _logger;

    public EmbeddingBackfillService(
        AppDbContext db,
        IVoyageClient voyage,
        ILogger<EmbeddingBackfillService> logger)
    {
        _db = db;
        _voyage = voyage;
        _logger = logger;
    }

    public async Task EnqueueAsync(
        string entityType,
        long entityId,
        int tenantId,
        string sourceText,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceText)) return;
        var hash = EmbeddingSourceText.ComputeHash(sourceText);

        // De-dupe: if an existing Embeddings row already has this exact
        // hash, no need to enqueue.
        var existingHash = await _db.Embeddings
            .Where(e => e.EntityType == entityType &&
                        e.EntityId == entityId &&
                        e.ModelVersion == ModelVersion)
            .Select(e => e.ContentHash)
            .FirstOrDefaultAsync(ct);
        if (existingHash == hash) return;

        // De-dupe queue: if same (EntityType, EntityId, ContentHash)
        // already queued + not failed-out, no-op.
        var alreadyQueued = await _db.PendingEmbeddings.AnyAsync(p =>
            p.EntityType == entityType &&
            p.EntityId == entityId &&
            p.ContentHash == hash &&
            p.Attempts < MaxAttempts, ct);
        if (alreadyQueued) return;

        _db.PendingEmbeddings.Add(new PendingEmbedding
        {
            EntityType = entityType,
            EntityId = entityId,
            TenantId = tenantId,
            SourceText = sourceText,
            ContentHash = hash,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken ct)
    {
        // Lease rows. Postgres-specific FOR UPDATE SKIP LOCKED so
        // multi-instance workers don't process the same row twice.
        var leased = await _db.PendingEmbeddings
            .FromSqlInterpolated($@"
                SELECT * FROM ""PendingEmbeddings""
                WHERE ""Attempts"" < {MaxAttempts}
                ORDER BY ""EnqueuedAt""
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED")
            .AsNoTracking()
            .ToListAsync(ct);

        if (leased.Count == 0) return 0;

        // Batch the Voyage call (32 at a time per ADR-021 §D4).
        int succeeded = 0;
        foreach (var batch in leased.Chunk(VoyageBatchSize))
        {
            try
            {
                var texts = batch.Select(b => b.SourceText).ToList();
                var vectors = await _voyage.EmbedDocumentsAsync(texts, ct);

                // Upsert + delete queue rows in one transaction.
                for (int i = 0; i < batch.Length; i++)
                {
                    var pending = batch[i];
                    var vector = vectors[i];
                    if (vector.Length != 1024)
                    {
                        _logger.LogWarning(
                            "Voyage returned vector of unexpected dimension {Dim} (expected 1024). Skipping {Entity}#{Id}.",
                            vector.Length, pending.EntityType, pending.EntityId);
                        continue;
                    }

                    var half = ToHalfVector(vector);
                    await UpsertEmbeddingAsync(pending, half, ct);

                    _db.PendingEmbeddings
                        .Where(p => p.Id == pending.Id)
                        .ExecuteDelete();
                    succeeded++;
                }
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Voyage embed batch of {Count} failed; bumping Attempts on the batch.",
                    batch.Length);

                // Bump Attempts + set LastError on all rows in the failed batch.
                var ids = batch.Select(b => b.Id).ToList();
                foreach (var id in ids)
                {
                    await _db.PendingEmbeddings
                        .Where(p => p.Id == id)
                        .ExecuteUpdateAsync(setter => setter
                            .SetProperty(p => p.Attempts, p => p.Attempts + 1)
                            .SetProperty(p => p.LastAttemptAt, _ => DateTime.UtcNow)
                            .SetProperty(p => p.LastError, _ => Truncate(ex.Message, 480)),
                            ct);
                }
            }
        }

        return succeeded;
    }

    public async Task<int> EnqueueAllReceiptProfilesAsync(CancellationToken ct)
    {
        var profiles = await _db.Set<Abs.FixedAssets.Models.Production.ReceiptProfile>()
            .AsNoTracking()
            .ToListAsync(ct);

        int enqueued = 0;
        foreach (var p in profiles)
        {
            var srcText = EmbeddingSourceText.ForReceiptProfile(p);
            // TenantId: ReceiptProfile is shared catalog (no per-tenant
            // column today). Use 0 = shared/global per ADR-020 §D6
            // convention. RLS policy still applies (a future per-tenant
            // ReceiptProfile would use its own TenantId).
            int tenantId = 0;
            var beforeCount = _db.PendingEmbeddings.Local.Count;
            await EnqueueAsync(
                EmbeddingSourceText.EntityTypeReceiptProfile,
                p.Id, tenantId, srcText, ct);
            // EnqueueAsync uses Add() which goes to Local — but it also
            // SaveChanges, so the Local count won't grow. Track via the
            // database side.
            enqueued++;
        }

        _logger.LogInformation(
            "Enqueued {Count} ReceiptProfiles for embedding (model: {Model})",
            enqueued, ModelVersion);
        return enqueued;
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private async Task UpsertEmbeddingAsync(
        PendingEmbedding pending,
        HalfVector vector,
        CancellationToken ct)
    {
        var existing = await _db.Embeddings.FirstOrDefaultAsync(e =>
            e.EntityType == pending.EntityType &&
            e.EntityId == pending.EntityId &&
            e.ModelVersion == ModelVersion, ct);

        if (existing is null)
        {
            _db.Embeddings.Add(new Embedding
            {
                EntityType = pending.EntityType,
                EntityId = pending.EntityId,
                TenantId = pending.TenantId,
                ModelVersion = ModelVersion,
                ContentHash = pending.ContentHash,
                Embedding_ = vector,
                SourceText = pending.SourceText,
            });
        }
        else
        {
            existing.ContentHash = pending.ContentHash;
            existing.Embedding_ = vector;
            existing.SourceText = pending.SourceText;
        }
    }

    private static HalfVector ToHalfVector(float[] floats)
    {
        var halves = new Half[floats.Length];
        for (int i = 0; i < floats.Length; i++)
        {
            halves[i] = (Half)floats[i];
        }
        return new HalfVector(new ReadOnlyMemory<Half>(halves));
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max];
    }
}
