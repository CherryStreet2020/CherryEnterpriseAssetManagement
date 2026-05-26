using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B6 Foundation Sprint PR-FS-2 (2026-05-26) — admin probe for IItemSiteResolver.
// Read-only diagnostic — no writes.
[Authorize(Roles = "Admin")]
public sealed class ItemSiteProbeModel : PageModel
{
    private readonly IItemSiteResolver _resolver;
    private readonly ILogger<ItemSiteProbeModel> _logger;

    public ItemSiteProbeModel(IItemSiteResolver resolver, ILogger<ItemSiteProbeModel> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public int? SiteId { get; set; }

    public ItemEffective? Effective { get; private set; }
    public bool NotFound { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ItemId <= 0)
        {
            ModelState.AddModelError(nameof(ItemId), "ItemId must be > 0.");
            return Page();
        }

        Effective = await _resolver.ResolveEffectiveAsync(ItemId, SiteId, ct);
        NotFound = Effective is null;

        _logger.LogInformation(
            "ItemSiteProbe: ItemId={ItemId}, SiteId={SiteId}, NotFound={NotFound}",
            ItemId, SiteId, NotFound);

        return Page();
    }
}
