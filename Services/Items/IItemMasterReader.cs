// B6 Foundation Sprint PR-FS-7 (2026-05-26) — IItemMasterReader.
//
// Thin read-only projection service for the Item Master expansion fields.
// Used by the /Admin/ItemMasterExpansionProbe page to keep CHERRY025 compliance
// (no direct AppDbContext injection in PageModels). Returns the 18 PR-FS-7
// expansion columns + a few identity fields in a single projection.

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Items;

public interface IItemMasterReader
{
    /// <summary>
    /// Project the PR-FS-7 expansion fields for a single Item. Returns null
    /// if not found.
    /// </summary>
    Task<ItemMasterExpansionFields?> GetExpansionFieldsAsync(int itemId, CancellationToken ct);
}

public sealed record ItemMasterExpansionFields(
    int ItemId,
    string PartNumber,
    string Description,
    ItemType Type,
    ItemMasterSource Source,
    PlanningPolicy PlanningPolicy,
    MakeBuyCode MakeBuyCode,
    LotSizingRule LotSizingRule,
    string? MrpPlannerCode,
    bool IsSellable,
    bool IsPhantom,
    bool RequiresKitting,
    bool AS9100Critical,
    bool KeyCharacteristic,
    bool RequiresFai,
    int? InspectionPlanId,
    string? ECCN,
    string? ScheduleB,
    string? IntrastatCode,
    bool EAR99,
    decimal? FrozenStandardCost,
    DateTime? FrozenStandardCostEffectiveAtUtc,
    string? ItemFamily,
    LifecycleStage LifecycleStage);
