// B6 Foundation Sprint PR-FS-5 (2026-05-26) — ItemSourcingRule entity.
//
// Multi-source Approved Vendor List (AVL) + priority ranking + customer-mandated
// flagging per (Item, optional Site). SAP S/4 "Source List" (ME01) + Oracle
// Approved Supplier List + D365 Approved Vendor List equivalent.
//
// Why this exists separate from ItemVendor:
//   - **ItemVendor** is the PRICING + LEAD-TIME relationship per (Item, Vendor)
//     — what we'd pay if we bought from them. That's vendor-side data.
//   - **ItemSourcingRule** is the SOURCING POLICY — for this Item at this Site,
//     in what order should we try sources, what's the approval state, is the
//     customer mandating the AVL, what's the allocation split if split-sourced.
//     That's planner-side data.
//   - Real-world: GE Aviation customer mandates a sole-source AVL on a precision
//     bearing for traceability. Internal planning prefers Grainger first because
//     of stock availability, but the customer-mandated rule overrides. The
//     ItemSourcingRule for that bearing has IsCustomerMandated=true and
//     ApprovalState=Approved on the customer-approved vendor only — all other
//     ItemVendor rows are visible for reference but inactive at the rule level.
//
// Used by:
//   - **Theme B7** Make-or-Buy decision service (Sprint 14.6+) — given an
//     Item + qty + dueDate, the decision service consults the sourcing rules to
//     enumerate approved external sources and compares the best external cost
//     against the internal make-cost.
//   - **Theme B8 PR-PO-3** Production Material Transaction service — at issue
//     time, "which vendor's stock should we draw from?" routes through the
//     sourcing rules + lot/heat constraints + customer-mandated AVL guards.
//   - **MRP** (Sprint 15) — primary source from rules drives Default Buyer +
//     Lead Time + Min Order Qty for the requisition.
//   - **AS9100 §8.4.1 / DCAA / IFRS 15** compliance — customer-mandated AVL
//     tracking + supplier approval state machine + effective-dating audit
//     trail.
//
// Tenant trio + null-safe partial UNIQUE + RowVersion concurrency token —
// applying ALL prior B6 Codex catches prophylactically from day one.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Models.Masters
{
    /// <summary>
    /// How the Item is sourced under this rule. Drives MRP, Make-or-Buy decision
    /// service input, and PostingProfile routing on issue / receipt.
    /// </summary>
    public enum SourceMethod
    {
        /// <summary>
        /// Make internally on a Production Order. Uses Item's internal Routing
        /// + BOM. No external vendor.
        /// </summary>
        MakeInternal = 0,

        /// <summary>
        /// Purchase from an external vendor (stock or made-to-order). The
        /// VendorId FK + AllocationPercent + LeadTimeDaysOverride apply.
        /// </summary>
        BuyFromVendor = 1,

        /// <summary>
        /// Subcontract operation — Item is owned by us but the conversion
        /// operation happens at the vendor. Charged via PRA-7 PostingProfile's
        /// Subcontract template.
        /// </summary>
        Subcontract = 2,

        /// <summary>
        /// Vendor ships directly to the end customer; inventory never touches
        /// our warehouse. Cost-engine flow: PO receipt → direct-to-COGS posting
        /// (per Theme B3, absorbed by B8 PR-PO-4).
        /// </summary>
        DirectShipToCustomer = 3,

        /// <summary>
        /// Inter-site transfer — fulfill from a sibling Site's inventory rather
        /// than buying or making. Cross-references the source-Site's CostLayer.
        /// </summary>
        TransferFromSite = 4,
    }

    /// <summary>
    /// Supplier approval state machine. Drives whether the rule is usable at
    /// runtime + the AS9100 §8.4.1 audit trail.
    /// </summary>
    public enum SourcingApprovalState
    {
        /// <summary>Awaiting first-article inspection / supplier audit / customer sign-off.</summary>
        PendingApproval = 0,

        /// <summary>Active and unrestricted.</summary>
        Approved = 1,

        /// <summary>
        /// Active but on quality probation — receipts require enhanced inspection
        /// (per the next ItemSourcingRule rule with this VendorId on probation,
        /// inspection routing kicks up to 100% AQL).
        /// </summary>
        Probation = 2,

        /// <summary>
        /// Suspended — cannot be used. Triggered by quality event, supplier
        /// audit failure, or customer disapproval. Reactivation requires explicit
        /// re-approval via the service.
        /// </summary>
        Suspended = 3,

        /// <summary>
        /// Permanently disqualified — vendor cannot be re-approved without a
        /// new supplier audit cycle. AS9100 audit-trail preservation.
        /// </summary>
        Disqualified = 4,
    }

    /// <summary>
    /// Per-(Item, optional Site, Vendor) ranked sourcing rule. Drives MRP +
    /// material-issue routing + Make-or-Buy decision input + AS9100 §8.4.1
    /// AVL traceability.
    /// </summary>
    public class ItemSourcingRule
    {
        public int Id { get; set; }

        // ===== Identity + tenant trio =====================================

        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        /// <summary>
        /// Optional per-Site scope. NULL = company-wide rule. Per-Site rules
        /// take precedence over company-wide for the same (Item, Vendor)
        /// combination — the service's GetActiveRulesAsync handles the cascade.
        /// </summary>
        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        /// <summary>
        /// Vendor for <see cref="SourceMethod.BuyFromVendor"/> /
        /// <see cref="SourceMethod.Subcontract"/> /
        /// <see cref="SourceMethod.DirectShipToCustomer"/>. NULL for
        /// <see cref="SourceMethod.MakeInternal"/> + <see cref="SourceMethod.TransferFromSite"/>
        /// (the latter uses <see cref="TransferFromSiteId"/> instead).
        /// </summary>
        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        /// <summary>
        /// For <see cref="SourceMethod.TransferFromSite"/> only — which sibling
        /// Site's inventory to draw from.
        /// </summary>
        public int? TransferFromSiteId { get; set; }

        public int? TenantId { get; set; }
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        // ===== Source method + ranking ===================================

        [Required]
        [Display(Name = "Source Method")]
        public SourceMethod SourceMethod { get; set; }

        /// <summary>
        /// Lower number = higher priority. Service orders by Priority ASC, so
        /// Priority=1 is the primary source. Two rules at the same priority
        /// indicate intentional split-sourcing (see AllocationPercent).
        /// </summary>
        [Required]
        [Display(Name = "Priority")]
        public int Priority { get; set; } = 1;

        /// <summary>
        /// For split-sourcing, the percentage of demand allocated to THIS rule.
        /// NULL = sole-source for its priority tier (100%). When multiple rules
        /// share the same Priority, AllocationPercent values across them must
        /// sum to 100% (service-enforced at write time).
        /// </summary>
        [Display(Name = "Allocation %")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AllocationPercent { get; set; }

        // ===== Per-rule overrides ========================================

        [Display(Name = "Min Order Qty (per-rule override)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MinOrderQty { get; set; }

        [Display(Name = "Max Order Qty")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MaxOrderQty { get; set; }

        /// <summary>
        /// Lead time in days from this source. NULL = use the Item-level or
        /// ItemVendor-level lead time.
        /// </summary>
        [Display(Name = "Lead Time Days (per-rule override)")]
        public int? LeadTimeDaysOverride { get; set; }

        // ===== Approval + AS9100 / DCAA =====================================

        [Required]
        [Display(Name = "Approval State")]
        public SourcingApprovalState ApprovalState { get; set; } = SourcingApprovalState.PendingApproval;

        /// <summary>
        /// AS9100 §8.4.1 customer-approved AVL flag. When TRUE, the rule cannot
        /// be deactivated without customer approval, and the service refuses
        /// suspension via routine quality flag — must go through explicit
        /// customer-notification flow (PR-PO-7 inspection-hold pattern).
        /// </summary>
        [Display(Name = "Customer Mandated AVL")]
        public bool IsCustomerMandated { get; set; }

        /// <summary>
        /// Required when <see cref="IsCustomerMandated"/> is TRUE — the customer
        /// that mandated this AVL entry.
        /// </summary>
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Display(Name = "Approved At")]
        public DateTime? ApprovedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Suspended At")]
        public DateTime? SuspendedAtUtc { get; set; }

        [StringLength(500)]
        [Display(Name = "Suspension Reason")]
        public string? SuspensionReason { get; set; }

        // ===== Effective-dating =============================================

        [Required]
        [Display(Name = "Effective From")]
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        [Display(Name = "Effective To")]
        public DateTime? EffectiveToUtc { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        // ===== Concurrency token ============================================
        //
        // PR-FS-4 lesson applied prophylactically (Codex P1 on PR #360).
        // ApprovalState transitions are concurrent-write hot paths — two
        // operators flipping Suspended ↔ Approved at the same time would lose
        // an update. RowVersion + retry in service guards against it.

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // ===== Audit ========================================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
