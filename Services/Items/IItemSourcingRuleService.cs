// B6 Foundation Sprint PR-FS-5 (2026-05-26) — IItemSourcingRuleService.
//
// Service for managing the Approved Vendor List + priority rules per
// (Item, optional Site). Drives MRP, Make-or-Buy decisions, material-issue
// routing, and AS9100 §8.4.1 supplier-approval audit trail.
//
// Per Lock 15 — IService surface only, never direct DbContext from callers.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Masters;

namespace Abs.FixedAssets.Services.Items;

public interface IItemSourcingRuleService
{
    /// <summary>
    /// Get all currently-effective rules for the (Item, Site) pair, ordered by
    /// Priority ASC (so Priority=1 first). Filters to Approved + Probation
    /// states by default — Suspended / Disqualified / PendingApproval are
    /// excluded unless <paramref name="includeInactive"/> is true.
    ///
    /// Cascade: per-Site rules (SiteId IS NOT NULL) are returned BEFORE
    /// company-wide rules (SiteId IS NULL) at the same Priority.
    /// </summary>
    Task<IReadOnlyList<ItemSourcingRule>> GetActiveRulesAsync(
        int itemId,
        int? siteId,
        DateTime? asOfUtc,
        bool includeInactive,
        CancellationToken ct);

    /// <summary>
    /// Convenience helper — returns the single highest-priority active
    /// (Approved or Probation) rule for the (Item, Site) pair, or null if
    /// no rule is available. Used by MRP / requisition flows to default the
    /// primary source.
    /// </summary>
    Task<ItemSourcingRule?> GetPrimarySourceAsync(
        int itemId,
        int? siteId,
        DateTime? asOfUtc,
        CancellationToken ct);

    /// <summary>
    /// Insert a new rule. Validation:
    ///   - Priority must be > 0.
    ///   - If AllocationPercent set, it must be in (0, 100].
    ///   - If multiple rules share the same Priority, their AllocationPercent
    ///     across the tier must sum to 100% (service checks BEFORE write).
    ///   - If IsCustomerMandated, CustomerId must be set.
    ///   - VendorId required for BuyFromVendor / Subcontract / DirectShipToCustomer.
    ///   - TransferFromSiteId required for TransferFromSite.
    /// New rules default to ApprovalState=PendingApproval.
    /// </summary>
    Task<ItemSourcingRule> AddRuleAsync(
        int itemId,
        int? siteId,
        int? vendorId,
        int? transferFromSiteId,
        SourceMethod sourceMethod,
        int priority,
        decimal? allocationPercent,
        decimal? minOrderQty,
        decimal? maxOrderQty,
        int? leadTimeDaysOverride,
        bool isCustomerMandated,
        int? customerId,
        string? notes,
        string? createdBy,
        CancellationToken ct);

    /// <summary>
    /// Flip ApprovalState=Approved. Stamps ApprovedAtUtc + ApprovedBy.
    /// Concurrency-safe via the RowVersion token on ItemSourcingRule.
    /// </summary>
    Task<ItemSourcingRule> ApproveRuleAsync(int ruleId, string approvedBy, CancellationToken ct);

    /// <summary>
    /// Flip ApprovalState=Suspended. Stamps SuspendedAtUtc + reason.
    /// REFUSES to suspend customer-mandated AVL rules — caller must use
    /// SuspendCustomerMandatedAsync (which requires explicit customer-
    /// notification context) for that flow. Concurrency-safe.
    /// </summary>
    Task<ItemSourcingRule> SuspendRuleAsync(int ruleId, string reason, string suspendedBy, CancellationToken ct);

    /// <summary>
    /// Flip ApprovalState=Probation. Concurrency-safe.
    /// </summary>
    Task<ItemSourcingRule> PutOnProbationAsync(int ruleId, string reason, string by, CancellationToken ct);
}
