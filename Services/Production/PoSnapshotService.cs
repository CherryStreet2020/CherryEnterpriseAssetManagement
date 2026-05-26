// Sprint 14.1 PR-1 (2026-05-26) — PoSnapshotService impl.
//
// Captures + reads the per-ProductionOrder frozen BOM snapshot. Single-PR
// scope on the post-B6 cascade — sibling Sprint 14.4 (cost engine) will
// extend this to snapshot the 8-element cost split per component.
//
// Concurrency posture: snapshot capture is an at-release event, so collision
// risk is low — but the cost engine + MES reads happen concurrently. EF's
// optimistic concurrency token on ProductionMaterialStructure.RowVersion +
// ProductionOrder header writes (no nav navigation that could race) are
// enough. CaptureAsync wraps the snapshot insert + header stamp in a single
// SaveChangesAsync for atomicity (PR-FS-6 lesson — non-atomic supersede was
// a Codex P1 there; applied prophylactically here from day one).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public sealed class PoSnapshotService : IPoSnapshotService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PoSnapshotService> _logger;

    public PoSnapshotService(AppDbContext db, ILogger<PoSnapshotService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<PoSnapshotSummary>> CaptureAsync(
        int productionOrderId,
        string capturedBy,
        CancellationToken ct)
    {
        if (productionOrderId <= 0)
            return Result.Failure<PoSnapshotSummary>("ProductionOrderId must be > 0.");
        if (string.IsNullOrWhiteSpace(capturedBy))
            return Result.Failure<PoSnapshotSummary>("CapturedBy is required.");

        // Load the PRO with Item (for fingerprinting) + MaterialStructure
        // (with Lines + ChildItem for snapshotting). Single round-trip.
        var po = await _db.ProductionOrders
            .Include(p => p.Item).ThenInclude(i => i!.CurrentReleasedRevision)
            .Include(p => p.MaterialStructure)
                .ThenInclude(ms => ms!.Lines!)
                    .ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);

        if (po == null)
            return Result.Failure<PoSnapshotSummary>($"ProductionOrder {productionOrderId} not found.");

        // Idempotency — if already snapshotted, return the existing summary
        // without re-writing. Re-capture requires explicit ClearSnapshotAsync.
        if (po.SnapshotCapturedAtUtc.HasValue)
        {
            _logger.LogInformation(
                "PoSnapshotService.CaptureAsync: PRO {PoId} already snapshotted at {At} by {By}; returning existing.",
                productionOrderId, po.SnapshotCapturedAtUtc, po.SnapshotCapturedBy);
            return Result.Success(await BuildSummaryAsync(po, ct));
        }

        if (!po.MaterialStructureId.HasValue || po.MaterialStructure == null)
        {
            return Result.Failure<PoSnapshotSummary>(
                $"ProductionOrder {productionOrderId} has no MaterialStructure to snapshot.");
        }

        var nowUtc = DateTime.UtcNow;
        var sourceMs = po.MaterialStructure;
        var sourceLines = sourceMs.Lines?.OrderBy(l => l.Sequence).ToList() ?? new List<MaterialStructureLine>();

        // Build the snapshot rows. Empty BOM = warn-not-fail: header gets
        // stamped, zero lines created, subsequent reads see "snapshotted
        // with no lines" not "never snapshotted."
        var snapshotRows = new List<ProductionMaterialStructure>(sourceLines.Count);
        foreach (var line in sourceLines)
        {
            // ChildItemId is required on MaterialStructureLine; if EF returned
            // a line with a null Item nav, fall back to the FK + a separate
            // lookup so we don't drop the row.
            var childItem = line.Item;
            if (childItem == null)
            {
                childItem = await _db.Items
                    .Include(i => i.CurrentReleasedRevision)
                    .FirstOrDefaultAsync(i => i.Id == line.ItemId, ct);
            }

            if (childItem == null)
            {
                _logger.LogWarning(
                    "PoSnapshotService.CaptureAsync: PRO {PoId} BOM line {LineId} references Item {ItemId} which could not be loaded; skipping.",
                    productionOrderId, line.Id, line.ItemId);
                continue;
            }

            var qtyPer = line.Quantity;
            var scrap = line.ScrapPercent;
            var frozenStdCost = childItem.StandardCost;
            decimal? frozenExtCost = null;
            if (frozenStdCost != 0m)
            {
                var inflation = 1m + ((scrap ?? 0m) / 100m);
                frozenExtCost = decimal.Round(qtyPer * inflation * frozenStdCost, 4, MidpointRounding.AwayFromZero);
            }

            snapshotRows.Add(new ProductionMaterialStructure
            {
                ProductionOrderId = po.Id,
                SourceMaterialStructureLineId = line.Id,
                SourceMaterialStructureId = sourceMs.Id,
                CompanyId = po.CompanyId,
                LocationId = po.LocationId,
                ChildItemId = childItem.Id,
                ChildPartNumber = childItem.PartNumber ?? string.Empty,
                ChildRevision = childItem.CurrentReleasedRevision?.RevisionCode ?? childItem.Revision,
                ChildItemRevisionId = childItem.CurrentReleasedRevisionId,
                ChildItemFingerprintHash = ComputeItemFingerprint(childItem),
                Sequence = line.Sequence,
                QuantityPer = qtyPer,
                Uom = line.Uom,
                ScrapPercent = scrap,
                PhaseSequence = line.PhaseSequence,
                LineKind = line.LineKind,
                IssueMethod = BomIssueMethod.Pull, // industry default; line-level override comes in a later sprint
                IsPhantom = childItem.IsPhantom,
                FrozenStandardCost = frozenStdCost == 0m ? null : frozenStdCost,
                FrozenExtendedCost = frozenExtCost,
                TypeSpecificProperties = line.TypeSpecificProperties,
                Notes = line.Notes,
                CreatedAt = nowUtc,
                CapturedBy = capturedBy,
            });
        }

        // Stamp PRO header — even if zero snapshot rows, the timestamps
        // record "this PRO has been snapshotted (with an empty BOM)" so
        // subsequent CaptureAsync calls correctly short-circuit as idempotent.
        po.SnapshotCapturedAtUtc = nowUtc;
        po.SnapshotCapturedBy = capturedBy;
        po.SourceMaterialStructureRevision = sourceMs.Revision;
        po.SourceItemRevisionId = po.Item?.CurrentReleasedRevisionId;

        if (snapshotRows.Count > 0)
        {
            _db.ProductionMaterialStructures.AddRange(snapshotRows);
        }

        // Single SaveChanges — atomic per the PR-FS-6 lesson.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PoSnapshotService.CaptureAsync: PRO {PoId} ({OrderNumber}) snapshotted {LineCount} lines from MaterialStructure {MsId} (Rev {Rev}) by {By}.",
            po.Id, po.OrderNumber, snapshotRows.Count, sourceMs.Id, sourceMs.Revision ?? "(none)", capturedBy);

        return Result.Success(await BuildSummaryAsync(po, ct));
    }

    public Task<PoSnapshotSummary> GetSnapshotAsync(int productionOrderId, CancellationToken ct)
    {
        // Read-only — no idempotency concerns. Pull header + snapshot lines
        // in a single query.
        return GetSnapshotInternalAsync(productionOrderId, ct);
    }

    public async Task<Result<PoSnapshotSummary>> ClearSnapshotAsync(
        int productionOrderId,
        string clearedBy,
        string reason,
        CancellationToken ct)
    {
        if (productionOrderId <= 0)
            return Result.Failure<PoSnapshotSummary>("ProductionOrderId must be > 0.");
        if (string.IsNullOrWhiteSpace(clearedBy))
            return Result.Failure<PoSnapshotSummary>("ClearedBy is required.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<PoSnapshotSummary>("Reason is required.");

        var po = await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);
        if (po == null)
            return Result.Failure<PoSnapshotSummary>($"ProductionOrder {productionOrderId} not found.");

        if (!po.SnapshotCapturedAtUtc.HasValue)
        {
            // Nothing to clear — idempotent.
            _logger.LogInformation(
                "PoSnapshotService.ClearSnapshotAsync: PRO {PoId} has no snapshot to clear (idempotent).",
                productionOrderId);
            return Result.Success(await BuildSummaryAsync(po, ct));
        }

        var existing = await _db.ProductionMaterialStructures
            .Where(s => s.ProductionOrderId == productionOrderId)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            _db.ProductionMaterialStructures.RemoveRange(existing);
        }

        po.SnapshotCapturedAtUtc = null;
        po.SnapshotCapturedBy = null;
        po.SourceMaterialStructureRevision = null;
        po.SourceItemRevisionId = null;

        await _db.SaveChangesAsync(ct);

        _logger.LogWarning(
            "PoSnapshotService.ClearSnapshotAsync: PRO {PoId} ({OrderNumber}) snapshot CLEARED by {By}. Reason: {Reason}. {LineCount} snapshot lines deleted.",
            po.Id, po.OrderNumber, clearedBy, reason, existing.Count);

        return Result.Success(await BuildSummaryAsync(po, ct));
    }

    // ---------- internals ---------------------------------------------------

    private async Task<PoSnapshotSummary> GetSnapshotInternalAsync(int productionOrderId, CancellationToken ct)
    {
        var po = await _db.ProductionOrders
            .Include(p => p.SourceItemRevision)
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);
        if (po == null)
        {
            return new PoSnapshotSummary(
                productionOrderId, "(not found)", null, null, null, null, null, null,
                System.Array.Empty<PoSnapshotLine>());
        }
        return await BuildSummaryAsync(po, ct);
    }

    private async Task<PoSnapshotSummary> BuildSummaryAsync(ProductionOrder po, CancellationToken ct)
    {
        var lines = await _db.ProductionMaterialStructures
            .AsNoTracking()
            .Where(s => s.ProductionOrderId == po.Id)
            .OrderBy(s => s.Sequence)
            .Select(s => new PoSnapshotLine(
                s.Id,
                s.Sequence,
                s.ChildItemId,
                s.ChildPartNumber,
                s.ChildRevision,
                s.ChildItemRevisionId,
                s.ChildItemFingerprintHash,
                s.LineKind,
                s.IssueMethod,
                s.QuantityPer,
                s.Uom,
                s.ScrapPercent,
                s.PhaseSequence,
                s.IsPhantom,
                s.FrozenStandardCost,
                s.FrozenExtendedCost,
                s.SourceMaterialStructureLineId,
                s.Notes))
            .ToListAsync(ct);

        // Best-effort revision code resolution if the nav wasn't loaded.
        string? revCode = po.SourceItemRevision?.RevisionCode;
        if (revCode == null && po.SourceItemRevisionId.HasValue)
        {
            revCode = await _db.ItemRevisions
                .AsNoTracking()
                .Where(r => r.Id == po.SourceItemRevisionId.Value)
                .Select(r => r.RevisionCode)
                .FirstOrDefaultAsync(ct);
        }

        return new PoSnapshotSummary(
            po.Id,
            po.OrderNumber,
            po.MaterialStructureId,
            po.SourceMaterialStructureRevision,
            po.SourceItemRevisionId,
            revCode,
            po.SnapshotCapturedAtUtc,
            po.SnapshotCapturedBy,
            lines);
    }

    /// <summary>
    /// SHA-256 of frozen Item fields used to detect post-release drift on the
    /// child Item. Lower-case hex, 64 chars. Canonical order matters — never
    /// reorder these fields, only append.
    /// </summary>
    internal static string ComputeItemFingerprint(Item item)
    {
        // Canonical pipe-separated representation. Use invariant culture so
        // decimal formatting is identical across regions.
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(256);
        sb.Append(item.PartNumber ?? string.Empty); sb.Append('|');
        sb.Append(item.Description ?? string.Empty); sb.Append('|');
        sb.Append(item.Revision ?? string.Empty); sb.Append('|');
        sb.Append(item.StockUOM ?? string.Empty); sb.Append('|');
        sb.Append(item.StandardCost.ToString(inv)); sb.Append('|');
        sb.Append(item.IsPhantom ? "1" : "0"); sb.Append('|');
        sb.Append(((int)item.MakeBuyCode).ToString(inv)); sb.Append('|');
        sb.Append(((int)item.PlanningPolicy).ToString(inv)); sb.Append('|');
        sb.Append(item.IsSellable ? "1" : "0"); sb.Append('|');
        sb.Append(((int)item.LifecycleStage).ToString(inv)); sb.Append('|');
        sb.Append(item.AS9100Critical ? "1" : "0"); sb.Append('|');
        sb.Append(item.ECCN ?? string.Empty); sb.Append('|');
        sb.Append(item.CurrentReleasedRevisionId?.ToString(inv) ?? string.Empty);

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) hex.Append(b.ToString("x2"));
        return hex.ToString();
    }
}
