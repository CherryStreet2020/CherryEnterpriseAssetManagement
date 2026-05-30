// Theme B7 Wave C PR-9 (2026-05-29) — IMakeBuyFulfillmentService impl. Design in the interface file.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public sealed class MakeBuyFulfillmentService : IMakeBuyFulfillmentService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IMakeBuyDecisionService _decision;
        private readonly ILogger<MakeBuyFulfillmentService> _log;

        public MakeBuyFulfillmentService(AppDbContext db, ITenantContext tenant,
            IMakeBuyDecisionService decision, ILogger<MakeBuyFulfillmentService> log)
        {
            _db = db; _tenant = tenant; _decision = decision; _log = log;
        }

        public async Task<Result<MakeBuyFulfillmentResult>> FulfillDemandAsync(int demandId, CancellationToken ct = default)
        {
            var demand = await _db.ProductionSupplyDemands.FirstOrDefaultAsync(d => d.Id == demandId, ct);
            if (demand == null) return Result.Failure<MakeBuyFulfillmentResult>($"Demand #{demandId} not found.");
            if (!_tenant.VisibleCompanyIds.Contains(demand.CompanyId))
                return Result.Failure<MakeBuyFulfillmentResult>("Demand is not in your tenant scope.");
            if (demand.ItemId is not int itemId)
                return Result.Failure<MakeBuyFulfillmentResult>("Demand has no item to make-or-buy.");

            var qty = demand.RequiredQuantity > 0m ? demand.RequiredQuantity : demand.RemainingQuantity;
            if (qty <= 0m) return Result.Failure<MakeBuyFulfillmentResult>("Demand has no positive quantity to fulfill.");
            var due = demand.NeedByDate ?? demand.RequiredDate;

            // 1) Decide (persisted), then link the audit row to this demand.
            var decided = await _decision.DecideAsync(itemId, qty, due, demand.SiteId,
                MakeBuyDecisionContext.Mrp, persist: true, ct);
            if (decided.IsFailure) return Result.Failure<MakeBuyFulfillmentResult>(decided.Error);
            var d = decided.Value;
            if (d.PersistedDecisionId is int decId)
            {
                var row = await _db.MakeBuyDecisions.FirstOrDefaultAsync(x => x.Id == decId, ct);
                if (row != null) { row.SourceType = "ProductionSupplyDemand"; row.SourceId = demandId; }
            }

            // 2) Act on the verdict.
            int? childOrderId = null;
            string action;
            if (d.Outcome == MakeBuyOutcome.Buy)
            {
                demand.SupplyPolicy = SupplyPolicy.BuyDirectToJob;
                demand.BuyerActionState = BuyerActionState.Open;     // ready for buyer / Auto-PO
                demand.VendorId = d.ChosenSupplierId;                // hand the chosen supplier to the Auto-PO evaluator
                // If this demand was previously fulfilled as MAKE, clear the stale make link so it
                // doesn't look both made and bought.
                demand.LinkedChildProductionOrderId = null;
                demand.SupplyStatus = DemandSupplyStatus.NotSupplied;
                action = $"BUY — demand routed to procurement (SupplyPolicy=BuyDirectToJob, buyer action Open, vendor #{d.ChosenSupplierId}); " +
                         $"decision #{d.PersistedDecisionId}. Auto-PO/buyer picks it up.";
            }
            else
            {
                demand.SupplyPolicy = SupplyPolicy.MakeDirectToJob;
                demand.BuyerActionState = BuyerActionState.Resolved; // no procurement needed; a child job supplies it
                demand.VendorId = null;                              // not a buy

                // Idempotent: reuse the existing make-job if this demand was already fulfilled-to-make
                // (re-running MRP / pressing fulfill twice must not duplicate the order or trip the
                // (CompanyId, OrderNumber) unique index).
                var orderNumber = $"MK-{demand.DemandNumber}-{demandId}";
                var child = await _db.ProductionOrders.FirstOrDefaultAsync(
                    p => p.CompanyId == demand.CompanyId && p.OrderNumber == orderNumber, ct);
                bool reused = child != null;
                if (child == null)
                {
                    child = new ProductionOrder
                    {
                        CompanyId = demand.CompanyId, LocationId = demand.SiteId,
                        OrderNumber = orderNumber,
                        Title = $"Make-job for demand {demand.DemandNumber} (item #{itemId})",
                        Status = ProductionOrderStatus.Planned, QuantityOrdered = qty,
                        ItemId = itemId, ScheduledEnd = due,
                        ParentProductionOrderId = demand.ProductionOrderId == 0 ? null : demand.ProductionOrderId,
                    };
                    _db.ProductionOrders.Add(child);
                    await _db.SaveChangesAsync(ct);   // get child.Id before stamping the demand
                }
                childOrderId = child.Id;
                demand.LinkedChildProductionOrderId = child.Id;   // the supply-back link the integration exists to create
                demand.SupplyStatus = DemandSupplyStatus.Planned; // a supply record now exists
                action = $"MAKE — child production order #{child.Id} ({child.OrderNumber}) {(reused ? "reused" : "created")} to supply the demand " +
                         $"(SupplyPolicy=MakeDirectToJob); decision #{d.PersistedDecisionId}.";
            }

            demand.BuyerActionStateUpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("FulfillDemand {Demand}: {Outcome} (decision {Dec}) → policy {Policy}{Child}.",
                demandId, d.Outcome, d.PersistedDecisionId, demand.SupplyPolicy,
                childOrderId != null ? $", child PRO {childOrderId}" : "");

            return Result.Success(new MakeBuyFulfillmentResult(
                demandId, itemId, d.Outcome, d.PersistedDecisionId, demand.SupplyPolicy, childOrderId, action));
        }
    }
}
