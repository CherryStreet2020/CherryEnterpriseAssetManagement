using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.WorkCenters;

// /Admin/WorkCenters/Details/{id} — the work-center detail screen the Index grid links to.
// Read-only. Surfaces the full master record PLUS everything the B11 Resource Model /
// Finite Scheduler built around a work center: assigned ProductionResources (R2),
// ordered alternate work centers (R1-2 WorkCenterAlternate), linked EAM Assets with
// live OEE, and the scheduling / dispatch / cost configuration the R4 scheduler reads.
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only work-center detail projection (master + resources + alternates + linked assets).")]
public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    public DetailsModel(AppDbContext db) { _db = db; }

    public WorkCenterDetail? WC { get; private set; }
    public IReadOnlyList<ResourceRow> Resources { get; private set; } = new List<ResourceRow>();
    public IReadOnlyList<AlternateRow> Alternates { get; private set; } = new List<AlternateRow>();
    public IReadOnlyList<AssetRow> Assets { get; private set; } = new List<AssetRow>();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        WC = await _db.WorkCenters
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new WorkCenterDetail
            {
                Id = w.Id,
                CompanyId = w.CompanyId,
                Code = w.Code,
                Name = w.Name,
                Description = w.Description,
                Type = w.Type,
                Status = w.Status,
                CapacityModel = w.CapacityModel,
                IsActive = w.IsActive,
                LocationName = _db.Locations.Where(l => l.Id == w.LocationId).Select(l => l.Name).FirstOrDefault() ?? "—",
                SiteName = w.SiteId == null ? "—" : _db.Sites.Where(s => s.Id == w.SiteId).Select(s => s.Name).FirstOrDefault() ?? "—",
                DepartmentName = w.OwningDepartmentId == null ? "—" : _db.Departments.Where(d => d.Id == w.OwningDepartmentId).Select(d => d.Name).FirstOrDefault() ?? "—",
                CalendarCode = w.CalendarId == null ? "—" : _db.WorkCalendars.Where(c => c.Id == w.CalendarId).Select(c => c.Code).FirstOrDefault() ?? "—",
                // Scheduling / dispatch
                EfficiencyPct = w.EfficiencyPct,
                UtilizationPct = w.UtilizationPct,
                SimultaneousOperationsMax = w.SimultaneousOperationsMax,
                BottleneckFlag = w.BottleneckFlag,
                ConstraintPriority = w.ConstraintPriority,
                DispatchRule = w.DispatchRule,
                SetupFamilyRule = w.SetupFamilyRule,
                SetupFamilyCode = w.SetupFamilyCode,
                SchedulingEnabled = w.SchedulingEnabled,
                DefaultQueueTimeMins = w.DefaultQueueTimeMins,
                DefaultMoveTimeMins = w.DefaultMoveTimeMins,
                DefaultWaitTimeMins = w.DefaultWaitTimeMins,
                // Cost
                StandardCostRatePerHour = w.StandardCostRatePerHour,
                OverheadRatePerHour = w.OverheadRatePerHour,
                SetupLaborRatePerHour = w.SetupLaborRatePerHour,
                RunLaborRatePerHour = w.RunLaborRatePerHour,
                SetupMachineRatePerHour = w.SetupMachineRatePerHour,
                RunMachineRatePerHour = w.RunMachineRatePerHour,
                CurrencyCode = w.CurrencyCode,
            })
            .FirstOrDefaultAsync(ct);

        if (WC == null) return NotFound();

        Resources = await _db.ProductionResources
            .AsNoTracking()
            .Where(r => r.WorkCenterId == id)
            .OrderByDescending(r => r.IsPrimary).ThenBy(r => r.Code)
            .Select(r => new ResourceRow
            {
                Id = r.Id, Code = r.Code, Name = r.Name, Kind = r.ResourceKind,
                Status = r.Status, IsPrimary = r.IsPrimary, AssetId = r.AssetId,
            })
            .ToListAsync(ct);

        Alternates = await _db.Set<WorkCenterAlternate>()
            .AsNoTracking()
            .Where(a => a.WorkCenterId == id)
            .OrderBy(a => a.Preference)
            .Select(a => new AlternateRow
            {
                AlternateWorkCenterId = a.AlternateWorkCenterId,
                AlternateCode = _db.WorkCenters.Where(w => w.Id == a.AlternateWorkCenterId).Select(w => w.Code).FirstOrDefault() ?? "—",
                Preference = a.Preference, IsActive = a.IsActive,
                EfficiencyFactor = a.EfficiencyFactor,
            })
            .ToListAsync(ct);

        Assets = await _db.WorkCenterAssetLinks
            .AsNoTracking()
            .Where(l => l.WorkCenterId == id && l.EffectiveTo == null)
            .Join(_db.Assets, l => l.AssetId, a => a.Id, (l, a) => new AssetRow
            {
                AssetId = a.Id, AssetTag = a.AssetNumber, Name = a.Description,
                IsPrimary = l.IsPrimary, OeePct = a.CurrentOEE,
            })
            .ToListAsync(ct);

        return Page();
    }

    public sealed class WorkCenterDetail
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public WorkCenterType Type { get; set; }
        public WorkCenterStatus Status { get; set; }
        public WorkCenterCapacityModel CapacityModel { get; set; }
        public bool IsActive { get; set; }
        public string LocationName { get; set; } = "—";
        public string SiteName { get; set; } = "—";
        public string DepartmentName { get; set; } = "—";
        public string CalendarCode { get; set; } = "—";
        public decimal EfficiencyPct { get; set; }
        public decimal UtilizationPct { get; set; }
        public int? SimultaneousOperationsMax { get; set; }
        public bool BottleneckFlag { get; set; }
        public int? ConstraintPriority { get; set; }
        public WorkCenterDispatchRule DispatchRule { get; set; }
        public WorkCenterSetupFamilyRule SetupFamilyRule { get; set; }
        public string? SetupFamilyCode { get; set; }
        public bool? SchedulingEnabled { get; set; }
        public int DefaultQueueTimeMins { get; set; }
        public int DefaultMoveTimeMins { get; set; }
        public int DefaultWaitTimeMins { get; set; }
        public decimal StandardCostRatePerHour { get; set; }
        public decimal OverheadRatePerHour { get; set; }
        public decimal SetupLaborRatePerHour { get; set; }
        public decimal RunLaborRatePerHour { get; set; }
        public decimal SetupMachineRatePerHour { get; set; }
        public decimal RunMachineRatePerHour { get; set; }
        public string CurrencyCode { get; set; } = "USD";

        public string StatusTone() => Status switch
        {
            WorkCenterStatus.Active => "success",
            WorkCenterStatus.Maintenance => "warning",
            _ => "neutral",
        };
    }

    public sealed class ResourceRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ResourceKind Kind { get; set; }
        public ProductionResourceStatus Status { get; set; }
        public bool IsPrimary { get; set; }
        public int? AssetId { get; set; }
    }

    public sealed class AlternateRow
    {
        public int AlternateWorkCenterId { get; set; }
        public string AlternateCode { get; set; } = "—";
        public int Preference { get; set; }
        public bool IsActive { get; set; }
        public decimal? EfficiencyFactor { get; set; }
    }

    public sealed class AssetRow
    {
        public int AssetId { get; set; }
        public string AssetTag { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public decimal? OeePct { get; set; }
    }
}
