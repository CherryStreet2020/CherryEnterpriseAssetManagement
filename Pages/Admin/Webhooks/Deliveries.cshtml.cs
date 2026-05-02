using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin.Webhooks;

[Authorize(Roles = "Admin")]
public class DeliveriesModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public DeliveriesModel(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public WebhookSubscription? Subscription { get; set; }
    public List<WebhookDeliveryLog> Deliveries { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        Subscription = await _db.WebhookSubscriptions
            .Include(s => s.Company)
            .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId) && s.Id == id)
            .FirstOrDefaultAsync();

        if (Subscription == null)
        {
            return NotFound();
        }

        Deliveries = await _db.WebhookDeliveryLogs
            .Where(l => l.WebhookSubscriptionId == id)
            .Include(l => l.OutboxEvent)
            .OrderByDescending(l => l.CreatedAt)
            .Take(50)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnGetPayloadAsync(int id)
    {
        var log = await _db.WebhookDeliveryLogs
            .Where(l => l.Id == id)
            .FirstOrDefaultAsync();
        if (log == null)
        {
            return NotFound();
        }
        return Content(log.PayloadSent ?? "{}", "application/json");
    }
}
