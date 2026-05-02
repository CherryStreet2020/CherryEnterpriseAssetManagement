using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Webhooks;
using System.Security.Cryptography;

namespace Abs.FixedAssets.Pages.Admin.Webhooks;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IOutboxWriter _outbox;
    private readonly ITenantContext _tenantContext;

    public IndexModel(AppDbContext db, IWebHostEnvironment env, IOutboxWriter outbox, ITenantContext tenantContext)
    {
        _db = db;
        _env = env;
        _outbox = outbox;
        _tenantContext = tenantContext;
    }

    public List<WebhookSubscription> Subscriptions { get; set; } = new();
    public bool IsDevelopment { get; set; }
    public string? NewSecret { get; set; }
    public int? NewSubscriptionId { get; set; }

    [BindProperty]
    public WebhookFormModel Input { get; set; } = new();

    public class WebhookFormModel
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public List<string> EventTypes { get; set; } = new();
    }

    public async Task OnGetAsync()
    {
        IsDevelopment = _env.IsDevelopment();
        Subscriptions = await _db.WebhookSubscriptions
            .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
            .Include(s => s.DeliveryLogs!.OrderByDescending(l => l.CreatedAt).Take(5))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrEmpty(Input.Name) || string.IsNullOrEmpty(Input.Url))
        {
            TempData["Error"] = "Name and URL are required";
            return RedirectToPage();
        }

        var secret = GenerateSecret();
        var subscription = new WebhookSubscription
        {
            CompanyId = _tenantContext.CompanyId ?? 1,
            Name = Input.Name,
            Url = Input.Url,
            IsActive = Input.IsActive,
            Secret = secret,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system"
        };
        subscription.SetEventTypes(Input.EventTypes);

        _db.WebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        TempData["NewSecret"] = secret;
        TempData["NewSubscriptionId"] = subscription.Id;
        TempData["Success"] = $"Webhook '{Input.Name}' created. Copy the secret now - it won't be shown again!";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (Input.Id == null)
        {
            TempData["Error"] = "Invalid subscription ID";
            return RedirectToPage();
        }

        var subscription = await _db.WebhookSubscriptions
            .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId) && s.Id == Input.Id)
            .FirstOrDefaultAsync();
        if (subscription == null)
        {
            TempData["Error"] = "Subscription not found";
            return RedirectToPage();
        }

        subscription.Name = Input.Name;
        subscription.Url = Input.Url;
        subscription.IsActive = Input.IsActive;
        subscription.SetEventTypes(Input.EventTypes);

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Webhook '{Input.Name}' updated";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRegenerateSecretAsync(int id)
    {
        var subscription = await _db.WebhookSubscriptions
            .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId) && s.Id == id)
            .FirstOrDefaultAsync();
        if (subscription == null)
        {
            TempData["Error"] = "Subscription not found";
            return RedirectToPage();
        }

        var newSecret = GenerateSecret();
        subscription.Secret = newSecret;
        await _db.SaveChangesAsync();

        TempData["NewSecret"] = newSecret;
        TempData["NewSubscriptionId"] = id;
        TempData["Success"] = $"Secret regenerated for '{subscription.Name}'. Copy it now - it won't be shown again!";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var subscription = await _db.WebhookSubscriptions
            .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId) && s.Id == id)
            .FirstOrDefaultAsync();
        if (subscription == null)
        {
            TempData["Error"] = "Subscription not found";
            return RedirectToPage();
        }

        _db.WebhookSubscriptions.Remove(subscription);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Webhook '{subscription.Name}' deleted";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendTestEventAsync(int id)
    {
        if (!_env.IsDevelopment())
        {
            TempData["Error"] = "Test events can only be sent in LAB environment";
            return RedirectToPage();
        }

        var subscription = await _db.WebhookSubscriptions
            .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId) && s.Id == id)
            .FirstOrDefaultAsync();
        if (subscription == null)
        {
            TempData["Error"] = "Subscription not found";
            return RedirectToPage();
        }

        await _outbox.EnqueueAsync(
            subscription.CompanyId,
            null,
            "test.ping",
            "TestEvent",
            Guid.NewGuid().ToString("N"),
            new
            {
                Message = "This is a test webhook event from CherryAI EAM",
                Timestamp = DateTime.UtcNow,
                SubscriptionId = subscription.Id,
                SubscriptionName = subscription.Name
            },
            $"test-{DateTime.UtcNow.Ticks}"
        );

        TempData["Success"] = $"Test event queued for '{subscription.Name}'";
        return RedirectToPage();
    }

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
