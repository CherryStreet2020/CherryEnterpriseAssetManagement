using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B6 Foundation Sprint PR-FS-5 (2026-05-26) — admin probe for
// IItemSourcingRuleService. Service-only DI per ADR-025 / CHERRY025
// (lesson from PR-FS-4).
[Authorize(Roles = "Admin")]
public sealed class ItemSourcingProbeModel : PageModel
{
    private readonly IItemSourcingRuleService _svc;
    private readonly ILogger<ItemSourcingProbeModel> _logger;

    public ItemSourcingProbeModel(IItemSourcingRuleService svc, ILogger<ItemSourcingProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public int? SiteId { get; set; }
    [BindProperty] public bool IncludeInactive { get; set; }

    public IReadOnlyList<ItemSourcingRule>? Rules { get; private set; }
    public ItemSourcingRule? Primary { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ItemId <= 0)
        {
            ModelState.AddModelError(nameof(ItemId), "ItemId must be > 0.");
            return Page();
        }

        Rules = await _svc.GetActiveRulesAsync(ItemId, SiteId, asOfUtc: null, includeInactive: IncludeInactive, ct);
        Primary = await _svc.GetPrimarySourceAsync(ItemId, SiteId, asOfUtc: null, ct);

        _logger.LogInformation(
            "ItemSourcingProbe: Item={ItemId} Site={SiteId} IncludeInactive={Incl} RuleCount={Count} PrimaryId={PrimaryId}",
            ItemId, SiteId, IncludeInactive, Rules.Count, Primary?.Id);

        return Page();
    }
}
