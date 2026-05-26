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

// B6 Foundation Sprint PR-FS-6 (2026-05-26) — admin probe for
// ICustomerItemXrefService. Service-only DI per CHERRY025.
[Authorize(Roles = "Admin")]
public sealed class CustomerItemXrefProbeModel : PageModel
{
    private readonly ICustomerItemXrefService _svc;
    private readonly ILogger<CustomerItemXrefProbeModel> _logger;

    public CustomerItemXrefProbeModel(ICustomerItemXrefService svc, ILogger<CustomerItemXrefProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public int CustomerId { get; set; }
    [BindProperty] public string CustomerPartNumber { get; set; } = string.Empty;
    [BindProperty] public string? CustomerRevision { get; set; }
    [BindProperty] public bool IncludeObsolete { get; set; }

    public IReadOnlyList<CustomerItemXref>? XrefsForItem { get; private set; }
    public CustomerItemXref? ResolvedByCustomerPn { get; private set; }
    public bool AttemptedByCustomerPn { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostByItemAsync(CancellationToken ct)
    {
        if (ItemId <= 0)
        {
            ModelState.AddModelError(nameof(ItemId), "ItemId must be > 0.");
            return Page();
        }

        XrefsForItem = await _svc.GetAllForItemAsync(ItemId, IncludeObsolete, ct);
        _logger.LogInformation(
            "CustomerItemXrefProbe ByItem: Item={ItemId} IncludeObsolete={Obs} Count={Count}",
            ItemId, IncludeObsolete, XrefsForItem.Count);
        return Page();
    }

    public async Task<IActionResult> OnPostByCustomerPnAsync(CancellationToken ct)
    {
        if (CustomerId <= 0 || string.IsNullOrWhiteSpace(CustomerPartNumber))
        {
            ModelState.AddModelError(string.Empty, "CustomerId and CustomerPartNumber are required.");
            return Page();
        }

        AttemptedByCustomerPn = true;
        var revision = string.IsNullOrWhiteSpace(CustomerRevision) ? null : CustomerRevision;
        ResolvedByCustomerPn = await _svc.ResolveByCustomerPnAsync(CustomerId, CustomerPartNumber, revision, asOfUtc: null, ct);
        _logger.LogInformation(
            "CustomerItemXrefProbe ByCustomerPn: Customer={CustId} PN='{PN}' Rev='{Rev}' Resolved={ResolvedId}",
            CustomerId, CustomerPartNumber, revision ?? "<null>", ResolvedByCustomerPn?.Id);
        return Page();
    }
}
