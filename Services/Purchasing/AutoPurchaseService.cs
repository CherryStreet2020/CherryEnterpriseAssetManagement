// =============================================================================
// CherryAI EAM — AutoPurchaseService (Sprint 15.3 PR-14)
//
// Implementation of the §16 auto-PO decision engine. Pure read; no writes.
// =============================================================================

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

namespace Abs.FixedAssets.Services.Purchasing;

public class AutoPurchaseService : IAutoPurchaseService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<AutoPurchaseService> _log;

    public AutoPurchaseService(
        AppDbContext db,
        ITenantContext tenant,
        ILogger<AutoPurchaseService> log)
    {
        _db = db;
        _tenant = tenant;
        _log = log;
    }

    // ═════════════════════════════════════════════════════════════════════
    // SINGLE-DEMAND EVALUATION
    // ═════════════════════════════════════════════════════════════════════

    public async Task<Result<AutoPoCandidate>> EvaluateDemandAsync(
        int demandId,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var demand = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Include(d => d.ProductionOrder)
            .Where(d => d.Id == demandId && visible.Contains(d.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (demand == null)
            return Result.Failure<AutoPoCandidate>(
                $"ProductionSupplyDemand {demandId} not found or out of tenant scope.");

        var candidate = await EvaluateAsync(demand, ct);
        return Result.Success(candidate);
    }

    public async Task<Result<IReadOnlyList<AutoPoCandidate>>> EvaluateProductionOrderAsync(
        int productionOrderId,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var demands = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Include(d => d.ProductionOrder)
            .Where(d => d.ProductionOrderId == productionOrderId
                        && visible.Contains(d.CompanyId)
                        && d.BuyerActionState != BuyerActionState.Closed
                        && d.BuyerActionState != BuyerActionState.Cancelled)
            .ToListAsync(ct);

        var results = new List<AutoPoCandidate>(demands.Count);
        foreach (var d in demands)
            results.Add(await EvaluateAsync(d, ct));

        return Result.Success<IReadOnlyList<AutoPoCandidate>>(results);
    }

    public async Task<Result<AutoPoCandidatePage>> GetCandidatesAsync(
        AutoPoEvaluationFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var take = Math.Clamp(filter.Take, 1, 500);
        var skip = Math.Max(0, filter.Skip);

        // Pre-filter the demand backlog before per-row evaluation. We exclude
        // terminal lifecycle states + demands that already have a linked PO
        // (those are AlreadySatisfied and shouldn't surface as auto-PO
        // candidates). This keeps the evaluation loop cheap at scale.
        var q = _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Include(d => d.ProductionOrder)
            .Where(d => visible.Contains(d.CompanyId)
                        && d.BuyerActionState != BuyerActionState.Closed
                        && d.BuyerActionState != BuyerActionState.Cancelled
                        && d.LinkedPurchaseOrderId == null);

        if (filter.CompanyId.HasValue) q = q.Where(d => d.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue) q = q.Where(d => d.SiteId == filter.SiteId);
        if (filter.VendorId.HasValue) q = q.Where(d => d.VendorId == filter.VendorId);
        if (filter.ProductionOrderId.HasValue)
            q = q.Where(d => d.ProductionOrderId == filter.ProductionOrderId);
        if (filter.BuyerUserId.HasValue)
            q = q.Where(d => d.BuyerUserId == filter.BuyerUserId);

        // Materialise enough rows to evaluate. We over-fetch by 2x to allow
        // the post-evaluation EligibleOnly filter to still satisfy the page
        // size — a demand can be excluded if it ends up NotEligible. Hard cap
        // at 500 to bound work.
        var fetchTake = Math.Min(500, take * 2 + skip);
        var demandSlice = await q
            .OrderBy(d => d.RequiredDate ?? DateTime.MaxValue)
            .ThenBy(d => d.Id)
            .Take(fetchTake)
            .ToListAsync(ct);

        var evaluated = new List<AutoPoCandidate>(demandSlice.Count);
        foreach (var d in demandSlice)
            evaluated.Add(await EvaluateAsync(d, ct));

        var filtered = filter.EligibleOnly
            ? evaluated.Where(c => c.Decision == AutoPoDecision.Eligible).ToList()
            : evaluated;

        var paged = filtered.Skip(skip).Take(take).ToList();
        var eligibleCount = filtered.Count(c => c.Decision == AutoPoDecision.Eligible);
        var blockedCount = filtered.Count(c => c.Decision == AutoPoDecision.BlockedReviewRequired);

        return Result.Success(new AutoPoCandidatePage(
            TotalCount: filtered.Count,
            EligibleCount: eligibleCount,
            BlockedCount: blockedCount,
            Candidates: paged));
    }

    // ═════════════════════════════════════════════════════════════════════
    // EVALUATION CORE — applies every §16 trigger + blocker rule
    // ═════════════════════════════════════════════════════════════════════

    private async Task<AutoPoCandidate> EvaluateAsync(
        ProductionSupplyDemand d,
        CancellationToken ct)
    {
        var visible = _tenant.VisibleCompanyIds;

        // Early exit — already satisfied.
        if (d.LinkedPurchaseOrderId != null
            || d.LinkedChildProductionOrderId != null
            || !string.IsNullOrEmpty(d.LinkedInventoryReservation))
        {
            return Build(d, AutoPoDecision.AlreadySatisfied,
                Array.Empty<AutoPoTrigger>(),
                Array.Empty<AutoPoBlocker>(),
                "Demand already has a linked supply — no new PO needed.",
                suggestedVendorId: null, suggestedVendorName: null);
        }

        var triggers = new List<AutoPoTrigger>();
        var blockers = new List<AutoPoBlocker>();

        // ─── TRIGGER rules (§16) ────────────────────────────────────────

        // P1-B fix — trigger MUST be expressed in terms of SupplyPolicy, not
        // SourceType. `DemandSourceType.BomLine` covers BOTH buy AND make BOM
        // lines; firing the buy trigger on Make/Transfer/Floorstock policies
        // would emit false-positive Eligible decisions.
        // P2-B fix — extend PRO-status check to include InProgress so late
        // material shortages discovered during execution still fire trigger 1.
        bool proIsLive = d.ProductionOrder != null
            && (d.ProductionOrder.Status == ProductionOrderStatus.Released
                || d.ProductionOrder.Status == ProductionOrderStatus.InProgress);
        bool isBuyPolicy = d.SupplyPolicy == SupplyPolicy.BuyDirectToJob
                           || d.SupplyPolicy == SupplyPolicy.BuyDirectToOperation
                           || d.SupplyPolicy == SupplyPolicy.BuyToVendorLocation
                           || d.SupplyPolicy == SupplyPolicy.InventoryFirstThenBuy;
        if (proIsLive && isBuyPolicy)
        {
            triggers.Add(AutoPoTrigger.ProductionOrderReleasedBuyLine);
        }

        if (d.SourceType == DemandSourceType.Subcontract
            || d.SupplyPolicy == SupplyPolicy.BuyToVendorLocation)
        {
            triggers.Add(AutoPoTrigger.SubcontractOperationExists);
        }

        if (d.ShortageStatus == DemandShortageStatus.Late
            || d.ShortageStatus == DemandShortageStatus.Critical
            || d.AlertStatus == DemandAlertStatus.Critical)
        {
            triggers.Add(AutoPoTrigger.OperationMaterialShortage);
        }

        if (d.ParentDemandId.HasValue
            && (d.SupplyPolicy == SupplyPolicy.BuyDirectToJob
                || d.SupplyPolicy == SupplyPolicy.BuyDirectToOperation))
        {
            triggers.Add(AutoPoTrigger.ChildJobBuyToJobMaterial);
        }

        if (d.ProjectId.HasValue
            && d.RequiredDate.HasValue
            && d.RequiredDate.Value.Date > DateTime.UtcNow.AddDays(30).Date)
        {
            // (P2-C acknowledged) Long-lead = required-date more than 30 days
            // out. Enum is named ProjectLongLeadApproved but the "approved"
            // semantics require a CipProject.Status check that doesn't ship
            // until Wave 4. For PR-14 this trigger is read as "long-lead
            // CANDIDATE"; the blocker ProjectOrContractNotApproved (also
            // deferred to Wave 4) is the gate that converts candidate →
            // approved. Document in §16-quirks until policy wiring lands.
            triggers.Add(AutoPoTrigger.ProjectLongLeadApproved);
        }

        // BomRevisionReleased / ChangeOrderAddedMaterial / ScrapReworkReplacement /
        // BuyerManualRequest — these need provenance signals not yet on the
        // demand record. Surface from explicit Notes prefix until provenance
        // fields ship in Wave 4. Cheap convention without forcing a schema
        // change for PR-14.
        if (!string.IsNullOrEmpty(d.Notes))
        {
            if (d.Notes.StartsWith("BomRevisionReleased:", StringComparison.OrdinalIgnoreCase))
                triggers.Add(AutoPoTrigger.BomRevisionReleased);
            if (d.Notes.StartsWith("ChangeOrder:", StringComparison.OrdinalIgnoreCase))
                triggers.Add(AutoPoTrigger.ChangeOrderAddedMaterial);
            if (d.Notes.StartsWith("ScrapReplacement:", StringComparison.OrdinalIgnoreCase)
                || d.Notes.StartsWith("ReworkReplacement:", StringComparison.OrdinalIgnoreCase))
                triggers.Add(AutoPoTrigger.ScrapReworkReplacement);
            if (d.Notes.StartsWith("BuyerManual:", StringComparison.OrdinalIgnoreCase))
                triggers.Add(AutoPoTrigger.BuyerManualRequest);
            if (d.Notes.StartsWith("TagPurchase:", StringComparison.OrdinalIgnoreCase))
                triggers.Add(AutoPoTrigger.ExplicitlyTaggedForPurchase);
        }

        // ─── BLOCKER rules (§16) ────────────────────────────────────────

        // (P2-D fix) PRO Status == OnHold maps to the more specific
        // RequiredOperationOnHold blocker. Otherwise the catch-all
        // "BOM line not released" proxy fires for any non-live PRO state.
        bool proOnHold = d.ProductionOrder != null
            && d.ProductionOrder.Status == ProductionOrderStatus.OnHold;
        if (proOnHold)
        {
            blockers.Add(AutoPoBlocker.RequiredOperationOnHold);
        }
        else if (d.ProductionOrder != null && !proIsLive)
        {
            blockers.Add(AutoPoBlocker.BomLineNotReleased);
        }

        if (string.IsNullOrEmpty(d.DrawingSpecRevision)
            && d.SourceType == DemandSourceType.BomLine
            && d.SupplyPolicy != SupplyPolicy.Floorstock)
        {
            blockers.Add(AutoPoBlocker.DrawingRevisionNotApproved);
        }

        // (P1-A fix) SupplierNotApproved fires whenever there's no resolved
        // vendor, regardless of SourceStatus. A demand without a VendorId
        // cannot have an approved supplier by definition.
        // (P2-A fix) When a vendor IS set, verify it's tenant-visible AND
        // active AND in Active status — not just "exists in the table".
        if (d.VendorId == null)
        {
            blockers.Add(AutoPoBlocker.SupplierNotApproved);
        }
        else
        {
            var vendorIsApproved = await _db.Set<Vendor>()
                .AsNoTracking()
                .AnyAsync(v => v.Id == d.VendorId.Value
                               && v.IsActive
                               && v.Status == VendorStatus.Active
                               && (v.CompanyId == null || visible.Contains(v.CompanyId.Value)),
                    ct);
            if (!vendorIsApproved) blockers.Add(AutoPoBlocker.SupplierNotApproved);
        }

        if (d.CustomerOwned)
            blockers.Add(AutoPoBlocker.CustomerOwnedMaterial);

        // (P1-C fix) "Inventory available + reservable" must mean the demand's
        // open shortfall (RemainingQuantity) is already covered — partial
        // reservation of a larger demand still has a shortfall that the
        // auto-PO engine should buy. Block ONLY when no shortfall remains.
        if (d.RemainingQuantity <= 0
            || d.SupplyStatus == DemandSupplyStatus.FullyFulfilled)
        {
            blockers.Add(AutoPoBlocker.InventoryAvailableReservable);
        }

        if (d.LinkedPurchaseOrderId.HasValue)
            blockers.Add(AutoPoBlocker.ExistingPoCanSatisfy);
        if (d.LinkedChildProductionOrderId.HasValue)
            blockers.Add(AutoPoBlocker.ExistingChildWoCanSatisfy);

        if (d.ItarOrExportControlled && d.VendorId == null)
            blockers.Add(AutoPoBlocker.ItarExportBlocksSupplier);

        if (d.BuyerActionState == BuyerActionState.AwaitingApproval)
            blockers.Add(AutoPoBlocker.BudgetApprovalPending);

        if (d.BuyerActionState == BuyerActionState.Blocked && !proOnHold)
            blockers.Add(AutoPoBlocker.RequiredOperationOnHold);

        // BelowApprovalThresholdNoRule + ProjectOrContractNotApproved need
        // company-policy + contract-status lookups not yet plumbed. Conservatively
        // skip in PR-14; Wave 4 polish wires the policy gate.

        // ─── DECISION ───────────────────────────────────────────────────

        AutoPoDecision decision;
        string summary;
        if (triggers.Count == 0)
        {
            decision = AutoPoDecision.NotEligible;
            summary = "No auto-PO trigger fires for this demand.";
        }
        else if (blockers.Count == 0)
        {
            decision = AutoPoDecision.Eligible;
            summary = $"{triggers.Count} trigger(s) fire, no blockers — safe to auto-create PO.";
        }
        else
        {
            decision = AutoPoDecision.BlockedReviewRequired;
            summary = $"{triggers.Count} trigger(s) but {blockers.Count} blocker(s) — manual review required.";
        }

        // ─── Suggested vendor (lightweight; PR-15 recommendation engine
        //     extends with full sourcing rule walk) ────────────────────
        int? suggestedVendorId = d.VendorId;
        string? suggestedVendorName = null;
        if (suggestedVendorId.HasValue)
        {
            // (P2-A fix) Tenant-scoped name lookup. A vendor from another
            // tenant should never leak into the suggestion UI.
            suggestedVendorName = await _db.Set<Vendor>()
                .AsNoTracking()
                .Where(v => v.Id == suggestedVendorId.Value
                            && (v.CompanyId == null || visible.Contains(v.CompanyId.Value)))
                .Select(v => v.Name)
                .FirstOrDefaultAsync(ct);
        }

        return Build(d, decision, triggers, blockers, summary, suggestedVendorId, suggestedVendorName);
    }

    private static AutoPoCandidate Build(
        ProductionSupplyDemand d,
        AutoPoDecision decision,
        IReadOnlyList<AutoPoTrigger> triggers,
        IReadOnlyList<AutoPoBlocker> blockers,
        string summary,
        int? suggestedVendorId,
        string? suggestedVendorName)
    {
        return new AutoPoCandidate(
            DemandId: d.Id,
            DemandNumber: d.DemandNumber,
            ProductionOrderId: d.ProductionOrderId,
            ProductionOrderNumber: d.ProductionOrder?.OrderNumber,
            BomLineId: d.BomLineId,
            OperationSequence: d.OperationSequence,
            PartNumber: d.PartNumber,
            RequiredQuantity: d.RequiredQuantity,
            RemainingQuantity: d.RemainingQuantity,
            RequiredDate: d.RequiredDate,
            SuggestedVendorId: suggestedVendorId,
            SuggestedVendorName: suggestedVendorName,
            Decision: decision,
            Triggers: triggers,
            Blockers: blockers,
            Summary: summary);
    }
}
