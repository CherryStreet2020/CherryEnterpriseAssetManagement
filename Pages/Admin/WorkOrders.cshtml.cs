using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class WorkOrdersModel : PageModel
    {
        private readonly AppDbContext _context;

        public WorkOrdersModel(AppDbContext context)
        {
            _context = context;
        }

        public int OpenCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }
        public decimal TotalPartsCost { get; set; }
        public List<WorkOrderViewModel> WorkOrders { get; set; } = new();

        public async Task OnGetAsync()
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            var events = await _context.MaintenanceEvents
                .Include(m => m.Asset)
                .Include(m => m.Technician)
                .OrderByDescending(m => m.ScheduledDate)
                .Take(100)
                .ToListAsync();

            OpenCount = events.Count(e => e.Status == MaintenanceStatus.Scheduled);
            InProgressCount = events.Count(e => e.Status == MaintenanceStatus.InProgress);
            CompletedCount = events.Count(e => e.Status == MaintenanceStatus.Completed && e.CompletedDate >= thirtyDaysAgo);

            var partsCosts = await _context.WorkOrderParts
                .Where(p => p.MaintenanceEvent != null && p.MaintenanceEvent.ScheduledDate >= startOfMonth)
                .SumAsync(p => p.QuantityUsed * p.UnitCost);
            TotalPartsCost = partsCosts;

            WorkOrders = events.Select(e => new WorkOrderViewModel
            {
                Id = e.Id,
                WorkOrderNumber = $"WO-{e.Id:D6}",
                AssetTag = e.Asset?.AssetNumber ?? "-",
                Type = e.Type.ToString(),
                Description = e.Description ?? "",
                AssignedTo = e.Technician?.Name ?? "-",
                Status = e.Status.ToString(),
                PartsCount = 0,
                CreatedDate = e.ScheduledDate
            }).ToList();
        }

        public class WorkOrderViewModel
        {
            public int Id { get; set; }
            public string WorkOrderNumber { get; set; } = "";
            public string AssetTag { get; set; } = "";
            public string Type { get; set; } = "";
            public string Description { get; set; } = "";
            public string AssignedTo { get; set; } = "";
            public string Status { get; set; } = "";
            public int PartsCount { get; set; }
            public DateTime CreatedDate { get; set; }
        }
    }
}
