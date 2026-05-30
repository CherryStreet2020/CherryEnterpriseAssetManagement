// Theme B7 Wave C PR-9 (2026-05-29) — IMakeBuyFulfillmentService (CLOSES Wave C).
//
// The integration that makes the make-or-buy DECISION (PR-8) actually DO something.
// Given a ProductionSupplyDemand for a MakeOrBuy item, it runs DecideAsync and acts on
// the verdict:
//   • BUY  → set the demand's SupplyPolicy to BuyDirectToJob + BuyerActionState Open, so
//            the buyer / Auto-PO engine picks it up. (Subcontract ops would route via
//            SubcontractOperation — out of scope here.)
//   • MAKE → set SupplyPolicy to MakeDirectToJob and create the child production order
//            that will supply the demand, parented to the demand's order.
// Either way the MakeBuyDecision is stamped against the demand (SourceType/SourceId) for
// end-to-end traceability. Tenant scope: the demand's company.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    public sealed record MakeBuyFulfillmentResult(
        int DemandId,
        int ItemId,
        MakeBuyOutcome Outcome,
        int? DecisionId,
        SupplyPolicy AppliedSupplyPolicy,
        int? CreatedChildOrderId,
        string Action);

    public interface IMakeBuyFulfillmentService
    {
        /// <summary>
        /// Decide make-or-buy for a demand and apply the verdict to the supply chain: BUY routes
        /// the demand to procurement; MAKE creates a child production order. Persists + links the
        /// decision to the demand.
        /// </summary>
        Task<Result<MakeBuyFulfillmentResult>> FulfillDemandAsync(int demandId, CancellationToken ct = default);
    }
}
