using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Seeding;
using Abs.FixedAssets.Services.Seeding.Pipelines;
using static Abs.FixedAssets.Services.Seeding.SeedPackDefinitions;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DataImportModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IMasterDataBootstrapService _bootstrapService;
        private readonly ISeedPipelineExecutor _pipelineExecutor;
        private readonly SystemReferenceSeedPipeline _systemRefPipeline;
        private readonly OrgAndFinanceSeedPipeline _orgFinancePipeline;
        private readonly VendorsAndPartsFoundationSeedPipeline _vendorsPipeline;
        private readonly EamExecutionMastersSeedPipeline _eamPipeline;
        private readonly DemoScenarioSeedPipeline _demoPipeline;
        private readonly IWebHostEnvironment _env;
        private readonly ISeedGuardService _guardService;
        private readonly ISeedPackExecutor _seedPackExecutor;

        public DataImportModel(
            AppDbContext context,
            IMasterDataBootstrapService bootstrapService,
            ISeedPipelineExecutor pipelineExecutor,
            SystemReferenceSeedPipeline systemRefPipeline,
            OrgAndFinanceSeedPipeline orgFinancePipeline,
            VendorsAndPartsFoundationSeedPipeline vendorsPipeline,
            EamExecutionMastersSeedPipeline eamPipeline,
            DemoScenarioSeedPipeline demoPipeline,
            IWebHostEnvironment env,
            ISeedGuardService guardService,
            ISeedPackExecutor seedPackExecutor)
        {
            _context = context;
            _bootstrapService = bootstrapService;
            _pipelineExecutor = pipelineExecutor;
            _systemRefPipeline = systemRefPipeline;
            _orgFinancePipeline = orgFinancePipeline;
            _vendorsPipeline = vendorsPipeline;
            _eamPipeline = eamPipeline;
            _demoPipeline = demoPipeline;
            _env = env;
            _guardService = guardService;
            _seedPackExecutor = seedPackExecutor;
        }

        public Dictionary<string, int> DataStatus { get; set; } = new();
        public BootstrapReport? ImportReport { get; set; }
        public PipelineResult? PipelineResult { get; set; }
        public PreviewResult? PreviewResult { get; set; }
        public SeedPackResult? SeedPackResult { get; set; }
        public DateTime? LastRunTime { get; set; }
        public string SystemRefStatus { get; set; } = "Unknown";
        public string CoreMastersStatus { get; set; } = "Unknown";
        public List<PipelineResult> RecentRuns { get; set; } = new();
        public SeedValidationReport? ValidationReport { get; set; }
        public bool IsDevelopment => _env.IsDevelopment();
        public SeedGuardResult? GuardResult { get; set; }
        public string EnvironmentProfile => _guardService.GetEnvironmentProfile();
        public IReadOnlyList<SeedPack> SeedPacks => SeedPackDefinitions.All;
        
        private IActionResult? CheckDevAdminGate()
        {
            if (!_env.IsDevelopment())
            {
                return new JsonResult(new { error = "Seed endpoints are only available in Development mode" }) { StatusCode = 403 };
            }
            
            if (!User.IsInRole("Admin"))
            {
                return new JsonResult(new { error = "Seed endpoints require Admin role" }) { StatusCode = 403 };
            }
            
            return null;
        }

        public async Task OnGetAsync()
        {
            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            GuardResult = _guardService.CheckSeedPermission();
        }

        public async Task<IActionResult> OnPostRunSeedPackAsync(string packId)
        {
            GuardResult = _guardService.CheckSeedPermission();
            
            if (!GuardResult.Allowed)
            {
                TempData["Error"] = GuardResult.Reason;
                await LoadDataStatus();
                return Page();
            }

            var pack = SeedPackDefinitions.GetById(packId);
            if (pack == null)
            {
                TempData["Error"] = $"Unknown seed pack: {packId}";
                await LoadDataStatus();
                return Page();
            }

            try
            {
                SeedPackResult = await _seedPackExecutor.ExecuteAsync(pack);
                LastRunTime = DateTime.UtcNow;
                
                if (SeedPackResult.Success)
                {
                    TempData["Success"] = $"{pack.Name} seed pack completed: {SeedPackResult.TotalInserted} inserted, {SeedPackResult.TotalSkipped} skipped (idempotent).";
                }
                else
                {
                    TempData["Error"] = $"Seed pack failed: {string.Join(", ", SeedPackResult.Errors)}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Seed pack error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            return Page();
        }

        private async Task LoadDataStatus()
        {
            DataStatus = new Dictionary<string, int>
            {
                ["Work Order Types"] = await _context.WorkOrderTypes.CountAsync(),
                ["Failure Codes"] = await _context.FailureCodes.CountAsync(),
                ["Cause Codes"] = await _context.CauseCodes.CountAsync(),
                ["Priority Levels"] = await _context.PriorityLevels.CountAsync(),
                ["Crafts"] = await _context.Crafts.CountAsync(),
                ["Numbering Sequences"] = await _context.NumberingSequences.CountAsync(),
                ["Payment Terms"] = await _context.PaymentTerms.CountAsync(),
                ["Currencies"] = await _context.Currencies.CountAsync(),
                ["Section 179 Limits"] = await _context.Section179Limits.CountAsync(),
                ["Bonus Depreciation Rates"] = await _context.BonusDepreciationRates.CountAsync(),
                ["GL Accounts"] = await _context.GlAccounts.CountAsync(),
                ["Sites"] = await _context.Sites.CountAsync(),
                ["Departments"] = await _context.Departments.CountAsync(),
                ["Cost Centers"] = await _context.CostCenters.CountAsync(),
                ["Asset Categories"] = await _context.AssetCategories.CountAsync(),
                ["PM Templates"] = await _context.PMTemplates.CountAsync(),
                ["Vendors"] = await _context.Vendors.CountAsync(),
                ["Items"] = await _context.Items.CountAsync(),
                ["Technicians"] = await _context.Technicians.CountAsync(),
                ["Assets"] = await _context.Assets.CountAsync()
            };

            var systemRefCount = DataStatus["Work Order Types"] + DataStatus["Failure Codes"] + 
                                 DataStatus["Cause Codes"] + DataStatus["Priority Levels"] +
                                 DataStatus["Crafts"] + DataStatus["Numbering Sequences"] +
                                 DataStatus["Payment Terms"] + DataStatus["Currencies"];
            SystemRefStatus = systemRefCount > 0 ? "Seeded" : "Empty";

            var customerCount = DataStatus["GL Accounts"] + DataStatus["Sites"] + 
                               DataStatus["Departments"] + DataStatus["Cost Centers"] +
                               DataStatus["Asset Categories"];
            CoreMastersStatus = customerCount > 0 ? "Loaded" : "Empty";
        }

        public async Task<IActionResult> OnPostRunSystemReferencePipelineAsync()
        {
            try
            {
                PipelineResult = await _pipelineExecutor.ExecuteAsync(_systemRefPipeline);
                LastRunTime = DateTime.UtcNow;
                TempData["Success"] = $"SystemReferenceSeed pipeline completed: {PipelineResult.TotalInserted} inserted, {PipelineResult.TotalUpdated} updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Pipeline error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            return Page();
        }

        public async Task<IActionResult> OnPostRunOrgFinancePipelineAsync()
        {
            try
            {
                PipelineResult = await _pipelineExecutor.ExecuteAsync(_orgFinancePipeline);
                LastRunTime = DateTime.UtcNow;
                TempData["Success"] = $"OrgAndFinanceSeed pipeline completed: {PipelineResult.TotalInserted} inserted, {PipelineResult.TotalUpdated} updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Pipeline error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            return Page();
        }

        public async Task<IActionResult> OnPostRunVendorsPipelineAsync()
        {
            try
            {
                PipelineResult = await _pipelineExecutor.ExecuteAsync(_vendorsPipeline);
                LastRunTime = DateTime.UtcNow;
                TempData["Success"] = $"VendorsAndPartsFoundationSeed pipeline completed: {PipelineResult.TotalInserted} inserted, {PipelineResult.TotalUpdated} updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Pipeline error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            return Page();
        }

        public async Task<IActionResult> OnPostRunEamPipelineAsync()
        {
            try
            {
                PipelineResult = await _pipelineExecutor.ExecuteAsync(_eamPipeline);
                LastRunTime = DateTime.UtcNow;
                TempData["Success"] = $"EamExecutionMastersSeed pipeline completed: {PipelineResult.TotalInserted} inserted, {PipelineResult.TotalUpdated} updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Pipeline error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            return Page();
        }

        public async Task<IActionResult> OnPostRunDemoPipelineAsync()
        {
            if (!_env.IsDevelopment())
            {
                TempData["Error"] = "Demo seed is only available in Development mode.";
                await LoadDataStatus();
                return Page();
            }

            try
            {
                PipelineResult = await _pipelineExecutor.ExecuteAsync(_demoPipeline);
                LastRunTime = DateTime.UtcNow;
                TempData["Success"] = $"DemoScenarioSeed pipeline completed: {PipelineResult.TotalInserted} inserted, {PipelineResult.TotalUpdated} updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Pipeline error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            return Page();
        }

        public async Task<IActionResult> OnPostValidateSeedAsync()
        {
            try
            {
                ValidationReport = await _pipelineExecutor.ValidateSeedDataAsync();
                TempData["Success"] = ValidationReport.AllValid 
                    ? $"All {ValidationReport.TablesChecked} tables passed validation." 
                    : $"Validation completed: {ValidationReport.TablesWithIssues} table(s) have issues.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Validation error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            return Page();
        }

        public async Task<IActionResult> OnPostPreviewSystemReferencePipelineAsync()
        {
            try
            {
                PreviewResult = await _pipelineExecutor.PreviewAsync(_systemRefPipeline);
                TempData["Success"] = $"Preview: Would create {PreviewResult.TotalWouldCreate}, update {PreviewResult.TotalWouldUpdate}, skip {PreviewResult.TotalWouldSkip}. No changes made.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Preview error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            GuardResult = _guardService.CheckSeedPermission();
            return Page();
        }

        public async Task<IActionResult> OnPostPreviewOrgFinancePipelineAsync()
        {
            try
            {
                PreviewResult = await _pipelineExecutor.PreviewAsync(_orgFinancePipeline);
                TempData["Success"] = $"Preview: Would create {PreviewResult.TotalWouldCreate}, update {PreviewResult.TotalWouldUpdate}, skip {PreviewResult.TotalWouldSkip}. No changes made.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Preview error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            GuardResult = _guardService.CheckSeedPermission();
            return Page();
        }

        public async Task<IActionResult> OnPostPreviewVendorsPipelineAsync()
        {
            try
            {
                PreviewResult = await _pipelineExecutor.PreviewAsync(_vendorsPipeline);
                TempData["Success"] = $"Preview: Would create {PreviewResult.TotalWouldCreate}, update {PreviewResult.TotalWouldUpdate}, skip {PreviewResult.TotalWouldSkip}. No changes made.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Preview error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            GuardResult = _guardService.CheckSeedPermission();
            return Page();
        }

        public async Task<IActionResult> OnPostPreviewEamPipelineAsync()
        {
            try
            {
                PreviewResult = await _pipelineExecutor.PreviewAsync(_eamPipeline);
                TempData["Success"] = $"Preview: Would create {PreviewResult.TotalWouldCreate}, update {PreviewResult.TotalWouldUpdate}, skip {PreviewResult.TotalWouldSkip}. No changes made.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Preview error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            GuardResult = _guardService.CheckSeedPermission();
            return Page();
        }

        public async Task<IActionResult> OnPostPreviewDemoPipelineAsync()
        {
            if (!_env.IsDevelopment())
            {
                TempData["Error"] = "Demo preview is only available in Development mode.";
                await LoadDataStatus();
                return Page();
            }

            try
            {
                PreviewResult = await _pipelineExecutor.PreviewAsync(_demoPipeline);
                TempData["Success"] = $"Preview: Would create {PreviewResult.TotalWouldCreate}, update {PreviewResult.TotalWouldUpdate}, skip {PreviewResult.TotalWouldSkip}. No changes made.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Preview error: {ex.Message}";
            }

            await LoadDataStatus();
            RecentRuns = await _pipelineExecutor.GetRecentRunsAsync(5);
            GuardResult = _guardService.CheckSeedPermission();
            return Page();
        }

        public async Task<IActionResult> OnGetPreviewPipelineAsync(string name)
        {
            var gateResult = CheckDevAdminGate();
            if (gateResult != null) return gateResult;

            try
            {
                ISeedPipeline pipeline = name switch
                {
                    "SystemReference" => _systemRefPipeline,
                    "OrgAndFinance" => _orgFinancePipeline,
                    "VendorsParts" => _vendorsPipeline,
                    "EamExecution" => _eamPipeline,
                    "Demo" => _demoPipeline,
                    _ => throw new ArgumentException($"Unknown pipeline: {name}")
                };

                var result = await _pipelineExecutor.PreviewAsync(pipeline);
                return new JsonResult(new
                {
                    isPreviewOnly = true,
                    pipeline = result.PipelineName,
                    version = result.Version,
                    totalWouldCreate = result.TotalWouldCreate,
                    totalWouldUpdate = result.TotalWouldUpdate,
                    totalWouldSkip = result.TotalWouldSkip,
                    stepPreviews = result.StepPreviews.Select(s => new
                    {
                        step = s.StepName,
                        domain = s.DomainName,
                        wouldCreate = s.WouldCreate,
                        wouldUpdate = s.WouldUpdate,
                        wouldSkip = s.WouldSkip,
                        totalInSeedData = s.TotalInSeedData
                    })
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostSystemReferenceSeedAsync()
        {
            try
            {
                ImportReport = await _bootstrapService.RunSystemReferenceSeedAsync();
                LastRunTime = DateTime.UtcNow;
                TempData["Success"] = $"System reference seed completed: {ImportReport.TotalInserted} inserted, {ImportReport.TotalUpdated} updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            await LoadDataStatus();
            return Page();
        }

        public async Task<IActionResult> OnPostCustomerMasterLoadAsync()
        {
            try
            {
                ImportReport = await _bootstrapService.RunCustomerMasterLoadAsync();
                LastRunTime = DateTime.UtcNow;
                TempData["Success"] = $"EAM Core Masters load completed: {ImportReport.TotalInserted} inserted, {ImportReport.TotalUpdated} updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            await LoadDataStatus();
            return Page();
        }

        public async Task<IActionResult> OnPostDemoSeedAsync()
        {
            try
            {
                ImportReport = await _bootstrapService.RunDemoSeedAsync();
                LastRunTime = DateTime.UtcNow;
                TempData["Success"] = $"Demo seed completed: {ImportReport.TotalInserted} inserted.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            await LoadDataStatus();
            return Page();
        }

        public async Task<IActionResult> OnGetRunPipelineAsync(string name)
        {
            var gateResult = CheckDevAdminGate();
            if (gateResult != null) return gateResult;

            try
            {
                ISeedPipeline pipeline = name switch
                {
                    "SystemReference" => _systemRefPipeline,
                    "OrgAndFinance" => _orgFinancePipeline,
                    "VendorsParts" => _vendorsPipeline,
                    "EamExecution" => _eamPipeline,
                    "Demo" => _demoPipeline,
                    _ => throw new ArgumentException($"Unknown pipeline: {name}")
                };

                var result = await _pipelineExecutor.ExecuteAsync(pipeline);
                return new JsonResult(new
                {
                    success = result.Success,
                    pipeline = result.PipelineName,
                    version = result.Version,
                    totalInserted = result.TotalInserted,
                    totalUpdated = result.TotalUpdated,
                    totalSkipped = result.TotalSkipped,
                    totalFailed = result.TotalFailed,
                    steps = result.StepResults.Select(s => new
                    {
                        step = s.StepName,
                        domain = s.DomainName,
                        inserted = s.Inserted,
                        updated = s.Updated,
                        skipped = s.Skipped,
                        failed = s.Failed,
                        durationMs = s.Duration.TotalMilliseconds,
                        warnings = s.Warnings,
                        errors = s.Errors
                    })
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message, stack = ex.StackTrace }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnGetValidateAsync()
        {
            var gateResult = CheckDevAdminGate();
            if (gateResult != null) return gateResult;

            try
            {
                var report = await _pipelineExecutor.ValidateSeedDataAsync();
                return new JsonResult(new
                {
                    allValid = report.AllValid,
                    tablesChecked = report.TablesChecked,
                    tablesWithIssues = report.TablesWithIssues,
                    results = report.Results.Select(r => new
                    {
                        table = r.TableName,
                        naturalKey = r.NaturalKey,
                        isValid = r.IsValid,
                        duplicateCount = r.DuplicateCount,
                        duplicates = r.DuplicateValues
                    })
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnGetRunSystemReferenceSeedAsync()
        {
            var gateResult = CheckDevAdminGate();
            if (gateResult != null) return gateResult;

            try
            {
                var report = await _bootstrapService.RunSystemReferenceSeedAsync();
                return new JsonResult(new { 
                    success = report.Success,
                    totalInserted = report.TotalInserted,
                    totalUpdated = report.TotalUpdated,
                    totalFailed = report.TotalFailed,
                    results = report.Results.Select(r => new {
                        domain = r.Domain,
                        total = r.TotalRecords,
                        inserted = r.Inserted,
                        updated = r.Updated,
                        skipped = r.Skipped,
                        failed = r.Failed,
                        errors = r.Errors
                    })
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message, stack = ex.StackTrace }) { StatusCode = 500 };
            }
        }

        // Legacy handler name kept for route compatibility - see docs/NamingMap.md
        public async Task<IActionResult> OnGetRunCustomerMasterLoadAsync()
        {
            return await RunEamCoreMastersLoadInternalAsync();
        }
        
        // New preferred handler alias - maps to EAM Core Masters Load
        public async Task<IActionResult> OnGetRunEamCoreMastersLoadAsync()
        {
            return await RunEamCoreMastersLoadInternalAsync();
        }
        
        private async Task<IActionResult> RunEamCoreMastersLoadInternalAsync()
        {
            var gateResult = CheckDevAdminGate();
            if (gateResult != null) return gateResult;

            try
            {
                var report = await _bootstrapService.RunCustomerMasterLoadAsync();
                return new JsonResult(new { 
                    success = report.Success,
                    pipeline = "EAM Core Masters Load",
                    totalInserted = report.TotalInserted,
                    totalUpdated = report.TotalUpdated,
                    totalFailed = report.TotalFailed,
                    results = report.Results.Select(r => new {
                        domain = r.Domain,
                        total = r.TotalRecords,
                        inserted = r.Inserted,
                        updated = r.Updated,
                        skipped = r.Skipped,
                        failed = r.Failed,
                        errors = r.Errors
                    })
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message, stack = ex.StackTrace }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnGetRunDemoSeedAsync()
        {
            var gateResult = CheckDevAdminGate();
            if (gateResult != null) return gateResult;

            try
            {
                var report = await _bootstrapService.RunDemoSeedAsync();
                return new JsonResult(new { 
                    success = report.Success,
                    totalInserted = report.TotalInserted,
                    results = report.Results.Select(r => new {
                        domain = r.Domain,
                        total = r.TotalRecords,
                        inserted = r.Inserted,
                        skipped = r.Skipped
                    })
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message, stack = ex.StackTrace }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnGetRunPipelineJsonAsync(string pipeline)
        {
            var gateResult = CheckDevAdminGate();
            if (gateResult != null) return gateResult;

            try
            {
                ISeedPipeline selectedPipeline = pipeline switch
                {
                    "system" => _systemRefPipeline,
                    "org" => _orgFinancePipeline,
                    "vendors" => _vendorsPipeline,
                    "eam" => _eamPipeline,
                    "demo" => _demoPipeline,
                    _ => throw new ArgumentException($"Unknown pipeline: {pipeline}")
                };

                var result = await _pipelineExecutor.ExecuteAsync(selectedPipeline);

                var bulkOp = await _context.BulkOperations
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();

                var auditLogs = await _context.AuditLogs
                    .Where(a => a.EntityType == "SeedStep" || a.EntityType == "SeedPipeline")
                    .OrderByDescending(a => a.Timestamp)
                    .Take(result.StepResults.Count + 1)
                    .ToListAsync();

                return new JsonResult(new
                {
                    pipeline = result.PipelineName,
                    version = result.Version,
                    status = result.Status,
                    success = result.Success,
                    transactionOutcome = result.Success ? "Committed" : "Rolled back",
                    startTime = result.StartTime,
                    endTime = result.EndTime,
                    duration = result.Duration.TotalMilliseconds,
                    totalInserted = result.TotalInserted,
                    totalUpdated = result.TotalUpdated,
                    totalSkipped = result.TotalSkipped,
                    totalFailed = result.TotalFailed,
                    stepResults = result.StepResults.Select(s => new
                    {
                        step = s.StepName,
                        domain = s.DomainName,
                        inserted = s.Inserted,
                        updated = s.Updated,
                        skipped = s.Skipped,
                        failed = s.Failed,
                        success = s.Success,
                        errors = s.Errors,
                        warnings = s.Warnings
                    }),
                    bulkOperation = bulkOp == null ? null : new
                    {
                        id = bulkOp.Id,
                        type = bulkOp.OperationType.ToString(),
                        date = bulkOp.OperationDate,
                        assetsAffected = bulkOp.AssetsAffected,
                        description = bulkOp.Description,
                        createdAt = bulkOp.CreatedAt
                    },
                    auditLogs = auditLogs.Select(a => new
                    {
                        id = a.Id,
                        entityType = a.EntityType,
                        action = a.Action,
                        description = a.Description,
                        timestamp = a.Timestamp
                    })
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message, stack = ex.StackTrace }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnGetAuditReceiptsAsync()
        {
            if (!_env.IsDevelopment())
            {
                return new JsonResult(new { error = "API only available in Development mode" }) { StatusCode = 403 };
            }

            var bulkOps = await _context.BulkOperations
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .ToListAsync();

            var auditLogs = await _context.AuditLogs
                .Where(a => a.EntityType == "SeedStep" || a.EntityType == "SeedPipeline")
                .OrderByDescending(a => a.Timestamp)
                .Take(50)
                .ToListAsync();

            return new JsonResult(new
            {
                bulkOperations = bulkOps.Select(b => new
                {
                    id = b.Id,
                    type = b.OperationType.ToString(),
                    date = b.OperationDate,
                    assetsAffected = b.AssetsAffected,
                    description = b.Description,
                    createdAt = b.CreatedAt
                }),
                auditLogs = auditLogs.Select(a => new
                {
                    id = a.Id,
                    entityType = a.EntityType,
                    action = a.Action,
                    description = a.Description,
                    timestamp = a.Timestamp
                })
            });
        }
    }
}
