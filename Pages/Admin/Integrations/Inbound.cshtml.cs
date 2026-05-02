using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.Integrations;

public class InboundModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ITenantContext _tenantContext;

    public InboundModel(AppDbContext db, IWebHostEnvironment env, ITenantContext tenantContext)
    {
        _db = db;
        _env = env;
        _tenantContext = tenantContext;
    }

    public List<InboundEvent> Events { get; set; } = new();
    public string CurrentTab { get; set; } = "pending";
    public int PendingCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public int DeadLetterCount { get; set; }
    public bool IsLabEnvironment => _env.IsDevelopment();
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(string? tab)
    {
        if (!IsLabEnvironment) return;

        CurrentTab = tab ?? "pending";

        PendingCount = await _db.InboundEvents.CountAsync(e => e.Status == InboundEventStatus.Pending);
        ProcessedCount = await _db.InboundEvents.CountAsync(e => e.Status == InboundEventStatus.Processed);
        FailedCount = await _db.InboundEvents.CountAsync(e => e.Status == InboundEventStatus.Failed);
        DeadLetterCount = await _db.InboundEvents.CountAsync(e => e.Status == InboundEventStatus.DeadLetter);

        var query = _db.InboundEvents.Include(e => e.IntegrationEndpoint).AsQueryable();

        query = CurrentTab switch
        {
            "processed" => query.Where(e => e.Status == InboundEventStatus.Processed),
            "failed" => query.Where(e => e.Status == InboundEventStatus.Failed),
            "deadletter" => query.Where(e => e.Status == InboundEventStatus.DeadLetter),
            _ => query.Where(e => e.Status == InboundEventStatus.Pending)
        };

        Events = await query
            .OrderByDescending(e => e.ReceivedAt)
            .Take(50)
            .ToListAsync();

        if (TempData.ContainsKey("SuccessMessage"))
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
    }

    public async Task<IActionResult> OnPostRetryAsync(int eventId, string? tab)
    {
        if (!IsLabEnvironment) return RedirectToPage();

        var evt = await _db.InboundEvents
            .Where(e => (e.TenantId == _tenantContext.TenantId || e.TenantId == null) && e.Id == eventId)
            .FirstOrDefaultAsync();
        if (evt != null && (evt.Status == InboundEventStatus.Failed || evt.Status == InboundEventStatus.DeadLetter))
        {
            evt.Status = InboundEventStatus.Pending;
            evt.AttemptCount = 0;
            evt.NextAttemptAt = null;
            evt.LastError = null;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Event {eventId} queued for retry.";
        }

        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostReplayAsync(int eventId, string? tab)
    {
        if (!IsLabEnvironment) return RedirectToPage();

        var evt = await _db.InboundEvents
            .Where(e => (e.TenantId == _tenantContext.TenantId || e.TenantId == null) && e.Id == eventId)
            .FirstOrDefaultAsync();
        if (evt != null && evt.Status == InboundEventStatus.Processed)
        {
            evt.Status = InboundEventStatus.Pending;
            evt.AttemptCount = 0;
            evt.NextAttemptAt = null;
            evt.ProcessedAt = null;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Event {eventId} queued for replay.";
        }

        return RedirectToPage(new { tab });
    }
}
