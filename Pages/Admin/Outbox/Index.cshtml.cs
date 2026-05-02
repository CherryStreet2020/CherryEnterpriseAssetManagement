using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin.Outbox;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ITenantContext _tenantContext;

    public IndexModel(AppDbContext db, IWebHostEnvironment env, ITenantContext tenantContext)
    {
        _db = db;
        _env = env;
        _tenantContext = tenantContext;
    }

    public List<OutboxEvent> Events { get; set; } = new();
    public string CurrentTab { get; set; } = "all";
    public bool IsLabEnvironment => _env.IsDevelopment();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public int PendingCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int DeadLetterCount { get; set; }

    public async Task<IActionResult> OnGetAsync(string? tab)
    {
        if (!_env.IsDevelopment())
        {
            return Content("Outbox console is only available in Development/LAB environment.");
        }

        CurrentTab = tab ?? "all";
        await LoadCountsAsync();
        await LoadEventsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRetryNowAsync(int id)
    {
        if (!_env.IsDevelopment())
        {
            return Content("Operation not allowed outside Development environment.");
        }

        var evt = await _db.OutboxEvents
            .Where(e => _tenantContext.VisibleCompanyIds.Contains(e.CompanyId) && e.Id == id)
            .FirstOrDefaultAsync();
        if (evt != null)
        {
            evt.Status = OutboxEventStatus.Pending;
            evt.NextAttemptAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            SuccessMessage = $"Event {id} queued for immediate retry.";
        }

        await LoadCountsAsync();
        await LoadEventsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostReplayAsync(int id)
    {
        if (!_env.IsDevelopment())
        {
            return Content("Operation not allowed outside Development environment.");
        }

        var original = await _db.OutboxEvents
            .Where(e => _tenantContext.VisibleCompanyIds.Contains(e.CompanyId) && e.Id == id)
            .FirstOrDefaultAsync();
        if (original != null)
        {
            var clone = new OutboxEvent
            {
                CompanyId = original.CompanyId,
                SiteId = original.SiteId,
                EventType = original.EventType,
                EntityType = original.EntityType,
                EntityId = original.EntityId,
                PayloadJson = original.PayloadJson,
                OccurredAt = DateTime.UtcNow,
                Status = OutboxEventStatus.Pending,
                AttemptCount = 0,
                NextAttemptAt = DateTime.UtcNow,
                CorrelationId = original.CorrelationId
            };
            _db.OutboxEvents.Add(clone);
            await _db.SaveChangesAsync();
            SuccessMessage = $"Event {id} replayed as new event {clone.Id}.";
        }

        await LoadCountsAsync();
        await LoadEventsAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetPayloadAsync(int id)
    {
        if (!_env.IsDevelopment())
        {
            return Content("Operation not allowed outside Development environment.");
        }

        var evt = await _db.OutboxEvents
            .Where(e => _tenantContext.VisibleCompanyIds.Contains(e.CompanyId) && e.Id == id)
            .FirstOrDefaultAsync();
        if (evt == null)
        {
            return NotFound();
        }
        return Content(evt.PayloadJson, "application/json");
    }

    private async Task LoadCountsAsync()
    {
        PendingCount = await _db.OutboxEvents.CountAsync(e => e.Status == OutboxEventStatus.Pending);
        SentCount = await _db.OutboxEvents.CountAsync(e => e.Status == OutboxEventStatus.Sent);
        FailedCount = await _db.OutboxEvents.CountAsync(e => e.Status == OutboxEventStatus.Failed);
        DeadLetterCount = await _db.OutboxEvents.CountAsync(e => e.Status == OutboxEventStatus.DeadLetter);
    }

    private async Task LoadEventsAsync()
    {
        IQueryable<OutboxEvent> query = _db.OutboxEvents
            .Include(e => e.Company)
            .OrderByDescending(e => e.OccurredAt);

        Events = CurrentTab switch
        {
            "pending" => await query.Where(e => e.Status == OutboxEventStatus.Pending).Take(100).ToListAsync(),
            "sent" => await query.Where(e => e.Status == OutboxEventStatus.Sent).Take(100).ToListAsync(),
            "failed" => await query.Where(e => e.Status == OutboxEventStatus.Failed).Take(100).ToListAsync(),
            "deadletter" => await query.Where(e => e.Status == OutboxEventStatus.DeadLetter).Take(100).ToListAsync(),
            _ => await query.Take(100).ToListAsync()
        };
    }
}
