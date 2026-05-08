using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Navigation;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Abs.FixedAssets.Pages.Assets
{
    public class AssetModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly AttachmentService _attachmentService;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;
        private readonly IOutboxWriter _outbox;

        public AssetModel(AppDbContext context, AttachmentService attachmentService, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard, IOutboxWriter outbox)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _attachmentService = attachmentService;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
            _outbox = outbox;
        }

        [BindProperty]
        public Asset Asset { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string Mode { get; set; } = "view";

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string GetBackUrl() => ReturnUrlHelper.GetBackUrl(ReturnUrl, "/Assets/Asset");

        public bool IsViewMode => Mode == "view";
        public bool IsEditMode => Mode == "edit";
        public bool IsCreateMode => Mode == "create";

        // Set when another user modified the row between load and save; the view
        // renders a yellow conflict banner when true.
        public bool HasConcurrencyConflict { get; set; }
        public Asset? ConflictServerCopy { get; set; }

        public List<Manufacturer> Manufacturers { get; set; } = new();
        public List<Location> Locations { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<Vendor> Vendors { get; set; } = new();
        public List<Site> Sites { get; set; } = new();
        public List<AssetCategory> Categories { get; set; } = new();
        public List<CostCenter> CostCenters { get; set; } = new();
        public List<Company> Companies { get; set; } = new();
        public List<Asset> ParentAssets { get; set; } = new();
        public List<Attachment> Attachments { get; set; } = new();
        public List<SelectListItem> AssetTypeOptions { get; set; } = new();
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> ConditionOptions { get; set; } = new();
        public List<SelectListItem> DepreciationMethodOptions { get; set; } = new();
        public List<SelectListItem> CurrencyOptions { get; set; } = new();
        public List<SelectListItem> MeterUomOptions { get; set; } = new();
        public List<SelectListItem> AttachmentTypeOptions { get; set; } = new();
        public List<SelectListItem> PressureUnitOptions { get; set; } = new();
        public List<SelectListItem> IoTProtocolOptions { get; set; } = new();
        public List<SelectListItem> SafetyClassOptions { get; set; } = new();
        public List<SelectListItem> EnergyEfficiencyOptions { get; set; } = new();
        public List<SelectListItem> CalibrationFreqOptions { get; set; } = new();
        public List<SelectListItem> EnvironmentalClassOptions { get; set; } = new();
        public List<SelectListItem> AssetPriorityOptions { get; set; } = new();
        public List<SelectListItem> MachineTypeOptions { get; set; } = new();
        public List<SelectListItem> SpindleTaperOptions { get; set; } = new();
        public MachineSpecification MachineSpec { get; set; } = new();
        public Asset? ParentAsset { get; set; }
        public List<Asset> ChildAssets { get; set; } = new();

        // Best-of-Breed Quick Stats
        public int OpenWorkOrderCount { get; set; }
        public MaintenanceEvent? NextScheduledMaintenance { get; set; }
        public DateTime? LastMaintenanceDate { get; set; }
        public int SparePartsCount { get; set; }
        public decimal TotalMaintenanceCostYTD { get; set; }
        public List<MaintenanceEvent> RecentActivity { get; set; } = new();
        public int DaysUntilNextPM => NextScheduledMaintenance?.ScheduledDate != null 
            ? Math.Max(0, (NextScheduledMaintenance.ScheduledDate - DateTime.Today).Days) 
            : -1;
        
        public List<MeterReading> MeterReadings { get; set; } = new();

        [BindProperty]
        public decimal NewMeterReading { get; set; }

        [BindProperty]
        public DateTime? NewMeterReadingDate { get; set; }

        [BindProperty]
        public string? NewMeterSource { get; set; }

        [BindProperty]
        public string? NewMeterNotes { get; set; }

        // Transaction History (unified view)
        public List<AssetTransactionVM> TransactionHistory { get; set; } = new();
        
        public class AssetTransactionVM
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string Type { get; set; } = string.Empty;
            public string TypeClass { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public decimal? Amount { get; set; }
            public string? Link { get; set; }
            public int AttachmentCount { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Assets" });

            if (id == null || id == 0)
            {
                Mode = "create";
                Asset = new Asset
                {
                    InServiceDate = DateTime.Today,
                    Currency = "USD",
                    Status = AssetStatus.Active,
                    Condition = AssetCondition.Good,
                    Priority = 3
                };
            }
            else
            {
                var asset = await _context.Assets
                    .Include(a => a.Manufacturer)
                    .Include(a => a.LocationRef)
                    .Include(a => a.DepartmentRef)
                    .Include(a => a.VendorRef)
                    .Include(a => a.Site)
                    .Include(a => a.AssetCategory)
                    .Include(a => a.CostCenterRef)
                    .Include(a => a.Company)
                    .FirstOrDefaultAsync(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value));

                if (asset == null)
                    return NotFound();

                if (string.IsNullOrWhiteSpace(asset.Currency))
                    asset.Currency = "USD";

                Asset = asset;

                if (Asset.ParentAssetId.HasValue)
                {
                    ParentAsset = await _context.Assets
                        .FirstOrDefaultAsync(a => a.Id == Asset.ParentAssetId);
                }

                ChildAssets = await _context.Assets
                    .Where(a => a.ParentAssetId == id)
                    .OrderBy(a => a.AssetNumber)
                    .ToListAsync();

                Attachments = await _attachmentService.GetByAssetAsync(id.Value);

                MachineSpec = await _context.MachineSpecifications
                    .FirstOrDefaultAsync(ms => ms.AssetId == asset.Id) ?? new MachineSpecification { AssetId = asset.Id };

                // Load Best-of-Breed Quick Stats - Consolidated query for efficiency
                var today = DateTime.UtcNow.Date;
                var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                
                // Single query to get all maintenance events for this asset
                var allMaintenanceEvents = await _context.MaintenanceEvents
                    .Where(m => m.AssetId == id)
                    .OrderByDescending(m => m.CreatedAt)
                    .ToListAsync();
                
                // Derive stats from the loaded data (in-memory operations)
                OpenWorkOrderCount = allMaintenanceEvents
                    .Count(m => m.Status == MaintenanceStatus.Scheduled || m.Status == MaintenanceStatus.InProgress);
                
                NextScheduledMaintenance = allMaintenanceEvents
                    .Where(m => m.Status == MaintenanceStatus.Scheduled && m.ScheduledDate >= today)
                    .OrderBy(m => m.ScheduledDate)
                    .FirstOrDefault();
                
                LastMaintenanceDate = allMaintenanceEvents
                    .Where(m => m.Status == MaintenanceStatus.Completed && m.CompletedDate != null)
                    .OrderByDescending(m => m.CompletedDate)
                    .Select(m => m.CompletedDate)
                    .FirstOrDefault();
                
                TotalMaintenanceCostYTD = allMaintenanceEvents
                    .Where(m => m.CompletedDate >= startOfYear && m.ActualCost.HasValue)
                    .Sum(m => m.ActualCost ?? 0);
                
                RecentActivity = allMaintenanceEvents.Take(5).ToList();
                
                // Load Meter Reading History
                MeterReadings = await _context.MeterReadings
                    .Where(mr => mr.AssetId == id && _tenantContext.VisibleCompanyIds.Contains(mr.CompanyId ?? 0))
                    .OrderByDescending(mr => mr.ReadingDate)
                    .Take(50)
                    .ToListAsync();

                // Load Transaction History (unified view)
                await LoadTransactionHistoryAsync(id.Value);

                if (string.IsNullOrEmpty(Mode))
                    Mode = "view";
            }

            await LoadDropdownsAsync();
            return Page();
        }

        private async Task LoadTransactionHistoryAsync(int assetId)
        {
            var transactions = new List<AssetTransactionVM>();
            
            // Load Improvements
            var improvements = await _context.CapitalImprovements
                .Where(c => c.AssetId == assetId)
                .ToListAsync();
            
            var improvementIds = improvements.Select(i => i.Id).ToList();
            var improvementAttachments = await _context.Attachments
                .Where(a => a.CapitalImprovementId != null && improvementIds.Contains(a.CapitalImprovementId.Value))
                .GroupBy(a => a.CapitalImprovementId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id ?? 0, x => x.Count);
            
            foreach (var imp in improvements)
            {
                transactions.Add(new AssetTransactionVM
                {
                    Id = imp.Id,
                    Date = imp.ImprovementDate,
                    Type = "Improvement",
                    TypeClass = "success",
                    Summary = imp.Description,
                    Amount = imp.Cost,
                    Link = $"/Assets/Improve/{assetId}#improvement-{imp.Id}",
                    AttachmentCount = improvementAttachments.GetValueOrDefault(imp.Id, 0)
                });
            }
            
            // Load Transfers
            var transfers = await _context.AssetTransfers
                .Where(t => t.AssetId == assetId)
                .ToListAsync();
            
            var transferIds = transfers.Select(t => t.Id).ToList();
            var transferAttachments = await _context.Attachments
                .Where(a => a.AssetTransferId != null && transferIds.Contains(a.AssetTransferId.Value))
                .GroupBy(a => a.AssetTransferId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id ?? 0, x => x.Count);
            
            foreach (var tr in transfers)
            {
                var fromTo = $"{tr.FromLocation ?? "—"} → {tr.ToLocation ?? "—"}";
                transactions.Add(new AssetTransactionVM
                {
                    Id = tr.Id,
                    Date = tr.TransferDate,
                    Type = "Transfer",
                    TypeClass = "info",
                    Summary = fromTo + (!string.IsNullOrEmpty(tr.Reason) ? $" ({tr.Reason})" : ""),
                    Amount = null,
                    Link = $"/Assets/Transfer/{assetId}#transfer-{tr.Id}",
                    AttachmentCount = transferAttachments.GetValueOrDefault(tr.Id, 0)
                });
            }
            
            // Load Disposals
            // Disposal attachments use AttachmentSource.Disposal + AssetId (no PartialDisposalId FK).
            // Since attachments are linked at asset-level (not disposal-specific), we show the total
            // asset-level disposal attachment count only on the most recent disposal record to avoid
            // misleading duplication when multiple disposals exist.
            var disposals = await _context.PartialDisposals
                .Where(d => d.AssetId == assetId)
                .OrderByDescending(d => d.DisposalDate)
                .ToListAsync();
            
            var disposalAttachmentCount = await _context.Attachments
                .Where(a => a.AssetId == assetId && a.Source == AttachmentSource.Disposal)
                .CountAsync();
            
            bool isFirstDisposal = true;
            foreach (var disp in disposals)
            {
                transactions.Add(new AssetTransactionVM
                {
                    Id = disp.Id,
                    Date = disp.DisposalDate,
                    Type = "Disposal",
                    TypeClass = "danger",
                    Summary = $"{disp.Reason} - {disp.PercentageDisposed:P0} disposed" + 
                              (disp.SaleProceeds > 0 ? $" (proceeds: {disp.SaleProceeds:C0})" : ""),
                    Amount = disp.GainLoss,
                    Link = $"/Assets/Dispose/{assetId}#disposal-{disp.Id}",
                    // Show attachment count only on most recent disposal to avoid duplication
                    AttachmentCount = isFirstDisposal ? disposalAttachmentCount : 0
                });
                isFirstDisposal = false;
            }
            
            TransactionHistory = transactions.OrderByDescending(t => t.Date).ToList();
        }

        private async Task LoadDropdownsAsync()
        {
            Manufacturers = await _context.Manufacturers.Where(m => m.Active).OrderBy(m => m.Name).ToListAsync();
            Locations = await _context.Locations
                .Where(l => l.IsActive && l.AllowsAssetInstallation)
                .OrderBy(l => l.SortOrder)
                .ToListAsync();
            Departments = await _context.Departments.Where(d => d.IsActive).OrderBy(d => d.SortOrder).ToListAsync();
            Vendors = await _context.Vendors.Where(v => v.IsActive).OrderBy(v => v.SortOrder).ToListAsync();
            Sites = await _context.Sites.Where(s => s.Status == SiteStatus.Active).OrderBy(s => s.Name).ToListAsync();
            Categories = await _context.AssetCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            CostCenters = await _context.CostCenters.Where(c => c.IsActive).OrderBy(c => c.Code).ToListAsync();
            var visibleIds = _tenantContext.VisibleCompanyIds;
            Companies = await _context.Companies.Where(c => c.IsActive && visibleIds.Contains(c.Id)).OrderBy(c => c.Name).ToListAsync();
            ParentAssets = await _context.Assets
                .Where(a => a.Active && a.Id != Asset.Id)
                .OrderBy(a => a.AssetNumber)
                .Take(500)
                .ToListAsync();

            var tid = _tenantContext.TenantId;
            var cid = _tenantContext.CompanyId;
            AssetTypeOptions = await _lookupService.GetSelectListByIdAsync(tid, cid, "AssetType", Asset.AssetTypeLookupValueId);
            StatusOptions = await _lookupService.GetSelectListByIdAsync(tid, cid, "AssetStatus", Asset.StatusLookupValueId);
            ConditionOptions = await _lookupService.GetSelectListByIdAsync(tid, cid, "AssetCondition", Asset.ConditionLookupValueId);
            DepreciationMethodOptions = await _lookupService.GetSelectListByIdAsync(tid, cid, "DepreciationMethod", Asset.DepreciationMethodLookupValueId);
            AssetPriorityOptions = await _lookupService.GetSelectListByIdAsync(tid, cid, "AssetPriority", Asset.AssetPriorityLookupValueId);
            CurrencyOptions = await _lookupService.GetSelectListAsync(tid, cid, "Currency", Asset.Currency);
            MeterUomOptions = await _lookupService.GetSelectListAsync(tid, cid, "MeterUOM", Asset.MeterType);
            AttachmentTypeOptions = await _lookupService.GetSelectListAsync(tid, cid, "AttachmentType");
            PressureUnitOptions = await _lookupService.GetSelectListAsync(tid, cid, "PressureUnit", Asset.PressureUOM);
            IoTProtocolOptions = await _lookupService.GetSelectListAsync(tid, cid, "IoTProtocol", Asset.IoTProtocol);
            SafetyClassOptions = await _lookupService.GetSelectListAsync(tid, cid, "SafetyClassification", Asset.SafetyClassification);
            EnergyEfficiencyOptions = await _lookupService.GetSelectListAsync(tid, cid, "EnergyEfficiencyClass");
            CalibrationFreqOptions = await _lookupService.GetSelectListAsync(tid, cid, "CalibrationFrequency");
            EnvironmentalClassOptions = await _lookupService.GetSelectListAsync(tid, cid, "EnvironmentalClass", Asset.EnvironmentalClass);
            MachineTypeOptions = await _lookupService.GetSelectListByIdAsync(tid, cid, "CncMachineType", MachineSpec.MachineTypeLookupValueId, "");
            SpindleTaperOptions = await _lookupService.GetSelectListByIdAsync(tid, cid, "SpindleTaper", MachineSpec.SpindleTaperLookupValueId, "");
        }

        private async Task SyncLookupValuesToEnumsAsync()
        {
            var tid = _tenantContext.TenantId;
            var cid = _tenantContext.CompanyId;

            if (Asset.AssetTypeLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(tid, cid, Asset.AssetTypeLookupValueId.Value);
                if (lv != null) Asset.AssetType = lv.Code;
            }

            if (Asset.StatusLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(tid, cid, Asset.StatusLookupValueId.Value);
                if (lv != null && Enum.TryParse<AssetStatus>(lv.Code, true, out var parsed))
                    Asset.Status = parsed;
            }

            if (Asset.ConditionLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(tid, cid, Asset.ConditionLookupValueId.Value);
                if (lv != null && Enum.TryParse<AssetCondition>(lv.Code, true, out var parsed))
                    Asset.Condition = parsed;
            }

            if (Asset.DepreciationMethodLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(tid, cid, Asset.DepreciationMethodLookupValueId.Value);
                if (lv != null && Enum.TryParse<DepreciationMethod>(lv.Code, true, out var parsed))
                    Asset.DepreciationMethod = parsed;
            }

            if (Asset.AssetPriorityLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(tid, cid, Asset.AssetPriorityLookupValueId.Value);
                if (lv != null && int.TryParse(lv.Code, out var priorityVal))
                    Asset.Priority = priorityVal;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDropdownsAsync();

            if (!ModelState.IsValid)
                return Page();

            await SyncLookupValuesToEnumsAsync();

            if (IsCreateMode || Asset.Id == 0)
            {
                Asset.CompanyId = _tenantContext.CompanyId;
                Asset.SiteId = _tenantContext.SiteId;
                Asset.CreatedAt = DateTime.UtcNow;
                Asset.CreatedBy = User.Identity?.Name ?? "System";
                _context.Assets.Add(Asset);
                await _context.SaveChangesAsync();

                await _outbox.EnqueueAsync(
                    Asset.CompanyId ?? 0,
                    siteId: Asset.SiteId,
                    new AssetCreatedV1(
                        AssetId: Asset.Id,
                        AssetNumber: Asset.AssetNumber,
                        Description: Asset.Description,
                        CompanyId: Asset.CompanyId,
                        SiteId: Asset.SiteId,
                        AcquisitionCost: Asset.AcquisitionCost,
                        InServiceDate: Asset.InServiceDate,
                        Status: Asset.Status.ToString(),
                        AssetCategoryId: Asset.AssetCategoryId,
                        VendorId: Asset.VendorId,
                        CreatedBy: Asset.CreatedBy,
                        Origin: "ui.assets.create"),
                    correlationId: $"asset-create-{Asset.Id}"
                );

                TempData["Success"] = $"Asset {Asset.AssetNumber} created successfully.";
                return RedirectToPage("./Asset", new { id = Asset.Id, mode = "view" });
            }
            else
            {
                var existingAsset = await _context.Assets.Where(a => a.Id == Asset.Id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).FirstOrDefaultAsync();
                if (existingAsset == null) return NotFound();

                // Pre-check before mutating fields: EF's concurrency token only fires
                // when SaveChanges actually issues an UPDATE, which is skipped on no-op
                // posts. The catch below remains as a safety net for true races.
                if (!RowVersionEquals(existingAsset.RowVersion, Asset.RowVersion))
                {
                    return await BuildConcurrencyConflictPageAsync(existingAsset);
                }

                Asset.CompanyId = existingAsset.CompanyId;
                Asset.ModifiedAt = DateTime.UtcNow;
                Asset.ModifiedBy = User.Identity?.Name ?? "System";
                _context.Entry(existingAsset).State = EntityState.Detached;
                _context.Attach(Asset).State = EntityState.Modified;
                _context.Entry(Asset).Property(a => a.RowVersion).OriginalValue = Asset.RowVersion;

                try
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Asset {Asset.AssetNumber} updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    var current = await _context.Assets
                        .AsNoTracking()
                        .Where(e => e.Id == Asset.Id
                                    && _tenantContext.VisibleCompanyIds.Contains(e.CompanyId ?? 0)
                                    && (!_tenantContext.SiteId.HasValue || e.SiteId == _tenantContext.SiteId.Value))
                        .FirstOrDefaultAsync();
                    if (current == null) return NotFound();
                    return await BuildConcurrencyConflictPageAsync(current);
                }

                return RedirectToPage("./Asset", new { id = Asset.Id, mode = "view" });
            }
        }

        // Renders the form with a conflict banner. Asset.RowVersion is intentionally
        // NOT advanced — the operator must refresh to re-read the latest server values
        // before saving, preventing a blind re-save from overwriting the other change.
        private async Task<IActionResult> BuildConcurrencyConflictPageAsync(Asset current)
        {
            var modifier = string.IsNullOrWhiteSpace(current.ModifiedBy) ? "another user" : current.ModifiedBy;
            var when = current.ModifiedAt?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                       ?? current.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            ModelState.AddModelError(string.Empty,
                $"This asset was changed by {modifier} at {when} since you opened it. " +
                "Please refresh to see the latest values — your edits were NOT saved.");
            HasConcurrencyConflict = true;
            ConflictServerCopy = current;
            Mode = "edit";
            await LoadDropdownsAsync();
            return Page();
        }

        private static bool RowVersionEquals(byte[]? a, byte[]? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public async Task<IActionResult> OnPostSaveMachineSpecAsync(int assetId, MachineSpecification machineSpec)
        {
            var asset = await _context.Assets
                .Where(a => a.Id == assetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value))
                .FirstOrDefaultAsync();
            if (asset == null) return NotFound();

            string? machineTypeCode = null;
            if (machineSpec.MachineTypeLookupValueId > 0)
            {
                machineTypeCode = await _context.LookupValues
                    .Where(lv => lv.Id == machineSpec.MachineTypeLookupValueId)
                    .Select(lv => lv.Code)
                    .FirstOrDefaultAsync();
            }

            string? spindleTaper = null;
            if (machineSpec.SpindleTaperLookupValueId > 0)
            {
                spindleTaper = await _context.LookupValues
                    .Where(lv => lv.Id == machineSpec.SpindleTaperLookupValueId)
                    .Select(lv => lv.Code)
                    .FirstOrDefaultAsync();
            }

            var existing = await _context.MachineSpecifications
                .FirstOrDefaultAsync(ms => ms.AssetId == assetId);

            if (existing == null)
            {
                machineSpec.AssetId = assetId;
                machineSpec.TenantId = _tenantContext.TenantId ?? 1;
                machineSpec.CompanyId = _tenantContext.CompanyId;
                machineSpec.MachineTypeCode = machineTypeCode;
                machineSpec.MachineTypeLookupValueId = machineSpec.MachineTypeLookupValueId > 0 ? machineSpec.MachineTypeLookupValueId : null;
                machineSpec.SpindleTaper = spindleTaper;
                machineSpec.SpindleTaperLookupValueId = machineSpec.SpindleTaperLookupValueId > 0 ? machineSpec.SpindleTaperLookupValueId : null;
                machineSpec.CreatedAt = DateTime.UtcNow;
                _context.MachineSpecifications.Add(machineSpec);
            }
            else
            {
                existing.MachineTypeCode = machineTypeCode;
                existing.MachineTypeLookupValueId = machineSpec.MachineTypeLookupValueId > 0 ? machineSpec.MachineTypeLookupValueId : null;
                existing.CncControlSystem = machineSpec.CncControlSystem;
                existing.FiveAxisCapable = machineSpec.FiveAxisCapable;
                existing.SyncFeedTapping = machineSpec.SyncFeedTapping;
                existing.CoolantThroughSpindle = machineSpec.CoolantThroughSpindle;
                existing.AtcPocketCount = machineSpec.AtcPocketCount;
                existing.SpindleTaper = spindleTaper;
                existing.SpindleTaperLookupValueId = machineSpec.SpindleTaperLookupValueId > 0 ? machineSpec.SpindleTaperLookupValueId : null;
                existing.SpindleDiameterMm = machineSpec.SpindleDiameterMm;
                existing.MaxSpindleSpeedRpm = machineSpec.MaxSpindleSpeedRpm;
                existing.SpindleMotorHp = machineSpec.SpindleMotorHp;
                existing.XAxisTravelMm = machineSpec.XAxisTravelMm;
                existing.YAxisTravelMm = machineSpec.YAxisTravelMm;
                existing.ZAxisTravelMm = machineSpec.ZAxisTravelMm;
                existing.WAxisTravelMm = machineSpec.WAxisTravelMm;
                existing.MaxSwingDiameterMm = machineSpec.MaxSwingDiameterMm;
                existing.MaxBetweenColumnsMm = machineSpec.MaxBetweenColumnsMm;
                existing.MaxHeightTableToRamMm = machineSpec.MaxHeightTableToRamMm;
                existing.MaxCuttingDiameterMm = machineSpec.MaxCuttingDiameterMm;
                existing.MaxCuttingLengthMm = machineSpec.MaxCuttingLengthMm;
                existing.TableSize = machineSpec.TableSize;
                existing.TableWeightCapacityKg = machineSpec.TableWeightCapacityKg;
                existing.MachineWeightCapacityKg = machineSpec.MachineWeightCapacityKg;
                existing.EquippedHeads = machineSpec.EquippedHeads;
                existing.ProbingSystem = machineSpec.ProbingSystem;
                existing.DetailsSketchLink = machineSpec.DetailsSketchLink;
                existing.Comments = machineSpec.Comments;
                existing.ModifiedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Machine specifications saved successfully.";
            return RedirectToPage("./Asset", new { id = assetId, mode = "view", tab = "technical" });
        }

        public async Task<IActionResult> OnPostAddMeterReadingAsync(int id)
        {
            var asset = await _context.Assets
                .Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value))
                .FirstOrDefaultAsync();
            if (asset == null) return NotFound();

            var lastReading = await _context.MeterReadings
                .Where(mr => mr.AssetId == id && _tenantContext.VisibleCompanyIds.Contains(mr.CompanyId ?? 0))
                .OrderByDescending(mr => mr.ReadingDate)
                .FirstOrDefaultAsync();

            var previousValue = lastReading?.Reading ?? asset.CurrentMeterReading;
            bool isRollover = previousValue.HasValue && NewMeterReading < previousValue.Value;
            var readingDate = NewMeterReadingDate?.ToUniversalTime() ?? DateTime.UtcNow;
            var source = !string.IsNullOrWhiteSpace(NewMeterSource) ? NewMeterSource : "Manual";

            var meterReading = new MeterReading
            {
                AssetId = id,
                Reading = NewMeterReading,
                PreviousReading = previousValue,
                ReadingDate = readingDate,
                RecordedBy = User.Identity?.Name ?? "System",
                Source = source,
                Notes = NewMeterNotes,
                IsRollover = isRollover,
                CompanyId = _tenantContext.CompanyId
            };

            if (!string.IsNullOrEmpty(asset.MeterType) && Enum.TryParse<Models.MeterType>(asset.MeterType, true, out var mt))
            {
                meterReading.MeterType = mt;
                meterReading.MeterName = asset.MeterType;
            }

            _context.MeterReadings.Add(meterReading);

            asset.CurrentMeterReading = NewMeterReading;
            asset.LastMeterReadingDate = readingDate;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Meter reading {NewMeterReading:N2} recorded successfully.";
            return RedirectToPage("./Asset", new { id, mode = "view", tab = "general" });
        }

        private static readonly HashSet<string> AllowedContentTypes = new()
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "application/pdf", "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain", "text/csv"
        };
        private const long MaxFileSize = 10 * 1024 * 1024;

        public async Task<IActionResult> OnPostUploadAsync(int id, IFormFile file, int category, string? description)
        {
            if (file == null || file.Length == 0)
                return RedirectToPage(new { id, mode = "view" });

            if (file.Length > MaxFileSize || !AllowedContentTypes.Contains(file.ContentType))
                return RedirectToPage(new { id, mode = "view" });

            using var stream = file.OpenReadStream();
            await _attachmentService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                id,
                AttachmentSource.Asset,
                null,
                (AttachmentCategory)category,
                description,
                User.Identity?.Name
            );

            return RedirectToPage(new { id, mode = "view" });
        }

        public async Task<IActionResult> OnPostDeleteAttachmentAsync(int id, int attachmentId)
        {
            var asset = await _context.Assets.Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).FirstOrDefaultAsync();
            if (asset == null) return NotFound();

            var attachment = await _context.Attachments.Where(a => a.Id == attachmentId && a.AssetId == id).FirstOrDefaultAsync();
            if (attachment == null)
                return RedirectToPage(new { id, mode = "view" });

            await _attachmentService.DeleteAsync(attachmentId);
            return RedirectToPage(new { id, mode = "view" });
        }

        public string FormatMoney(decimal amount, string? currency)
        {
            try
            {
                var symbol = (currency ?? "USD").ToUpperInvariant() switch
                {
                    "USD" => "en-US",
                    "CAD" => "en-CA",
                    "EUR" => "fr-FR",
                    "GBP" => "en-GB",
                    _ => "en-US"
                };
                var culture = CultureInfo.GetCultureInfo(symbol);
                return string.Format(culture, "{0:C}", amount);
            }
            catch
            {
                return $"${amount:N2}";
            }
        }

        public string FormatDate(DateTime? date) => date?.ToString("yyyy-MM-dd") ?? "-";


        public async Task<IActionResult> OnPostUploadImageAsync(int id, IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return RedirectToPage(new { id, mode = "view" });

            var asset = await _context.Assets.Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).FirstOrDefaultAsync();
            if (asset == null)
                return NotFound();

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "assets");
            Directory.CreateDirectory(uploadsPath);

            var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!allowedExtensions.Contains(fileExtension))
                return RedirectToPage(new { id, mode = "view" });

            var fileName = $"asset_{id}_{Guid.NewGuid():N}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            asset.ImageUrl = $"/uploads/assets/{fileName}";
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id, mode = "view" });
        }
    }
}
