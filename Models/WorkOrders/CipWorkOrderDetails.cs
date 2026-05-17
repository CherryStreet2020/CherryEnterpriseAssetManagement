using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.8 — CipWorkOrderDetails satellite.
    //
    // CIP (Capital Improvement Project) is the "operator name" for a
    // capital build whose accumulated cost is carried in the ledger
    // as ASC 360-10 Construction in Progress until substantial
    // completion. This satellite holds the fields that ONLY exist for
    // Classification=CIP work orders, so the unified WorkOrder header
    // doesn't carry CIP-only columns that would always be NULL for
    // Maintenance / Quality / Engineering / HSE work.
    //
    // Relationship: 1:0..1 with WorkOrder.
    //   - WorkOrderId is both FK and UNIQUE — exactly one CIP details
    //     row per WO, but only for CIP-classified WOs.
    //   - Nav property on WorkOrder side (`CipDetails`) is null for any
    //     non-CIP WO; nav property here (`WorkOrder`) is required.
    //   - ON DELETE CASCADE because details are owned by the WO.
    //
    // Field sourcing:
    //   - AfeNumber: industry-standard AFE (Authorization For Expenditure)
    //     identifier. Required for tracking and reconciling against
    //     ChartOfAccounts.GlCipSubAccount.
    //   - ApprovedBudget vs final actuals: drives variance reporting.
    //   - CapitalizedInterest: per ASC 835-20, qualifying borrowing
    //     costs may be capitalized into the CIP balance until the
    //     asset is ready for intended use.
    //   - SubstantialCompletionDate / InServiceDate: the trigger for
    //     transferring CIP balance → fixed asset register and starting
    //     depreciation. ASC 360-10 says depreciation begins when the
    //     asset is "available for its intended use," not on completion
    //     accounting completion.
    //   - DepreciationMethod + UsefulLifeMonths: predetermined here so
    //     the capitalization workflow (existing CipCapitalizationCost
    //     pipeline) can hydrate the destination Asset with the
    //     correct depreciation plan in one step.
    //   - TargetFixedAssetId: nullable until capitalization fires.
    //     Once set, depreciation snapshots reference it.
    //   - Stage: tracks the operator-side milestone funnel. Distinct
    //     from WorkOrder.Status (which is the workflow state). Stage
    //     is "where is the project in its construction lifecycle"; Status
    //     is "what's the workflow gate state."
    //   - ChangeOrderCount: denormalized for at-a-glance reporting.
    //     Maintained by the change-order service (deferred to Phase F).
    //   - RetainagePercent: industry standard 5–10% withheld from each
    //     contractor invoice until substantial completion.
    //   - JvPartnerSplits: jsonb array of `{partnerId, sharePercent}`.
    //     Used by the cost-allocation engine when a CIP is co-funded.
    //     Approval chain (WorkOrderApproval) gets +1 Partner-* stage
    //     per partner entry.
    //   - RegulatoryAuthority: short code for the regulator that
    //     governs this CIP (FERC, PUC-CA, AER, etc.). Drives which
    //     report templates the closeout workflow emits.
    //
    // Source standards:
    //   - ASC 360-10 (Property, Plant, and Equipment)
    //   - ASC 835-20 (Capitalization of Interest)
    //   - AFE industry practice (Finario AFE module, Plant Services
    //     CMMS conventions)
    [Table("CipWorkOrderDetails")]
    public class CipWorkOrderDetails
    {
        public int Id { get; set; }

        // FK to WorkOrder. UNIQUE — one CIP details row per WO.
        public int WorkOrderId { get; set; }

        // AFE identifier. Stable across budget revisions of the same
        // project; revised CIPs use the Revision column on the parent
        // WorkOrder (PR #119.6) rather than minting a new AFE.
        [Required, StringLength(32)]
        public string AfeNumber { get; set; } = string.Empty;

        // GL sub-account where this project's CIP costs accumulate.
        // String for now; future PR upgrades to FK against ChartOfAccounts
        // once the chart of accounts is normalized.
        [StringLength(32)]
        public string? GlCipSubAccount { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal? ApprovedBudget { get; set; }

        // ASC 835-20 qualifying borrowing costs capitalized into CIP
        // balance. Cumulative — incremented by the interest-cap process
        // each period until substantial completion.
        [Column(TypeName = "numeric(18,2)")]
        public decimal? CapitalizedInterest { get; set; }

        // Substantial completion = the date the asset is available for
        // intended use. Triggers depreciation start under ASC 360-10
        // even if final closeout accounting hasn't finished.
        public DateTime? SubstantialCompletionDate { get; set; }

        // In-service = the date the asset was placed in operating use.
        // Usually the same as SubstantialCompletionDate but can lag
        // (e.g., asset built but not yet installed).
        public DateTime? InServiceDate { get; set; }

        public int? UsefulLifeMonths { get; set; }

        // Depreciation method to apply once capitalized.
        public CipDepreciationMethod DepreciationMethod { get; set; } =
            CipDepreciationMethod.StraightLine;

        // Once the CIP capitalizes, this points to the Asset row that
        // received the cost basis. Until then, NULL.
        public int? TargetFixedAssetId { get; set; }

        // Project-lifecycle stage. Distinct from WorkOrder.Status
        // (which is the workflow gate).
        public CipStage Stage { get; set; } = CipStage.Feasibility;

        // Denormalized count of change orders raised against this CIP.
        // Maintained by the change-order service (Phase F).
        public int ChangeOrderCount { get; set; }

        // Retainage withheld from contractor invoices until substantial
        // completion. Industry standard 5-10%.
        [Column(TypeName = "numeric(5,2)")]
        public decimal? RetainagePercent { get; set; }

        // jsonb: array of `{partnerId, sharePercent}` entries when this
        // CIP is co-funded by joint venture partners. The cost-allocation
        // engine reads this to split each posted cost across partners,
        // and the approval-chain seeder adds one Partner-* stage per entry.
        [Column(TypeName = "jsonb")]
        public string? JvPartnerSplits { get; set; }

        // Short code for the governing regulator (FERC, PUC-CA, AER…).
        // Drives the closeout-report template selection.
        [StringLength(40)]
        public string? RegulatoryAuthority { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    // ASC 360-10 / IRS Pub. 946 depreciation methods. MACRS is US-only;
    // the catalog supports it for accounting consumers but the CIP UI
    // defaults to StraightLine.
    public enum CipDepreciationMethod
    {
        StraightLine = 0,
        DoubleDecliningBalance = 1,
        UnitsOfProduction = 2,
        Macrs = 3,
    }

    // Operator-side project milestones. Distinct from WorkOrder.Status
    // (workflow gate state). The order matches typical capital-project
    // gate review names.
    public enum CipStage
    {
        Feasibility = 0,
        FrontEndEngineeringDesign = 1,   // FEED
        Approved = 2,
        Design = 3,
        Procurement = 4,
        Construction = 5,
        Commissioning = 6,
        SubstantialComplete = 7,
        Closeout = 8,
    }
}
