// B6 Foundation Sprint PR-FS-3 (2026-05-26) — IItemStandardCostService.
//
// Service surface for the SAP Cost Component Split equivalent. Reads/writes
// ItemStandardCostElement rows with effective-date semantics. The Sprint 14.4
// cost-engine + variance service will consume this — for now it's the
// foundational data layer + read API + write API.
//
// Cascade semantics:
//   per-Site cost (SiteId IS NOT NULL)  →  Item-level cost (SiteId IS NULL)  →  $0
//
// As-of resolution: each request can pass an `asOf` DateTime to retrieve
// the historically-effective row. Default (null) = current (rows with
// EffectiveTo IS NULL OR EffectiveTo > now).
//
// Per Lock 15 — IService surface only, never direct DbContext usage from callers.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Masters;

namespace Abs.FixedAssets.Services.Items;

public interface IItemStandardCostService
{
    /// <summary>
    /// Get the full cost breakdown for an Item, optionally scoped to a Site
    /// and an as-of date. Cascade: per-Site cost wins over Item-level for the
    /// same ElementType + as-of window; missing element types default to 0.
    /// </summary>
    Task<ItemCostBreakdown?> GetCostBreakdownAsync(
        int itemId,
        int? siteId,
        DateTime? asOfUtc,
        CancellationToken ct);

    /// <summary>
    /// Insert a new cost element row, automatically closing any currently-
    /// effective row for the same (Item, Site, ElementType) by stamping its
    /// EffectiveToUtc. Idempotent: if an active row with the same
    /// (Item, Site, ElementType, Amount, Source) already exists, returns it
    /// without writing.
    /// </summary>
    Task<ItemStandardCostElement> SetCostElementAsync(
        int itemId,
        int? siteId,
        CostElementType elementType,
        decimal amount,
        CostElementSource source,
        string? calculationNotes,
        string? createdBy,
        CancellationToken ct);

    /// <summary>
    /// Raw rows for the (Item, optional Site) combination — used by admin
    /// surfaces that want to render the full history (active + superseded).
    /// </summary>
    Task<IReadOnlyList<ItemStandardCostElement>> GetHistoryAsync(
        int itemId,
        int? siteId,
        CancellationToken ct);
}

/// <summary>
/// Resolved cost breakdown for a given (Item, optional Site, as-of date).
/// Every element type has an entry — 0 if no row exists. <c>Total</c> is the
/// sum. <c>OverrideSource</c> tells the caller which element resolved from
/// the per-Site override vs. the Item-level row.
/// </summary>
public sealed record ItemCostBreakdown(
    int ItemId,
    int? SiteId,
    DateTime AsOfUtc,
    string CurrencyCode,
    decimal Material,
    decimal Labor,
    decimal VariableOverhead,
    decimal FixedOverhead,
    decimal Subcontract,
    decimal Setup,
    decimal Tooling,
    decimal Other,
    decimal Total,
    IReadOnlyDictionary<CostElementType, string> OverrideSource);
