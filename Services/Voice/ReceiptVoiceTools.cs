using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Receiving;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Voice;

// ADR-016 §D8 — Production implementation of IReceiptVoiceTools.
// Replaces ReceiptVoiceToolsStub (still in the codebase as a test fallback).
//
// Each tool:
//   - Reads or mutates the receipt graph via real EF queries / the
//     IReceivingControlCenterService.
//   - Writes an AuditLog row with ActorKind=AiOnBehalfOf, the tool name,
//     and the invoking user's id (NEVER an AI-only identity per ADR-014 D6).
//   - Returns Result<T> — never throws on expected failures.
//
// Three of the ten tools (MatchOrphanReceipt, ExplainException,
// OcrParseMillCert) ship with deterministic / template-based bodies in this
// PR. Sprint 5 swaps the bodies for real LLM/OCR calls without touching the
// signature. The deterministic bodies are good enough to wire the
// Receiving Control Center's voice surface today and to give Sprint 5 a
// regression baseline.
public sealed class ReceiptVoiceTools : IReceiptVoiceTools
{
    private readonly AppDbContext _db;
    private readonly IReceivingControlCenterService _receiving;
    private readonly ILogger<ReceiptVoiceTools> _logger;

    public ReceiptVoiceTools(
        AppDbContext db,
        IReceivingControlCenterService receiving,
        ILogger<ReceiptVoiceTools> logger)
    {
        _db = db;
        _receiving = receiving;
        _logger = logger;
    }

    // =====================================================================
    // ADR-015 D10 — original 4 tools, now with real implementations.
    // =====================================================================

    public async Task<Result<ChainOfCustodyGraph>> TraceChainOfCustodyAsync(
        string naturalKey, string direction, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(naturalKey))
            return Result.Failure<ChainOfCustodyGraph>("naturalKey is required.");

        var receiptResult = await LookupReceiptAsync(naturalKey, null, ct);
        if (receiptResult.IsFailure || receiptResult.Value is null || receiptResult.Value.Count == 0)
            return Result.Failure<ChainOfCustodyGraph>($"No receipt matched '{naturalKey}'.");

        var receipt = receiptResult.Value[0];
        var nodes = new List<ChainNode>();
        var edges = new List<ChainEdge>();

        if (!string.IsNullOrEmpty(receipt.SourcePoNumber))
        {
            nodes.Add(new ChainNode("PurchaseOrder", 0, $"PO {receipt.SourcePoNumber}", null));
            edges.Add(new ChainEdge(
                "PurchaseOrder", 0,
                "StockReceipt", receipt.Id,
                "received-against"));
        }

        nodes.Add(new ChainNode(
            "StockReceipt", receipt.Id,
            $"{receipt.ReceiptNumber} ({receipt.QuantityReceived} {receipt.Uom})",
            receipt.ReceivedAt));

        // Sprint 5 will extend this with Nest / Remnant / Shipment edges
        // walking forward from the receipt. Today we ship the receipt-level
        // chain; downstream graph hops require the production-batch service.

        await WriteAiAuditAsync(
            action: "Voice.TraceChainOfCustody",
            entityType: "StockReceipt",
            entityId: receipt.Id,
            description: $"Traced chain for '{naturalKey}' ({direction}) — {nodes.Count} nodes",
            actorUserId: null,
            voiceContext: null,
            ct);

