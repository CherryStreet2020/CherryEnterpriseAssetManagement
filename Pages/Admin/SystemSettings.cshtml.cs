using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class SystemSettingsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public SystemSettingsModel(AppDbContext context, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<PeriodLock> LockedPeriods { get; set; } = new();
        public List<SelectListItem> RetentionOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }

        public async Task OnGetAsync()
        {
            RetentionOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RetentionPeriod", null, "");
            LockedPeriods = await _context.PeriodLocks
                .OrderByDescending(p => p.Period)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostLockPeriodAsync(int period)
        {
            var existing = await _context.PeriodLocks
                .FirstOrDefaultAsync(p => p.Period == period);

            if (existing == null)
            {
                _context.PeriodLocks.Add(new PeriodLock
                {
                    Period = period,
                    LockedAt = DateTime.UtcNow,
                    LockedBy = User.Identity?.Name,
                    IsLocked = true
                });
                await _context.SaveChangesAsync();
                SuccessMessage = $"Period {period} has been locked.";
            }

            LockedPeriods = await _context.PeriodLocks.OrderByDescending(p => p.Period).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUnlockPeriodAsync(int id)
        {
            var periodLock = await _context.PeriodLocks
                .Where(p => p.Id == id)
                .FirstOrDefaultAsync();
            if (periodLock != null)
            {
                _context.PeriodLocks.Remove(periodLock);
                await _context.SaveChangesAsync();
                SuccessMessage = $"Period {periodLock.Period} has been unlocked.";
            }

            LockedPeriods = await _context.PeriodLocks.OrderByDescending(p => p.Period).ToListAsync();
            return Page();
        }
    }
}
