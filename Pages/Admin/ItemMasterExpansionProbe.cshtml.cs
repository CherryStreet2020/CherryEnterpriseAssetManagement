using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B6 Foundation Sprint PR-FS-7 (2026-05-26) — admin probe for the
// 18-column Item Master expansion. Service-only DI per CHERRY025 (no
// AppDbContext). Reads via:
//   - IItemMasterReader for the expansion-fields projection.
//   - IItemGroupResolver.ResolveDefaultForItemAsync(Item) for the
//     IsSellable-aware group resolution + .GetByIdAsync for the code.
[Authorize(Roles = "Admin")]
public sealed class ItemMasterExpansionProbeModel : PageModel
{
    private readonly IItemMasterReader _reader;
    private readonly IItemGroupResolver _groupResolver;
    private readonly ILogger<ItemMasterExpansionProbeModel> _logger;

    public ItemMasterExpansionProbeModel(
        IItemMasterReader reader,
        IItemGroupResolver groupResolver,
        ILogger<ItemMasterExpansionProbeModel> logger)
    {
        _reader = reader;
        _groupResolver = groupResolver;
        _logger = logger;
    }

    [BindProperty] public int ItemId { get; set; }

    public ItemMasterExpansionView? View { get; private set; }
    public bool NotFound { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ItemId <= 0)
        {
            ModelState.AddModelError(nameof(ItemId), "ItemId must be > 0.");
            return Page();
        }

        var fields = await _reader.GetExpansionFieldsAsync(ItemId, ct);
        if (fields == null)
        {
            NotFound = true;
            return Page();
        }

        // Re-resolve the group via the PR-FS-7-aware overload. Build a
        // throwaway Item for the resolver dispatch (only Type/Source/IsSellable
        // are read on that path).
        var probeItem = new Item
        {
            Id = fields.ItemId,
            PartNumber = fields.PartNumber,
            Type = fields.Type,
            Source = fields.Source,
            IsSellable = fields.IsSellable,
        };
        var groupId = await _groupResolver.ResolveDefaultForItemAsync(probeItem, ct);
        var (_, groupCode) = await _groupResolver.GetByIdAsync(groupId, ct);

        View = new ItemMasterExpansionView(
            ItemId: fields.ItemId,
            PartNumber: fields.PartNumber,
            Description: fields.Description,
            ResolvedItemGroupId: groupId,
            ResolvedItemGroupCode: groupCode,
            PlanningPolicy: fields.PlanningPolicy,
            MakeBuyCode: fields.MakeBuyCode,
            LotSizingRule: fields.LotSizingRule,
            MrpPlannerCode: fields.MrpPlannerCode,
            IsSellable: fields.IsSellable,
            IsPhantom: fields.IsPhantom,
            RequiresKitting: fields.RequiresKitting,
            AS9100Critical: fields.AS9100Critical,
            KeyCharacteristic: fields.KeyCharacteristic,
            RequiresFai: fields.RequiresFai,
            InspectionPlanId: fields.InspectionPlanId,
            ECCN: fields.ECCN,
            ScheduleB: fields.ScheduleB,
            IntrastatCode: fields.IntrastatCode,
            EAR99: fields.EAR99,
            FrozenStandardCost: fields.FrozenStandardCost,
            FrozenStandardCostEffectiveAtUtc: fields.FrozenStandardCostEffectiveAtUtc,
            ItemFamily: fields.ItemFamily,
            LifecycleStage: fields.LifecycleStage);

        _logger.LogInformation(
            "ItemMasterExpansionProbe: Item={ItemId} Group={GroupCode} IsSellable={IsSellable} PlanningPolicy={Policy} Lifecycle={Stage}",
            ItemId, groupCode, fields.IsSellable, fields.PlanningPolicy, fields.LifecycleStage);

        return Page();
    }
}

public sealed record ItemMasterExpansionView(
    int ItemId,
    string PartNumber,
    string Description,
    int? ResolvedItemGroupId,
    string? ResolvedItemGroupCode,
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