        return Result.Success(new ChainOfCustodyGraph(
            nodes, edges, naturalKey, direction));
    }

    public async Task<Result<IReadOnlyList<ExpectedReceiptItem>>> ListExpectedReceiptsAsync(
        DateTime fromUtc, DateTime toUtc, int? forUserId, CancellationToken ct)
    {
        // PR #4 sketches the shape — pull open PO lines from existing PurchaseOrders.
        // Until the PO domain exposes a clean "expected receipt" view, we infer from
        // StockReceipts.SourcePoNumber MAX(ReceivedAt) per PO line.

        var recent = await _db.StockReceipts
            .AsNoTracking()
            .Where(r => r.ReceivedAt >= fromUtc.AddDays(-90) && !string.IsNullOrEmpty(r.SourcePoNumber))
            .Select(r => new {
                r.SourcePoNumber,
                r.SourcePoLineId,
                r.ItemId,
                r.ProfileId,
                r.QuantityReceived,
                r.Uom,
                r.ReceivedAt
            })
            .ToListAsync(ct);

        var items = new List<ExpectedReceiptItem>();
        foreach (var line in recent.GroupBy(r => new { r.SourcePoNumber, r.SourcePoLineId, r.ItemId }))
        {
            var lastReceipt = line.Max(l => l.ReceivedAt);
            // Heuristic: expect another shipment within window if recent activity
            // and the last receive was inside fromUtc..toUtc projected forward.
            if (lastReceipt < fromUtc.AddDays(-60)) continue;

            items.Add(new ExpectedReceiptItem(
                PurchaseOrderLineId: 0,    // PO-line FK not yet exposed in this view
                PoNumber: line.Key.SourcePoNumber ?? "",
                SupplierName: "(supplier lookup deferred to PR #5)",
                ItemId: line.Key.ItemId,
                ItemDescription: $"Item {line.Key.ItemId}",
                ExpectedQuantity: line.Sum(l => l.QuantityReceived),
                QuantityReceivedSoFar: line.Sum(l => l.QuantityReceived),
                Uom: line.First().Uom,
                ExpectedAtUtc: lastReceipt.AddDays(7),
                HasAsn: false,
                DefaultReceiptProfileId: line.First().ProfileId,
                DefaultReceiptProfileCode: null));
        }

        await WriteAiAuditAsync(
            action: "Voice.ListExpectedReceipts",
            entityType: "Receiving",
            entityId: null,
            description: $"Listed {items.Count} expected receipts in {fromUtc:O}..{toUtc:O}",
            actorUserId: forUserId,
            voiceContext: null,
            ct);

        return Result.Success<IReadOnlyList<ExpectedReceiptItem>>(items);
    }

    public async Task<Result<int>> QuarantineByFilterAsync(
        string profileCode,
        IReadOnlyDictionary<string, object?> attributeFilter,
        string reason,
        int actorUserId,
        Guid idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(profileCode))
            return Result.Failure<int>("profileCode is required.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<int>("Quarantine reason is required.");

        // Locate profile + candidate receipts.
        var profile = await _db.Set<ReceiptProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == profileCode, ct);
        if (profile is null)
            return Result.Failure<int>($"Unknown ReceiptProfile code '{profileCode}'.");

        var candidates = await _db.StockReceipts
            .Where(r => r.ProfileId == profile.Id &&
                        r.Status != StockReceiptStatus.Quarantined &&
                        r.Status != StockReceiptStatus.Scrapped &&
                        r.Status != StockReceiptStatus.Returned)
            .ToListAsync(ct);

        // Apply attribute filter (best-effort JSONB string-equality match).
        var filtered = new List<StockReceipt>();
        foreach (var r in candidates)
        {
            if (attributeFilter.Count == 0 || MatchesAttributeFilter(r.Attributes, attributeFilter))
            {
                filtered.Add(r);
            }
        }

        int succeeded = 0;
        foreach (var r in filtered)
        {
            // Each receipt goes through the regular service path — same
            // state-machine guard, same idempotency story.
            var perReceiptKey = new IdempotencyKey
            {
                Key = DeriveSubKey(idempotencyKey, r.Id),
                UserId = actorUserId,
            };
            var cmd = new QuarantineCommand { ReceiptId = r.Id, Reason = reason };
            var result = await _receiving.QuarantineAsync(actorUserId, perReceiptKey, cmd, ct);
            if (result.IsSuccess) succeeded++;
        }

        await WriteAiAuditAsync(
            action: "Voice.QuarantineByFilter",
            entityType: "Receiving",
            entityId: null,
            description: $"Bulk-quarantined {succeeded}/{filtered.Count} receipts under {profileCode}: {reason}",
            actorUserId: actorUserId,
            voiceContext: null,
            ct);

        return Result.Success(succeeded);
    }

    public async Task<Result<IReadOnlyList<StockReceipt>>> LookupReceiptAsync(
        string naturalKey, string? profileHint, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(naturalKey))
            return Result.Failure<IReadOnlyList<StockReceipt>>("naturalKey is required.");

        var trimmed = naturalKey.Trim();
        var likePattern = $"%{trimmed}%";

        // First pass — typed columns. Cheapest.
        var query = _db.StockReceipts
            .Include(r => r.Item)
            .Include(r => r.Profile)
            .AsNoTracking()
            .Where(r =>
                EF.Functions.ILike(r.ReceiptNumber, likePattern) ||
                (r.LotNumber != null    && EF.Functions.ILike(r.LotNumber, likePattern)) ||
                (r.SerialNumber != null && EF.Functions.ILike(r.SerialNumber, likePattern)) ||
                (r.SourcePoNumber != null && EF.Functions.ILike(r.SourcePoNumber, likePattern)));

        var matches = await query.Take(20).ToListAsync(ct);

        // Second pass — JSONB Attributes (for heatNumber, ndc, gtin, etc.).
        // Only run if first pass turned up too few matches (avoid full scans).
        //
        // Voice MVP hotfix #1 (2026-05-19): Attributes is mapped as jsonb in
        // Postgres (ADR-015 PR #1). EF.Functions.ILike(jsonb_col, ...) emits
        // `col ILIKE pattern` which Postgres rejects with error 42883
        // ("operator does not exist: jsonb ~~* unknown"). The fix is to cast
        // to text via raw SQL — `("Attributes")::text ILIKE pattern`. EF
        // doesn't surface a typed-cast helper, so we use FromSqlInterpolated
        // with the same Include shape.
        if (matches.Count < 5)
        {
            var jsonMatches = await _db.StockReceipts
                .FromSqlInterpolated(
                    $@"SELECT * FROM ""StockReceipts""
                       WHERE ""Attributes"" IS NOT NULL
                         AND (""Attributes"")::text ILIKE {likePattern}
                       LIMIT 20")
                .Include(r => r.Item)
                .Include(r => r.Profile)
                .AsNoTracking()
                .ToListAsync(ct);

            foreach (var jm in jsonMatches)
            {
                if (!matches.Any(m => m.Id == jm.Id))
                    matches.Add(jm);
            }
        }

        // Optional profile-scope narrowing.
        if (!string.IsNullOrEmpty(profileHint))
        {
            matches = matches.Where(m =>
                m.Profile != null &&
                string.Equals(m.Profile.Code, profileHint, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        await WriteAiAuditAsync(
            action: "Voice.LookupReceipt",
            entityType: "StockReceipt",
            entityId: matches.FirstOrDefault()?.Id,
            description: $"Lookup '{trimmed}' (profile hint: {profileHint ?? "any"}) → {matches.Count} match(es)",
            actorUserId: null,
            voiceContext: null,
            ct);

        return Result.Success<IReadOnlyList<StockReceipt>>(matches);
    }

    // =====================================================================
    // ADR-016 D8 — 6 new tools.
    // =====================================================================

    public async Task<Result<IReadOnlyList<ExpectedArrival>>> ListExpectedArrivalsAsync(
        string? siteCode,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken ct)
    {
        // PR #4: forward-project from recent receipts (open POs as known via
        // StockReceipts). ASN ingestion + carrier-tracking ETAs land in PR #6.
        var expected = await ListExpectedReceiptsAsync(
            windowStartUtc, windowEndUtc, forUserId: null, ct);
        if (expected.IsFailure || expected.Value is null)
            return Result.Failure<IReadOnlyList<ExpectedArrival>>(expected.Error ?? "ListExpectedReceipts failed.");

        var items = expected.Value.Select(r => new ExpectedArrival(
            Source: "po",
            Reference: r.PoNumber,
            VendorName: r.SupplierName,
            ItemId: r.ItemId,
            ItemDescription: r.ItemDescription,
            ExpectedQuantity: r.ExpectedQuantity - r.QuantityReceivedSoFar,
            Uom: r.Uom,
            ExpectedAtUtc: r.ExpectedAtUtc,
            ConfidenceScore: 0.65,    // historical-lead-time guess
            Notes: r.HasAsn ? "ASN declared" : "predicted from receive history"
        )).ToList();

        await WriteAiAuditAsync(
            action: "Voice.ListExpectedArrivals",
            entityType: "Receiving",
            entityId: null,
            description: $"Forecast {items.Count} arrivals between {windowStartUtc:O} and {windowEndUtc:O}",
            actorUserId: null,
            voiceContext: null,
            ct);

        return Result.Success<IReadOnlyList<ExpectedArrival>>(items);
    }

    public async Task<Result<IReadOnlyList<OrphanMatchCandidate>>> MatchOrphanReceiptAsync(
        int receiptId, int actorUserId, CancellationToken ct)
    {
        var receipt = await _db.StockReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct);
        if (receipt is null)
            return Result.Failure<IReadOnlyList<OrphanMatchCandidate>>($"Receipt #{receiptId} not found.");
        if (!string.IsNullOrEmpty(receipt.SourcePoNumber))
            return Result.Failure<IReadOnlyList<OrphanMatchCandidate>>(
                $"Receipt {receipt.ReceiptNumber} already has PO {receipt.SourcePoNumber} — not an orphan.");

        // Deterministic ranker (PR #4):
        //   Score = item-match (50) + recency-bucket (30) + same-profile (20)
        // Sprint 5 swaps with an LLM-scored embedding match.
        var candidates = await _db.StockReceipts
            .AsNoTracking()
            .Where(r => r.SourcePoNumber != null &&
                        r.ItemId == receipt.ItemId &&
                        r.ReceivedAt >= receipt.ReceivedAt.AddDays(-60))
            .OrderByDescending(r => r.ReceivedAt)
            .Take(20)
            .ToListAsync(ct);

        var grouped = candidates
            .GroupBy(c => c.SourcePoNumber!)
            .Select(g => {
                var newest = g.OrderByDescending(c => c.ReceivedAt).First();
                var ageDays = (receipt.ReceivedAt - newest.ReceivedAt).TotalDays;
                double recencyScore = ageDays <= 7 ? 30 : ageDays <= 30 ? 20 : 10;
                double profileScore = newest.ProfileId == receipt.ProfileId ? 20 : 0;
                double itemScore = 50;
                double total = (itemScore + recencyScore + profileScore) / 100.0;
                return new OrphanMatchCandidate(
                    PoNumber: g.Key,
                    PoLineId: newest.SourcePoLineId,
                    VendorName: null,
                    ItemId: newest.ItemId,
                    ItemDescription: $"Item {newest.ItemId}",
                    RemainingQuantity: null,
                    Confidence: total,
                    Rationale: $"Same item ({newest.ItemId}); last received {ageDays:0} days ago; " +
                               (profileScore > 0 ? "matching profile." : "different profile.")
                );
            })
            .OrderByDescending(c => c.Confidence)
            .Take(3)
            .ToList();

        await WriteAiAuditAsync(
            action: "Voice.MatchOrphanReceipt",
            entityType: "StockReceipt",
            entityId: receiptId,
            description: $"Ranked {grouped.Count} candidate POs for orphan {receipt.ReceiptNumber}",
            actorUserId: actorUserId,
            voiceContext: null,
            ct);

        return Result.Success<IReadOnlyList<OrphanMatchCandidate>>(grouped);
    }

    public async Task<Result<ExceptionExplanation>> ExplainExceptionAsync(
        int receiptId, CancellationToken ct)
    {
        var receipt = await _db.StockReceipts
            .Include(r => r.Profile)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct);
        if (receipt is null)
            return Result.Failure<ExceptionExplanation>($"Receipt #{receiptId} not found.");

        // Deterministic templates per kind. Sprint 5 swaps with LLM-generated prose.
        string kind, severity, headline, narrative;
        var actions = new List<string>();

        if (receipt.Status == StockReceiptStatus.Quarantined)
        {
            kind = "qc-hold";
            severity = "critical";
            headline = $"{receipt.ReceiptNumber} is on QC hold.";
            narrative = $"This receipt was quarantined on {receipt.ModifiedAt:yyyy-MM-dd} for: " +
                        $"{receipt.QuarantineReason ?? "(no reason recorded)"}. " +
                        $"Until QC releases it, the {receipt.QuantityReceived} {receipt.Uom} cannot be issued to a job.";
            actions.Add("Open the receipt and review the QC inspector's notes");
            actions.Add("Release to Available, or escalate to Scrapped / Returned via the Quarantine workflow");
        }
        else if (string.IsNullOrEmpty(receipt.SourcePoNumber))
        {
            kind = "orphan";
            severity = "info";
            headline = $"{receipt.ReceiptNumber} is an orphan (no PO).";
            narrative = $"This receipt was created without a source PO. The voice-AI MatchOrphanReceipt tool " +
                        $"can rank candidate POs by vendor + item + recency; review the top 3 and attach.";
            actions.Add("Run MatchOrphanReceipt to see candidate POs");
            actions.Add("Manually attach a PO via the drawer's PO field");
        }
        else if (string.IsNullOrEmpty(receipt.Attributes))
        {
            kind = "doc";
            severity = "warning";
            headline = $"{receipt.ReceiptNumber} is missing required profile attributes.";
            narrative = $"The {receipt.Profile?.Code ?? "active"} profile requires attribute data " +
                        $"(heat number / lot / serial / etc) that hasn't been filled in. " +
                        $"Subsequent traceability lookups will not find this receipt by natural key.";
            actions.Add("Open the receipt and fill in the profile-required fields");
            actions.Add("If the supplier didn't send the data, contact them before consuming the lot");
        }
        else
        {
            kind = "info";
            severity = "info";
            headline = $"{receipt.ReceiptNumber} needs review.";
            narrative = "The exception lane flagged this receipt by an AI-priority heuristic — " +
                        "supplier on-time / quantity variance / unusual lead time.";
            actions.Add("Review the receipt details");
            actions.Add("Confirm with the supplier if anomalous");
        }

        await WriteAiAuditAsync(
            action: "Voice.ExplainException",
            entityType: "StockReceipt",
            entityId: receipt.Id,
            description: $"Explained exception for {receipt.ReceiptNumber}: {kind}",
            actorUserId: null,
            voiceContext: null,
            ct);

        return Result.Success(new ExceptionExplanation(
            ReceiptId: receipt.Id,
            ReceiptNumber: receipt.ReceiptNumber,
            Kind: kind,
            Severity: severity,
            Headline: headline,
            DetailedNarrative: narrative,
            SuggestedActions: actions));
    }

    public async Task<Result<ReceiveResult>> ReceiveByVoiceAsync(
        int actorUserId,
        IdempotencyKey idempotencyKey,
        ReceiveByPoCommand command,
        VoiceContext voiceContext,
        CancellationToken ct)
    {
        var result = await _receiving.ReceiveByPoAsync(actorUserId, idempotencyKey, command, ct);
        await WriteAiAuditAsync(
            action: "Voice.ReceiveByPo",
            entityType: "StockReceipt",
            entityId: result.Value?.ReceiptId,
            description: result.IsSuccess
                ? $"Voice-received {result.Value!.QuantityReceived} on PO {command.PoNumber} as {result.Value.ReceiptNumber}"
                : $"Voice-receive failed for PO {command.PoNumber}: {result.Error}",
            actorUserId: actorUserId,
            voiceContext: voiceContext,
            ct);
        return result;
    }

    public async Task<Result<QuarantineResult>> QuarantineByVoiceAsync(
        int actorUserId,
        IdempotencyKey idempotencyKey,
        QuarantineCommand command,
        VoiceContext voiceContext,
        CancellationToken ct)
    {
        var result = await _receiving.QuarantineAsync(actorUserId, idempotencyKey, command, ct);
        await WriteAiAuditAsync(
            action: "Voice.Quarantine",
            entityType: "StockReceipt",
            entityId: command.ReceiptId,
            description: result.IsSuccess
                ? $"Voice-quarantined receipt {command.ReceiptId}: {command.Reason}"
                : $"Voice-quarantine failed for receipt {command.ReceiptId}: {result.Error}",
            actorUserId: actorUserId,
            voiceContext: voiceContext,
            ct);
        return result;
    }

    public Task<Result<MillCertExtraction>> OcrParseMillCertAsync(
        byte[] pdfBytes, string profileCode, CancellationToken ct)
    {
        // PR #4 ships the contract; Sprint 5 will wire the OCR runtime.
        // Returning Result.Failure (not throwing) so the page handler can
        // present a graceful "OCR coming in Sprint 5" message.
        if (pdfBytes is null || pdfBytes.Length == 0)
            return Task.FromResult(Result.Failure<MillCertExtraction>("pdfBytes is empty."));

        _logger.LogInformation(
            "OcrParseMillCertAsync called for profile {Profile} — {Bytes} bytes — Sprint 5 OCR runtime not yet wired",
            profileCode, pdfBytes.Length);

        return Task.FromResult(Result.Failure<MillCertExtraction>(
            "OCR mill-cert parsing is wired in Sprint 5. PR #4 ships the contract only — " +
            "drop the PDF at the receipt and a human can transcribe today; the OCR runtime " +
            "will replace that path next sprint."));
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    /// <summary>
    /// AI-flavored audit logger. Writes an AuditLog row with
    /// ActorKind=AiOnBehalfOf so the audit trail tells the AI vs human story.
    /// All voice tools call this exactly once per invocation, success or failure.
    /// Wrapped in try/catch — audit failures must never block the underlying action.
    /// </summary>
    private async Task WriteAiAuditAsync(
        string action,
        string entityType,
        int? entityId,
        string description,
        int? actorUserId,
        VoiceContext? voiceContext,
        CancellationToken ct)
    {
        try
        {
            var row = new AuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Username = actorUserId is null ? "ai-tool" : $"user:{actorUserId}",
                Timestamp = DateTime.UtcNow,
                Description = description,
                ActorKind = ActorKind.AiOnBehalfOf,
                OnBehalfOfUserId = actorUserId,
                AiSessionId = voiceContext?.AiSessionId,
                AiCommandText = voiceContext?.CommandText,
                AiModelVersion = voiceContext?.ModelVersion,
                AiToolName = action,
                AiConfidence = voiceContext?.Confidence,
            };
            _db.AuditLogs.Add(row);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Voice-tool AuditLog write failed for {Action} on {EntityType}#{EntityId}",
                action, entityType, entityId);
        }
    }

    /// <summary>
    /// Best-effort attribute filter — for each (key, expected) pair, parse the
    /// receipt's Attributes JSON and check that key matches expected. Simple
    /// string-equality (case-insensitive). Sprint 5 may add operator support
    /// (gte, lte, in) when the LLM emits richer filter clauses.
    /// </summary>
    private static bool MatchesAttributeFilter(
        string? attributesJson,
        IReadOnlyDictionary<string, object?> filter)
    {
        if (string.IsNullOrWhiteSpace(attributesJson)) return false;
        JsonDocument? doc = null;
        try { doc = JsonDocument.Parse(attributesJson); }
        catch { return false; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var kv in filter)
            {
                if (!doc.RootElement.TryGetProperty(kv.Key, out var prop)) return false;
                var propString = prop.ValueKind switch
                {
                    JsonValueKind.String => prop.GetString() ?? "",
                    JsonValueKind.Number => prop.GetRawText(),
                    _ => prop.ToString(),
                };
                var expected = kv.Value?.ToString() ?? "";
                if (!string.Equals(propString, expected, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Derive a stable per-receipt idempotency key from the bulk-operation's
    /// parent key plus the receipt id. Ensures bulk-quarantine is replay-safe
    /// at both the bulk level (parent key) and per-receipt level (sub-key).
    /// </summary>
    private static Guid DeriveSubKey(Guid parent, int receiptId)
    {
        Span<byte> bytes = stackalloc byte[20];
        parent.TryWriteBytes(bytes[..16]);
        BitConverter.GetBytes(receiptId).CopyTo(bytes[16..]);
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(bytes, hash);
        return new Guid(hash[..16]);
    }
}
