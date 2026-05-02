using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AuditLogModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public AuditLogModel(AppDbContext context, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<AuditLog> Logs { get; set; } = new();
        public List<string> EntityTypes { get; set; } = new();
        public List<string> Actions { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;

        [BindProperty(SupportsGet = true)]
        public string? EntityType { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Action { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Username { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        public async Task OnGetAsync(int page = 1)
        {
            CurrentPage = page;

            var entityTypeValues = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "AuditEntityType");
            EntityTypes = entityTypeValues.Select(v => v.Code).ToList();
            var actionValues = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "AuditAction");
            Actions = actionValues.Select(v => v.Code).ToList();

            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(EntityType))
                query = query.Where(l => l.EntityType == EntityType);

            if (!string.IsNullOrEmpty(Action))
                query = query.Where(l => l.Action == Action);

            if (!string.IsNullOrEmpty(Username))
                query = query.Where(l => l.Username != null && l.Username.Contains(Username));

            if (FromDate.HasValue)
                query = query.Where(l => l.Timestamp >= FromDate.Value);

            if (ToDate.HasValue)
                query = query.Where(l => l.Timestamp <= ToDate.Value.AddDays(1));

            TotalCount = await query.CountAsync();

            Logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

    }
}
