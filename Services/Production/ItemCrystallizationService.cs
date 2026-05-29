// Theme B7 Wave B PR-5 (2026-05-29) — ItemCrystallizationService impl.
//
// THE BIC DIFFERENTIATOR (see IItemCrystallizationService for the why).
//
// Patterns adopted from the codebase:
//   - ITenantContext.VisibleCompanyIds gating on EVERY read + write.
//   - Result.Success / Result.Failure return shape.
//   - Two-phase numbering for CrystallizationNumber (CRYST-{guid:N} → CRYST-
//     YYYY-NNNNNN), fits varchar(40) (PR-4 hotfix lesson — placeholder length
//     must be ≤ column width).
//   - Cross-service transaction enlistment (HARD LOCK): CrystallizeAsync owns
//     its tx but uses the enlisted-tx pattern so it composes safely if a caller
//     ever wraps it. All writes share the scoped AppDbContext + bare SaveChanges
//     inside the open tx.
//   - CrystallizationFingerprint (PR-4) for the dedupe key.
//
// Compliance (§7): dedupe is NEVER auto-linked (decision #3) — the caller must
// explicitly confirm-link or force-create-new when a fingerprint match exists.
// A PoFirst order requires drawing #/rev (AS9100 §8.5.2) which is frozen onto
// the audit record as the as-built lineage; the crystallized part #/rev mirror
// the as-planned identity the order carried at release. Reversal NEVER rewrites
// the as-built history (ProductionMaterialStructure / ProductionOperation).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public sealed class ItemCrystallizationService : IItemCrystallizationService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ItemCrystallizationService> _logger;

    public ItemCrystallizationService(
        AppDbContext db, ITenantContext tenant, ILogger<ItemCrystallizationService> logger)
    {
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PREVIEW (read-only)
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<CrystallizationPreview>> PreviewCrystallizationAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        if (productionOrderId <= 0)
            return Result.Failure<CrystallizationPreview>("ProductionOrderId must be > 0.");

        var pro = await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == productionOrderId
                && _tenant.VisibleCompanyIds.Contains(p.CompanyId), ct);
        if (pro == null)
            return Result.Failure<CrystallizationPreview>(
                $"ProductionOrder {productionOrderId} not found in your tenant scope.");

        var bom = await LoadBomAsync(productionOrderId, ct);
        var ops = await LoadOpsAsync(productionOrderId, ct);
        var fingerprint = CrystallizationFingerprint.Compute(bom, ops);

        var warnings = new List<string>();
        if (!pro.IsPoFirst)
            warnings.Add("This order is not flagged PoFirst — crystallization is intended for master-optional (ETO) orders.");
        if (bom.Count == 0)
            warnings.Add("No as-built BOM lines captured — the crystallized standard BOM would be empty.");
        if (ops.Count == 0)
            warnings.Add("No as-run operations captured — the crystallized standard routing would be empty.");

        var (seededCost, costSource) = await ResolveSeededCostAsync(pro, ct);
        if (seededCost == null)
            warnings.Add("No actual cost summary found — the seeded standard cost would be null until cost is posted (PR-6 seeds the element split).");

        // Dedupe: an existing, non-reversed crystallization that MINTED a master
        // with the same structural fingerprint, in tenant scope.
        var (dupeCrystId, dupeItemId, dupePart) = await FindDedupeMatchAsync(pro.CompanyId, fingerprint, ct);

        var bomPreview = bom
            .OrderBy(l => l.Sequence).ThenBy(l => l.ChildPartNumber, StringComparer.Ordinal)
            .Select(l => new CrystallizationBomLinePreview(
                l.Sequence, l.ChildItemId, l.ChildPartNumber, l.ChildRevision,
                l.QuantityPer, l.Uom, l.LineKind))
            .ToList();

        var opsPreview = ops
            .OrderBy(o => o.SequenceNumber).ThenBy(o => o.WorkCenterId)
            .Select(o => new CrystallizationRoutingOpPreview(
                o.SequenceNumber, o.WorkCenterId, o.OperationType, o.Description,
                o.PlannedSetupMins, o.PlannedRunMins))
            .ToList();

        return Result.Success(new CrystallizationPreview(
            pro.Id, pro.OrderNumber, pro.IsPoFirst,
            pro.AsPlannedPartNumber, pro.AsPlannedDrawingRev, pro.AsPlannedDescription,
            fingerprint, seededCost, costSource,
            bomPreview, opsPreview,
            dupeItemId, dupePart, dupeCrystId,
            pro.CrystallizedItemId != null, pro.CrystallizedItemId,
            warnings));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CRYSTALLIZE (atomic mint or dedupe-link)
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<CrystallizeResult>> CrystallizeAsync(
        CrystallizeRequest request, CancellationToken ct = default)
    {
        if (request.ProductionOrderId <= 0)
            return Result.Failure<CrystallizeResult>("ProductionOrderId must be > 0.");
        if (string.IsNullOrWhiteSpace(request.By))
            return Result.Failure<CrystallizeResult>("By (actor) is required.");

        var pro = await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == request.ProductionOrderId
                && _tenant.VisibleCompanyIds.Contains(p.CompanyId), ct);
        if (pro == null)
            return Result.Failure<CrystallizeResult>(
                $"ProductionOrder {request.ProductionOrderId} not found in your tenant scope.");

        // Guard: already crystallized — idempotent refusal (reverse first to redo).
        if (pro.CrystallizedItemId != null)
            return Result.Failure<CrystallizeResult>(
                $"PRO {pro.OrderNumber} is already crystallized into Item {pro.CrystallizedItemId}. " +
                "Reverse the existing crystallization before re-crystallizing.");

        // Compliance guard (§7): a PoFirst order must carry drawing #/rev — the
        // crystallized master's configuration lineage. (Release already enforces
        // this, but crystallization re-checks: the audit record must not be born
        // without the AS9100 §8.5.2 anchor.)
        if (pro.IsPoFirst &&
            (string.IsNullOrWhiteSpace(pro.AsPlannedDrawingNumber) ||
             string.IsNullOrWhiteSpace(pro.AsPlannedDrawingRev)))
            return Result.Failure<CrystallizeResult>(
                "Cannot crystallize a PoFirst order without an as-planned drawing number + revision " +
                "(AS9100 §8.5.2). The crystallized master must carry its configuration lineage.");

        var bom = await LoadBomAsync(pro.Id, ct);
        var ops = await LoadOpsAsync(pro.Id, ct);
        var fingerprint = CrystallizationFingerprint.Compute(bom, ops);
        var (seededCost, costSource) = await ResolveSeededCostAsync(pro, ct);
        var (dupeCrystId, dupeItemId, dupePart) = await FindDedupeMatchAsync(pro.CompanyId, fingerprint, ct);

        // ── Resolve the path: LINK vs CREATE, with the human-confirm gate ──
        bool linkPath;
        int linkItemId = 0;
        if (request.ConfirmDedupeLink)
        {
            // Explicit link. Validate the target.
            if (request.LinkToExistingItemId is not int targetId || targetId <= 0)
                return Result.Failure<CrystallizeResult>(
                    "ConfirmDedupeLink was set but LinkToExistingItemId is missing.");
            var target = await _db.Items.FirstOrDefaultAsync(
                i => i.Id == targetId
                    && (i.CompanyId == null || _tenant.VisibleCompanyIds.Contains(i.CompanyId.Value)), ct);
            if (target == null)
                return Result.Failure<CrystallizeResult>(
                    $"LinkToExistingItemId {targetId} not found in your tenant scope.");
            linkPath = true;
            linkItemId = targetId;
        }
        else if (dupeItemId != null && !request.ForceCreateNew)
        {
            // Dedupe match exists and the caller has NOT resolved it. NEVER auto-link
            // (decision #3) and never silently mint a duplicate — require a decision.
            return Result.Failure<CrystallizeResult>(
                $"A matching standard already exists: Item {dupeItemId} ({dupePart}) was crystallized " +
                $"from an identical as-built structure (crystallization {dupeCrystId}). Confirm the link " +
                $"(ConfirmDedupeLink + LinkToExistingItemId={dupeItemId}) or set ForceCreateNew to mint a new master anyway.");
        }
        else
        {
            linkPath = false;
        }

        var nowUtc = DateTime.UtcNow;
        var partNumber = !string.IsNullOrWhiteSpace(pro.AsPlannedPartNumber)
            ? pro.AsPlannedPartNumber!
            : $"ETO-{pro.OrderNumber}";
        var revision = pro.AsPlannedDrawingRev;
        // Item.Revision (varchar 10) + Routing.RevisionNumber (varchar 10) are
        // narrower than ProductionOrder.AsPlannedDrawingRev (varchar 16). Clamp
        // the master-side rev so a long ECO-suffixed drawing rev can't overflow
        // those columns. The FULL rev is preserved verbatim on the audit record
        // (ItemCrystallization.AsBuiltPartRev / AsBuiltDrawingRev are varchar 16).
        var masterRev = revision is { Length: > 10 } ? revision[..10] : revision;

        // Cross-service transaction enlistment (HARD LOCK).
        var existingTx = _db.Database.CurrentTransaction;
        var tx = existingTx ?? await _db.Database.BeginTransactionAsync(ct);
        var ownsTx = existingTx == null;
        try
        {
            int? createdItemId = null;
            int? materialStructureId = null;
            int? routingId = null;
            CrystallizationOutcome outcome;
            string rationale;

            if (linkPath)
            {
                outcome = CrystallizationOutcome.LinkedToExisting;
                pro.CrystallizedItemId = linkItemId;
                rationale = request.RationaleOverride
                    ?? $"Linked PRO {pro.OrderNumber} to existing standard Item {linkItemId} " +
                       $"(human-confirmed dedupe match on structural fingerprint {fingerprint[..12]}…).";
            }
            else
            {
                // ── Mint the Item Master from as-built actuals ──
                var minted = new Item
                {
                    CompanyId = pro.CompanyId,
                    PartNumber = partNumber,
                    Description = !string.IsNullOrWhiteSpace(pro.AsPlannedDescription)
                        ? pro.AsPlannedDescription!
                        : $"Crystallized from {pro.OrderNumber}",
                    Revision = masterRev,
                    Type = ItemType.Part,
                    Status = ItemStatus.Active,
                    UOM = UnitOfMeasure.Each,
                    StockUOM = "EA",
                    CostMethod = CostMethod.Average,
                    StandardCost = seededCost ?? 0m,
                    // B7: an ETO-originated master is PoFirst (a repeat candidate);
                    // it stays PoFirst until a 2nd run / deliberate standard-set
                    // promotes it (PR-6 sets the StandardCostBasis flag).
                    SourcePattern = SourcePattern.PoFirst,
                    LifecycleStage = LifecycleStage.Production,
                    MakeBuyCode = MakeBuyCode.Make,
                    IsSellable = true,
                };
                _db.Items.Add(minted);
                await _db.SaveChangesAsync(ct);
                createdItemId = minted.Id;

                // ── Standard BOM (Bom subtype of MaterialStructure) from as-built ──
                var ms = new MaterialStructure
                {
                    CompanyId = pro.CompanyId,
                    LocationId = pro.LocationId,
                    IsSiteWideTemplate = pro.LocationId == null,
                    StructureNumber = $"MS-CRYST-{Guid.NewGuid():N}",
                    Name = $"BOM — {partNumber}" + (revision != null ? $" Rev {revision}" : ""),
                    StructureType = StructureType.Bom,
                    Status = MaterialStructureStatus.Approved,
                    Revision = revision ?? "A",
                    OutputItemId = minted.Id,
                    CreatedBy = request.By,
                };
                _db.Set<MaterialStructure>().Add(ms);
                await _db.SaveChangesAsync(ct);
                materialStructureId = ms.Id;

                _db.Set<Bom>().Add(new Bom
                {
                    MaterialStructureId = ms.Id,
                    BomType = BomType.Manufacturing, // MBOM — as-built on the floor
                    IsPhantom = false,
                });

                foreach (var l in bom.OrderBy(l => l.Sequence).ThenBy(l => l.ChildPartNumber, StringComparer.Ordinal))
                {
                    _db.Set<MaterialStructureLine>().Add(new MaterialStructureLine
                    {
                        MaterialStructureId = ms.Id,
                        ItemId = l.ChildItemId,
                        LineKind = l.LineKind,
                        Sequence = l.Sequence,
                        Quantity = l.QuantityPer,
                        Uom = l.Uom,
                        ScrapPercent = l.ScrapPercent,
                    });
                }

                // ── Standard Routing from as-run operations ──
                var routing = new Routing
                {
                    CompanyId = pro.CompanyId,
                    Code = $"RT-CRYST-{Guid.NewGuid():N}",
                    RevisionNumber = masterRev ?? "A",
                    Name = $"Routing — {partNumber}",
                    ItemId = minted.Id,
                    Type = RoutingType.Discrete,
                    Status = RoutingStatus.Approved,
                    LocationId = pro.LocationId,
                    IsSiteWideTemplate = pro.LocationId == null,
                    LotBaseSize = 1,
                    UnitOfMeasure = "EA",
                    IsDefault = true,
                    CreatedBy = request.By,
                };
                _db.Set<Routing>().Add(routing);
                await _db.SaveChangesAsync(ct);
                routingId = routing.Id;

                foreach (var o in ops.OrderBy(o => o.SequenceNumber).ThenBy(o => o.WorkCenterId))
                {
                    _db.Set<RoutingOperation>().Add(new RoutingOperation
                    {
                        RoutingId = routing.Id,
                        SequenceNumber = o.SequenceNumber,
                        LocationIdSnapshot = o.LocationIdSnapshot,
                        WorkCenterId = o.WorkCenterId,
                        OperationType = o.OperationType,
                        Description = string.IsNullOrWhiteSpace(o.Description) ? "Operation" : o.Description,
                        // As-run actuals preferred; fall back to planned for the standard.
                        SetupTimeMins = o.ActualSetupMins > 0 ? o.ActualSetupMins : o.PlannedSetupMins,
                        RunTimePerUnitMins = o.ActualRunMins > 0 ? o.ActualRunMins : o.PlannedRunMins,
                    });
                }

                outcome = CrystallizationOutcome.CreatedNewItem;
                pro.CrystallizedItemId = minted.Id;
                rationale = request.RationaleOverride
                    ?? $"Minted standard Item {partNumber}" + (revision != null ? $" Rev {revision}" : "") +
                       $" from PRO {pro.OrderNumber} as-built actuals: {bom.Count} BOM line(s), {ops.Count} routing op(s), " +
                       $"standard cost ${seededCost ?? 0m:N4} ({costSource}). Structural fingerprint {fingerprint[..12]}….";
            }

            // ── The crystallization audit record (two-phase number) ──
            var cryst = new ItemCrystallization
            {
                CompanyId = pro.CompanyId,
                SiteId = pro.LocationId,
                SourceProductionOrderId = pro.Id,
                CrystallizationNumber = $"CRYST-{Guid.NewGuid():N}",  // ≤ varchar(40); patched below
                Outcome = outcome,
                CreatedItemId = createdItemId,
                MatchedItemId = linkPath ? linkItemId : (int?)null,
                StructureFingerprintHash = fingerprint,
                SeededStandardCost = seededCost,
                CostSource = costSource,
                AsBuiltPartNumber = partNumber,
                AsBuiltPartRev = revision,
                AsBuiltDrawingNumber = pro.AsPlannedDrawingNumber,
                AsBuiltDrawingRev = pro.AsPlannedDrawingRev,
                RationaleText = rationale,
                CrystallizedAtUtc = nowUtc,
                CrystallizedBy = request.By,
                CreatedAtUtc = nowUtc,
                CreatedBy = request.By,
            };
            _db.ItemCrystallizations.Add(cryst);
            await _db.SaveChangesAsync(ct);

            cryst.CrystallizationNumber = $"CRYST-{nowUtc:yyyy}-{cryst.Id:D6}";
            await _db.SaveChangesAsync(ct);

            if (ownsTx) await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Crystallized PRO {Pro} ({Outcome}) → {Num} (item {Item}, ms {Ms}, routing {Rt}).",
                pro.Id, outcome, cryst.CrystallizationNumber, createdItemId ?? linkItemId,
                materialStructureId, routingId);

            var msg = linkPath
                ? $"PRO {pro.OrderNumber} linked to existing standard Item {linkItemId} ({cryst.CrystallizationNumber})."
                : $"PRO {pro.OrderNumber} crystallized into new standard Item {partNumber} " +
                  $"(#{createdItemId}) with standard BOM + Routing + cost ${seededCost ?? 0m:N2} ({cryst.CrystallizationNumber}).";

            return Result.Success(new CrystallizeResult(
                cryst.Id, cryst.CrystallizationNumber, outcome,
                createdItemId, linkPath ? linkItemId : (int?)null,
                materialStructureId, routingId, seededCost, msg));
        }
        catch (Exception ex)
        {
            if (ownsTx) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "CrystallizeAsync failed for PRO {Pro}.", request.ProductionOrderId);
            return Result.Failure<CrystallizeResult>($"Crystallization failed: {ex.Message}");
        }
        finally
        {
            if (ownsTx) await tx.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // REVERSE
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<ReverseCrystallizationResult>> ReverseCrystallizationAsync(
        int crystallizationId, string reason, string by, CancellationToken ct = default)
    {
        if (crystallizationId <= 0)
            return Result.Failure<ReverseCrystallizationResult>("crystallizationId must be > 0.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<ReverseCrystallizationResult>("A reversal reason is required.");
        if (string.IsNullOrWhiteSpace(by))
            return Result.Failure<ReverseCrystallizationResult>("By (actor) is required.");

        var cryst = await _db.ItemCrystallizations.FirstOrDefaultAsync(
            c => c.Id == crystallizationId && _tenant.VisibleCompanyIds.Contains(c.CompanyId), ct);
        if (cryst == null)
            return Result.Failure<ReverseCrystallizationResult>(
                $"Crystallization {crystallizationId} not found in your tenant scope.");

        if (cryst.IsReversed)
            return Result.Success(new ReverseCrystallizationResult(
                cryst.Id, cryst.CrystallizationNumber, "Already reversed (idempotent)."));

        var nowUtc = DateTime.UtcNow;
        var existingTx = _db.Database.CurrentTransaction;
        var tx = existingTx ?? await _db.Database.BeginTransactionAsync(ct);
        var ownsTx = existingTx == null;
        try
        {
            // Unlink the source PRO so it can be re-crystallized. The as-built
            // records (ProductionMaterialStructure / ProductionOperation) are
            // NEVER touched — reversal is a master-data convenience, not a
            // rewrite of history (§7). The minted Item / BOM / Routing are
            // retained as independent records.
            var pro = await _db.ProductionOrders.FirstOrDefaultAsync(
                p => p.Id == cryst.SourceProductionOrderId, ct);
            if (pro != null && pro.CrystallizedItemId != null &&
                (pro.CrystallizedItemId == cryst.CreatedItemId || pro.CrystallizedItemId == cryst.MatchedItemId))
            {
                pro.CrystallizedItemId = null;
            }

            cryst.IsReversed = true;
            cryst.ReversedAtUtc = nowUtc;
            cryst.ReversedBy = by;
            cryst.ReversalReason = reason;
            cryst.UpdatedAtUtc = nowUtc;
            cryst.UpdatedBy = by;
            await _db.SaveChangesAsync(ct);

            if (ownsTx) await tx.CommitAsync(ct);

            _logger.LogWarning("Crystallization {Num} reversed by {By}: {Reason}",
                cryst.CrystallizationNumber, by, reason);

            return Result.Success(new ReverseCrystallizationResult(
                cryst.Id, cryst.CrystallizationNumber,
                $"Crystallization {cryst.CrystallizationNumber} reversed. The PRO is unlinked and can be " +
                "re-crystallized; the as-built records and any minted master are retained."));
        }
        catch (Exception ex)
        {
            if (ownsTx) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "ReverseCrystallizationAsync failed for {Id}.", crystallizationId);
            return Result.Failure<ReverseCrystallizationResult>($"Reversal failed: {ex.Message}");
        }
        finally
        {
            if (ownsTx) await tx.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private Task<List<ProductionMaterialStructure>> LoadBomAsync(int proId, CancellationToken ct) =>
        _db.ProductionMaterialStructures.AsNoTracking()
            .Where(s => s.ProductionOrderId == proId).ToListAsync(ct);

    private Task<List<ProductionOperation>> LoadOpsAsync(int proId, CancellationToken ct) =>
        _db.ProductionOperations.AsNoTracking()
            .Where(o => o.ProductionOrderId == proId).ToListAsync(ct);

    /// <summary>
    /// First-actual cost per good unit (§5.4). Prefers the summary's CostPerGoodUnit,
    /// else ActualTotalCost / GoodQuantityCompleted, else ActualTotalCost scalar.
    /// </summary>
    private async Task<(decimal? cost, CrystallizationCostSource source)> ResolveSeededCostAsync(
        ProductionOrder pro, CancellationToken ct)
    {
        var summary = await _db.ProductionOrderCostSummaries.AsNoTracking()
            .Where(s => s.ProductionOrderId == pro.Id
                && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (summary == null) return (null, CrystallizationCostSource.FirstActual);

        if (summary.CostPerGoodUnit is { } perUnit && perUnit > 0m)
            return (decimal.Round(perUnit, 4, MidpointRounding.AwayFromZero), CrystallizationCostSource.FirstActual);

        var good = summary.GoodQuantityCompleted ?? 0m;
        if (summary.ActualTotalCost > 0m && good > 0m)
            return (decimal.Round(summary.ActualTotalCost / good, 4, MidpointRounding.AwayFromZero),
                CrystallizationCostSource.FirstActual);

        if (summary.ActualTotalCost > 0m)
            return (decimal.Round(summary.ActualTotalCost, 4, MidpointRounding.AwayFromZero),
                CrystallizationCostSource.FirstActual);

        return (null, CrystallizationCostSource.FirstActual);
    }

    /// <summary>
    /// Find a non-reversed crystallization that MINTED a master (Outcome=CreatedNewItem,
    /// CreatedItemId set) with the same structural fingerprint, in tenant scope. Returns
    /// the match's (crystallizationId, itemId, partNumber) or nulls. The dedupe match is
    /// only ever SURFACED — never auto-applied (decision #3).
    /// </summary>
    private async Task<(int? crystId, int? itemId, string? partNumber)> FindDedupeMatchAsync(
        int companyId, string fingerprint, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(fingerprint)) return (null, null, null);

        var match = await _db.ItemCrystallizations.AsNoTracking()
            .Where(c => c.CompanyId == companyId
                && _tenant.VisibleCompanyIds.Contains(c.CompanyId)
                && !c.IsReversed
                && c.Outcome == CrystallizationOutcome.CreatedNewItem
                && c.CreatedItemId != null
                && c.StructureFingerprintHash == fingerprint)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.CreatedItemId })
            .FirstOrDefaultAsync(ct);
        if (match == null) return (null, null, null);

        var part = await _db.Items.AsNoTracking()
            .Where(i => i.Id == match.CreatedItemId!.Value)
            .Select(i => i.PartNumber)
            .FirstOrDefaultAsync(ct);

        return (match.Id, match.CreatedItemId, part);
    }
}
