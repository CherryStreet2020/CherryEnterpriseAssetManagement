using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-9 — RebateAgreement (customer back-end rebate contracts).
    //
    // Master Files Baseline cascade ship #7 of 10.
    //
    // BACK-END REBATES are NOT discounts — they're an accrual against the sale
    // that pays back to the customer based on hitting a volume threshold over
    // a period. Modeled separately from DiscountSchema because:
    //   (a) GL flow is different — rebates accrue as a liability during the
    //       period, then settle via credit memo / check / wire at period end
    //   (b) Period-level evaluation, not per-order
    //   (c) Tiered thresholds with growth-over-prior options (common in
    //       automotive / distribution contracts)
    //
    // CROSS-TENANT — operational data, CompanyId NOT NULL. No system templates
    // (every rebate is customer-specific by definition).
    //
    // GL ACCRUAL: during the period, each qualifying sale accrues a rebate
    // liability:  Dr SalesReturnsAllowances (or similar contra-revenue)
    //             Cr RebateAccrualLiability (AccrualGlAccountId)
    //
    // GL PAYOUT: at period end, when payout method executes:
    //             Dr RebateAccrualLiability
    //             Cr Cash / AccountsPayable / CustomerCredit
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.7
    //   - SAP SD rebate processing (LIS/rebate agreements VBO1)
    //   - Oracle Cloud Channel Revenue Management
    // =============================================================================
    [Table("RebateAgreements")]
    public class RebateAgreement
    {
        public int Id { get; set; }

        // Operational — never NULL.
        public int CompanyId { get; set; }

        // Stable code (e.g. "ACME-2026-ANNUAL", "DIST-Q2-VOLUME-GROWTH").
        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        // Customer this rebate is with. REQUIRED.
        public int CustomerId { get; set; }

        // ---------------------------------------------------------------------
        // EVALUATION BASIS — what does the rebate get computed against?
        // ---------------------------------------------------------------------
        public RebateBasis Basis { get; set; } = RebateBasis.PurchaseAmount;

        public RebatePeriod Period { get; set; } = RebatePeriod.Annual;

        // For Period=Custom — explicit start/end (otherwise computed from
        // EffectiveFromUtc + Period).
        public DateTime? CustomPeriodStartUtc { get; set; }
        public DateTime? CustomPeriodEndUtc { get; set; }

        // ---------------------------------------------------------------------
        // TIER SCHEDULE (JSONB).
        //
        // Single-tier example (simplest):
        //   [{"threshold": 100000, "pct": 0.02}]
        //
        // Multi-tier example (common in distribution):
        //   [{"threshold":  50000, "pct": 0.01},
        //    {"threshold": 100000, "pct": 0.02},
        //    {"threshold": 250000, "pct": 0.03},
        //    {"threshold": 500000, "pct": 0.04}]
        //
        // Growth-over-prior example:
        //   [{"growth_pct": 0.05, "rebate_pct": 0.01},
        //    {"growth_pct": 0.10, "rebate_pct": 0.025},
        //    {"growth_pct": 0.20, "rebate_pct": 0.05}]
        //
        // Flat-amount example:
        //   [{"threshold": 100000, "amount": 5000}]
        //
        // Service layer evaluates the tier at period end based on Basis.
        // ---------------------------------------------------------------------
        [Required]
        [Column(TypeName = "jsonb")]
        public string TiersJson { get; set; } = "[]";

        // ---------------------------------------------------------------------
        // PAYOUT.
        // ---------------------------------------------------------------------
        public RebatePayoutMethod PayoutMethod { get; set; } = RebatePayoutMethod.CreditMemo;

        // GL account where the accrual liability builds during the period.
        public int? AccrualGlAccountId { get; set; }

        // GL account where the payout JE flows when the period closes.
        public int? PayoutGlAccountId { get; set; }

        // Currency (FK to PRA-6).
        public int? CurrencyId { get; set; }

        // ---------------------------------------------------------------------
        // SCOPE (optional narrowing — by default applies to all customer purchases).
        // ---------------------------------------------------------------------

        // Restrict rebate to specific ItemGroup (e.g. only FG, not raw mats).
        public int? RestrictedToItemGroupId { get; set; }

        // Restrict to a specific price list (e.g. promotional list).
        public int? RestrictedToPriceListMasterId { get; set; }

        // ---------------------------------------------------------------------
        // EFFECTIVE-DATING + LIFECYCLE.
        // ---------------------------------------------------------------------
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        public DateTime? EffectiveToUtc { get; set; }

        public RebateStatus Status { get; set; } = RebateStatus.Draft;

        public bool IsActive { get; set; } = true;

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    public enum RebateBasis
    {
        PurchaseAmount = 0,         // $ of qualifying purchases (most common)
        PurchaseQuantity = 1,       // # of units purchased
        GrowthOverPrior = 2,        // % growth vs prior matched period
        NewProductAdoption = 3,     // qualifying purchases of new/strategic SKUs
        MarketDevelopmentFund = 4,  // co-op marketing rebate (separate accrual)
        Other = 99
    }

    public enum RebatePeriod
    {
        Monthly = 0,
        Quarterly = 1,
        SemiAnnual = 2,
        Annual = 3,
        Lifetime = 4,               // accrues forever — terminated by EffectiveToUtc
        Custom = 99                 // uses CustomPeriodStart/End
    }

    public enum RebatePayoutMethod
    {
        CreditMemo = 0,             // issue credit memo against future invoices (most common)
        Check = 1,                  // physical check
        WireTransfer = 2,           // wire / ACH
        CustomerCreditAccount = 3,  // apply to customer's standing credit balance
        NextOrderDiscount = 4,      // discount applied to first order after payout
        Other = 99
    }

    public enum RebateStatus
    {
        Draft = 0,                  // negotiated but not yet active
        Active = 1,                 // accruing
        Suspended = 2,              // paused (customer dispute, hold)
        Closed = 3,                 // period ended, awaiting reconciliation
        Reconciled = 4,             // paid out, settled
        Cancelled = 5,              // voided before settlement
        Other = 99
    }
}
