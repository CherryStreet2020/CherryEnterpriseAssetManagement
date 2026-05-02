using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class EnvironmentStatusModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ISeedGuardService _guardService;
        private readonly IWebHostEnvironment _env;

        public EnvironmentStatusModel(
            AppDbContext context, 
            ISeedGuardService guardService,
            IWebHostEnvironment env)
        {
            _context = context;
            _guardService = guardService;
            _env = env;
        }

        public string EnvironmentProfile { get; set; } = string.Empty;
        public string MaskedConnectionString { get; set; } = string.Empty;
        public bool IsLabEnvironment { get; set; }
        public bool IsDemoEnvironment { get; set; }
        public bool IsDevelopment { get; set; }
        public SeedGuardResult? GuardResult { get; set; }
        public Dictionary<string, int> TableCounts { get; set; } = new();
        public DateTime CheckedAt { get; set; }

        public async Task OnGetAsync()
        {
            CheckedAt = DateTime.UtcNow;
            EnvironmentProfile = _guardService.GetEnvironmentProfile();
            MaskedConnectionString = _guardService.GetMaskedConnectionString();
            IsLabEnvironment = _guardService.IsLabEnvironment();
            IsDemoEnvironment = _guardService.IsDemoEnvironment();
            IsDevelopment = _env.IsDevelopment();
            GuardResult = _guardService.CheckSeedPermission();

            TableCounts = new Dictionary<string, int>
            {
                ["Companies"] = await _context.Companies.CountAsync(),
                ["Sites"] = await _context.Sites.CountAsync(),
                ["Locations"] = await _context.Locations.CountAsync(),
                ["Assets"] = await _context.Assets.CountAsync(),
                ["Vendors"] = await _context.Vendors.CountAsync(),
                ["Technicians"] = await _context.Technicians.CountAsync(),
                ["Books"] = await _context.Books.CountAsync(),
                ["DepreciationPolicies"] = await _context.DepreciationPolicies.CountAsync(),
                ["MaintenanceEvents"] = await _context.MaintenanceEvents.CountAsync(),
                ["GlAccounts"] = await _context.GlAccounts.CountAsync(),
                ["WorkOrderTypes"] = await _context.WorkOrderTypes.CountAsync(),
                ["Items"] = await _context.Items.CountAsync(),
                ["PurchaseOrders"] = await _context.PurchaseOrders.CountAsync(),
                ["Users"] = await _context.Users.CountAsync()
            };
        }

        public async Task<IActionResult> OnGetApiStatusAsync()
        {
            var profile = _guardService.GetEnvironmentProfile();
            var guardResult = _guardService.CheckSeedPermission();

            var tableCounts = new Dictionary<string, int>
            {
                ["Companies"] = await _context.Companies.CountAsync(),
                ["Sites"] = await _context.Sites.CountAsync(),
                ["Locations"] = await _context.Locations.CountAsync(),
                ["Assets"] = await _context.Assets.CountAsync(),
                ["Vendors"] = await _context.Vendors.CountAsync(),
                ["Technicians"] = await _context.Technicians.CountAsync(),
                ["Books"] = await _context.Books.CountAsync(),
                ["MaintenanceEvents"] = await _context.MaintenanceEvents.CountAsync(),
                ["GlAccounts"] = await _context.GlAccounts.CountAsync()
            };

            return new JsonResult(new
            {
                checkedAt = DateTime.UtcNow,
                environment = new
                {
                    profile = profile,
                    isLab = _guardService.IsLabEnvironment(),
                    isDemo = _guardService.IsDemoEnvironment(),
                    isDevelopment = _env.IsDevelopment(),
                    maskedConnection = _guardService.GetMaskedConnectionString()
                },
                seedGuard = new
                {
                    allowed = guardResult.Allowed,
                    reason = guardResult.Reason,
                    passedChecks = guardResult.PassedChecks,
                    failedChecks = guardResult.FailedChecks
                },
                tableCounts = tableCounts,
                totalRows = tableCounts.Values.Sum()
            });
        }
    }
}
