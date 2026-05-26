// B6 Foundation Sprint PR-FS-5 (2026-05-26) — ItemSourcingRuleService impl.
//
// Concurrency-safe (RowVersion + retry). Cascade-aware (per-Site over
// company-wide at same Priority). Allocation-split validation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Items;

public sealed class ItemSourcingRuleService : IItemSourcingRuleService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItemSourcingRuleService> _logger;

    public ItemSourcingRuleService(AppDbContext db, ILogger<ItemSourcingRuleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ItemSourcingRule>> GetActiveRulesAsync(
        int itemId,
        int? siteId,
        DateTime? asOfUtc,
        bool includeInactive,
        CancellationToken ct)
    {
        var asOf = asOfUtc ?? DateTime.UtcNow;
        var states = includeInactive
            ? new[]
                {
                    SourcingApprovalState.PendingApproval,
                    SourcingApprovalState.Approved,
                    SourcingApprovalState.Probation,
                    SourcingApprovalState.Suspended,
                    SourcingApprovalState.Disqualified,
                }
            : new[]
                {
                    SourcingApprovalState.Approved,
                    SourcingApprovalState.Probation,
                };

        var rules = await _db.ItemSourcingRules.AsNoTracking()
            .Where(r => r.ItemId == itemId)
            .Where(r => r.IsActive)
            .Where(r => states.Contains(r.ApprovalState))
            .Where(r => r.EffectiveFromUtc <= asOf)
            .Where(r => r.EffectiveToUtc == null || r.EffectiveToUtc > asOf)
            .Where(r => r.SiteId == null || r.SiteId == siteId)
            .ToListAsync(ct);

        // Cascade: when both a per-Site and a company-wide rule share the
        // same Priority, return the per-Site one first. Within Priority,
        // per-Site (SiteId != null) sorts before company-wide.
        return rules
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.SiteId.HasValue ? 0 : 1)
            .ThenBy(r => r.Id)
            .ToList();
    }

    public async Task<ItemSourcingRule?> GetPrimarySourceAsync(
        int itemId,
        int? siteId,
        DateTime? asOfUtc,
        CancellationToken ct)
    {
        var rules = await GetActiveRulesAsync(itemId, siteId, asOfUtc, includeInactive: false, ct);
        return rules.FirstOrDefault();
    }

    public async Task<ItemSourcingRule> AddRuleAsync(
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
        CancellationToken ct)
    {
        if (priority <= 0)
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be > 0.");

        if (allocationPercent.HasValue && (allocationPercent.Value <= 0m || allocationPercent.Value > 100m))
            throw new ArgumentOutOfRangeException(nameof(allocationPercent), "AllocationPercent must be in (0, 100].");

        if (isCustomerMandated && !customerId.HasValue)
            throw new ArgumentException("CustomerId is required when IsCustomerMandated=true (AS9100 §8.4.1).", nameof(customerId));

        switch (sourceMethod)
        {
            case SourceMethod.BuyFromVendor:
            case SourceMethod.Subcontract:
            case SourceMethod.DirectShipToCustomer:
                if (!vendorId.HasValue)
                    throw new ArgumentException($"VendorId is required for SourceMethod.{sourceMethod}.", nameof(vendorId));
                break;
            case SourceMethod.TransferFromSite:
                if (!transferFromSiteId.HasValue)
                    throw new ArgumentException("TransferFromSiteId is required for SourceMethod.TransferFromSite.", nameof(transferFromSiteId));
                break;
            case SourceMethod.MakeInternal:
                // No vendor/transfer needed.
                break;
        }

        // PR-FS-5 P1 fix (Codex on PR #361): service-side uniqueness enforcement
        // for (TenantId, ItemId, SiteId, VendorId, Priority) on active rules.
        // The DB partial unique indexes can't catch this because Postgres treats
        // NULL as distinct in unique indexes — SiteId NULL (company-wide) or
        // VendorId NULL (MakeInternal) would slip through the DB-level guard.
        // Service-side check uses explicit equality (NULL == NULL) on the read,
        // catches the duplicate before write.
        var duplicateExists = await _db.ItemSourcingRules
            .AnyAsync(r => r.ItemId == itemId
                        && r.SiteId == siteId
                        && r.VendorId == vendorId
                        && r.Priority == priority
                        && r.IsActive, ct);
        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"Active sourcing rule already exists for Item={itemId} Site={siteId?.ToString() ?? "(any)"} " +
                $"Vendor={vendorId?.ToString() ?? "(none)"} Priority={priority}. Suspend or deactivate " +
                $"the existing rule before adding a new one — or use a different Priority for the alternate.");
        }

        // Allocation-split validation: if AllocationPercent is set, sum across
        // currently-active rules at this same Priority must not exceed 100%
        // (with the new rule added). Service-enforced BEFORE write.
        if (allocationPercent.HasValue)
        {
            var existingAtPriority = await _db.ItemSourcingRules.AsNoTracking()
                .Where(r => r.ItemId == itemId
                         && r.SiteId == siteId
                         && r.Priority == priority
                         && r.IsActive
                         && r.AllocationPercent.HasValue)
                .SumAsync(r => r.AllocationPercent!.Value, ct);
            var prospectiveTotal = existingAtPriority + allocationPercent.Value;
            if (prospectiveTotal > 100m)
            {
                throw new InvalidOperationException(
                    $"Adding this rule would push the Priority={priority} allocation total to " +
                    $"{prospectiveTotal}% — must sum to ≤100%. Current sum: {existingAtPriority}%.");
            }
        }

        var now = DateTime.UtcNow;
        var rule = new ItemSourcingRule
        {
            ItemId = itemId,
            SiteId = siteId,
            VendorId = vendorId,
            TransferFromSiteId = transferFromSiteId,
            SourceMethod = sourceMethod,
            Priority = priority,
            AllocationPercent = allocationPercent,
            MinOrderQty = minOrderQty,
            MaxOrderQty = maxOrderQty,
            LeadTimeDaysOverride = leadTimeDaysOverride,
            ApprovalState = SourcingApprovalState.PendingApproval,
            IsCustomerMandated = isCustomerMandated,
            CustomerId = customerId,
            EffectiveFromUtc = now,
            EffectiveToUtc = null,
            IsActive = true,
            Notes = notes,
            CreatedAt = now,
            CreatedBy = createdBy,
        };
        _db.ItemSourcingRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ItemSourcingRuleService: added rule {RuleId} Item={ItemId} Site={SiteId} {SourceMethod} Priority={Priority} VendorId={VendorId} CustMandated={CustMandated}.",
            rule.Id, itemId, siteId, sourceMethod, priority, vendorId, isCustomerMandated);

        return rule;
    }

    public Task<ItemSourcingRule> ApproveRuleAsync(int ruleId, string approvedBy, CancellationToken ct) =>
        UpdateStateWithRetryAsync(ruleId, rule =>
        {
            rule.ApprovalState = SourcingApprovalState.Approved;
            rule.ApprovedAtUtc = DateTime.UtcNow;
            rule.ApprovedBy = approvedBy;
            rule.SuspendedAtUtc = null;
            rule.SuspensionReason = null;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = approvedBy;
        }, "Approve", ct);

    public Task<ItemSourcingRule> SuspendRuleAsync(int ruleId, string reason, string suspendedBy, CancellationToken ct) =>
        UpdateStateWithRetryAsync(ruleId, rule =>
        {
            // GO BIG: refuse routine suspension on customer-mandated AVL rules.
            // AS9100 §8.4.1 requires explicit customer-notification flow for
            // these — caller must use a dedicated SuspendCustomerMandatedAsync
            // entry point (not in this PR; tracked for Theme B8 PR-PO-7).
            if (rule.IsCustomerMandated)
            {
                throw new InvalidOperationException(
                    $"Cannot suspend customer-mandated AVL rule {rule.Id} via routine SuspendRuleAsync " +
                    $"(AS9100 §8.4.1). The customer (Id={rule.CustomerId}) must be notified through the " +
                    $"customer-AVL suspension flow.");
            }
            rule.ApprovalState = SourcingApprovalState.Suspended;
            rule.SuspendedAtUtc = DateTime.UtcNow;
            rule.SuspensionReason = reason;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = suspendedBy;
        }, "Suspend", ct);

    public Task<ItemSourcingRule> PutOnProbationAsync(int ruleId, string reason, string by, CancellationToken ct) =>
        UpdateStateWithRetryAsync(ruleId, rule =>
        {
            rule.ApprovalState = SourcingApprovalState.Probation;
            rule.SuspensionReason = reason;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = by;
        }, "Probation", ct);

    // ============================================================
    // Internal retry-on-concurrency-conflict helper.
    // ============================================================
    private async Task<ItemSourcingRule> UpdateStateWithRetryAsync(
        int ruleId,
        Action<ItemSourcingRule> mutate,
        string operation,
        CancellationToken ct)
    {
        const int maxRetries = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var rule = await _db.ItemSourcingRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct)
                    ?? throw new InvalidOperationException($"ItemSourcingRule {ruleId} not found.");

                mutate(rule);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "ItemSourcingRuleService: {Op} rule {RuleId} → ApprovalState={State}.",
                    operation, rule.Id, rule.ApprovalState);
                return rule;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    "ItemSourcingRuleService: concurrency conflict on {Op} rule {RuleId} attempt {Attempt}/{Max} — retrying.",
                    operation, ruleId, attempt, maxRetries);
                foreach (var entry in _db.ChangeTracker.Entries<ItemSourcingRule>().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }
}
