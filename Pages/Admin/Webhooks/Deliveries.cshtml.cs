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
    public List<WebhookSubscription> Subscriptions { get; set; } = new();
    public bool IsAggregateView => Subscription == null;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        // No id → aggregate view across every subscription this tenant
        // can see. Closes DEF-001 from the 2026-05-08 E2E run report
        // (the page used to 404 when reached without an id, despite
        // being linked from the global nav).
        if (id == null)
        {
            Subscriptions = await _db.WebhookSubscriptions
                .Include(s => s.Company)
                .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
                .OrderBy(s => s.Name)
                .ToListAsync();

            var subIds = Subscriptions.Select(s => s.Id).ToList();
            Deliveries = await _db.WebhookDeliveryLogs
                .Where(l => subIds.Contains(l.WebhookSubscriptionId))
                .Include(l => l.OutboxEvent)
                .Include(l => l.WebhookSubscription)
                .OrderByDescending(l => l.CreatedAt)
                .Take(100)
                .ToListAsync();

            return Page();
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
