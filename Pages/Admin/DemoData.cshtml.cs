using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Seeding;
using Abs.FixedAssets.Services.Seeding.Pipelines;
using Abs.FixedAssets.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DemoDataModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ISeedPipelineExecutor _pipelineExecutor;
        private readonly DemoPackV1Pipeline _demoPackV1Pipeline;
        private readonly DemoPackV2Pipeline _demoPackV2Pipeline;
        private readonly ISeedGuardService _guardService;
        private readonly IPMSchedulerService _pmScheduler;
        private readonly IWebHostEnvironment _env;

        public DemoDataModel(
            AppDbContext db,
            ISeedPipelineExecutor pipelineExecutor,
            DemoPackV1Pipeline demoPackV1Pipeline,
            DemoPackV2Pipeline demoPackV2Pipeline,
            ISeedGuardService guardService,
            IPMSchedulerService pmScheduler,
            IWebHostEnvironment env)
        {
            _db = db;
            _pipelineExecutor = pipelineExecutor;
            _demoPackV1Pipeline = demoPackV1Pipeline;
            _demoPackV2Pipeline = demoPackV2Pipeline;
            _guardService = guardService;
            _pmScheduler = pmScheduler;
            _env = env;
        }

        public bool IsDevelopment => _env.IsDevelopment();
        public string EnvironmentProfile => _guardService.GetEnvironmentProfile();
        public bool IsDemoDataEnabled => _guardService.IsDemoDataEnabled();
        public SeedGuardResult? GuardResult { get; set; }
        public PreviewResult? PreviewResult { get; set; }
        public PreviewResult? PreviewResultV2 { get; set; }
        public PipelineResult? ExecuteResult { get; set; }
        public PipelineResult? ExecuteResultV2 { get; set; }
        public DemoDataSummary? DataSummary { get; set; }
        public DemoPackV2Summary? DataSummaryV2 { get; set; }
        public int? GeneratedWorkOrders { get; set; }
        public TimeSpan? ExecutionTime { get; set; }
        public TimeSpan? ExecutionTimeV2 { get; set; }
        
        [BindProperty]
        public bool GenerateDueWorkOrders { get; set; } = false;

        public async Task OnGetAsync()
        {
            GuardResult = _guardService.CheckSeedPermission();
            await LoadDataSummaryAsync();
            await LoadDataSummaryV2Async();
        }

        public async Task<IActionResult> OnPostPreviewAsync()
        {
            GuardResult = _guardService.CheckSeedPermission();
            
            if (!GuardResult.Allowed)
            {
                TempData["Error"] = GuardResult.Reason;
                return Page();
            }

            try
            {
                var sw = Stopwatch.StartNew();
                PreviewResult = await _pipelineExecutor.PreviewAsync(_demoPackV1Pipeline);
                sw.Stop();
                ExecutionTime = sw.Elapsed;
                TempData["Success"] = $"Preview completed in {sw.ElapsedMilliseconds}ms. No changes made to database.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Preview failed: {ex.Message}";
            }

            await LoadDataSummaryAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostExecuteAsync()
        {
            GuardResult = _guardService.CheckSeedPermission();
            
            if (!GuardResult.Allowed)
            {
                TempData["Error"] = GuardResult.Reason;
                await LoadDataSummaryAsync();
                return Page();
            }

            var correlationId = Guid.NewGuid().ToString("N")[..12];
            var sw = Stopwatch.StartNew();

            try
            {
                ExecuteResult = await _pipelineExecutor.ExecuteAsync(_demoPackV1Pipeline);
                
                if (GenerateDueWorkOrders && ExecuteResult.Success)
                {
                    var generateResult = await _pmScheduler.GenerateDueAsync(30, DateTime.UtcNow, "DemoPackV1");
                    GeneratedWorkOrders = generateResult.CreatedCount;
                }

                sw.Stop();
                ExecutionTime = sw.Elapsed;

                await WriteAuditLogAsync(correlationId, ExecuteResult, GeneratedWorkOrders);

                if (ExecuteResult.Success)
                {
                    var woMessage = GeneratedWorkOrders > 0 ? $" Generated {GeneratedWorkOrders} work orders." : "";
                    TempData["Success"] = $"Demo Pack v1 executed successfully in {sw.ElapsedMilliseconds}ms. " +
                                         $"Inserted: {ExecuteResult.TotalInserted}, Updated: {ExecuteResult.TotalUpdated}, Skipped: {ExecuteResult.TotalSkipped}.{woMessage}";
                }
                else
                {
                    TempData["Error"] = $"Demo Pack v1 completed with errors: {string.Join(", ", ExecuteResult.StepResults.SelectMany(s => s.Errors))}";
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                TempData["Error"] = $"Execution failed: {ex.Message}";
            }

            await LoadDataSummaryAsync();
            await LoadDataSummaryV2Async();
            return Page();
        }

        public async Task<IActionResult> OnPostPreviewV2Async()
        {
            GuardResult = _guardService.CheckSeedPermission();
            
            if (!GuardResult.Allowed)
            {
                TempData["Error"] = GuardResult.Reason;
                return Page();
            }

            try
            {
                var sw = Stopwatch.StartNew();
                PreviewResultV2 = await _pipelineExecutor.PreviewAsync(_demoPackV2Pipeline);
                sw.Stop();
                ExecutionTimeV2 = sw.Elapsed;
                TempData["Success"] = $"DemoPackV2 Preview completed in {sw.ElapsedMilliseconds}ms. No changes made to database.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Preview failed: {ex.Message}";
            }

            await LoadDataSummaryAsync();
            await LoadDataSummaryV2Async();
            return Page();
        }

        public async Task<IActionResult> OnPostExecuteV2Async()
        {
            GuardResult = _guardService.CheckSeedPermission();
            
            if (!GuardResult.Allowed)
            {
                TempData["Error"] = GuardResult.Reason;
                await LoadDataSummaryAsync();
                await LoadDataSummaryV2Async();
                return Page();
            }

            var correlationId = Guid.NewGuid().ToString("N")[..12];
            var sw = Stopwatch.StartNew();

            try
            {
                ExecuteResultV2 = await _pipelineExecutor.ExecuteAsync(_demoPackV2Pipeline);
                sw.Stop();
                ExecutionTimeV2 = sw.Elapsed;

                await WriteAuditLogV2Async(correlationId, ExecuteResultV2);

                if (ExecuteResultV2.Success)
                {
                    TempData["Success"] = $"Demo Pack v2 executed successfully in {sw.ElapsedMilliseconds}ms. " +
                                         $"Inserted: {ExecuteResultV2.TotalInserted}, Updated: {ExecuteResultV2.TotalUpdated}, Skipped: {ExecuteResultV2.TotalSkipped}.";
                }
                else
                {
                    TempData["Error"] = $"Demo Pack v2 completed with errors: {string.Join(", ", ExecuteResultV2.StepResults.SelectMany(s => s.Errors))}";
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                TempData["Error"] = $"Execution failed: {ex.Message}";
            }

            await LoadDataSummaryAsync();
            await LoadDataSummaryV2Async();
            return Page();
        }

        private async Task LoadDataSummaryAsync()
        {
            DataSummary = new DemoDataSummary
            {
                DemoAssetCount = await _db.Assets.CountAsync(a => a.AssetNumber.StartsWith("DEMO-")),
                DemoPMTemplateCount = await _db.PMTemplates.CountAsync(t => t.Code.StartsWith("PM-")),
                DemoPMScheduleCount = await _db.PMSchedules.CountAsync(),
                DemoWorkOrderCount = await _db.PMOccurrences.CountAsync(),
                TotalAssetCount = await _db.Assets.CountAsync(),
                TotalPMTemplateCount = await _db.PMTemplates.CountAsync(),
                TotalPMScheduleCount = await _db.PMSchedules.CountAsync(),
                TotalWorkOrderCount = await _db.MaintenanceEvents.CountAsync()
            };
        }

        private async Task WriteAuditLogAsync(string correlationId, PipelineResult result, int? generatedWorkOrders)
        {
            var summaryJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                correlationId,
                pipeline = result.PipelineName,
                version = result.Version,
                executedBy = User.Identity?.Name ?? "Unknown",
                executedAt = DateTime.UtcNow.ToString("O"),
                duration = result.EndTime - result.StartTime,
                totalInserted = result.TotalInserted,
                totalUpdated = result.TotalUpdated,
                totalSkipped = result.TotalSkipped,
                generatedWorkOrders = generatedWorkOrders ?? 0,
                steps = result.StepResults.Select(s => new
                {
                    step = s.StepName,
                    domain = s.DomainName,
                    inserted = s.Inserted,
                    updated = s.Updated,
                    skipped = s.Skipped,
                    failed = s.Failed
                })
            });

            var auditLog = new AuditLog
            {
                EntityType = "DemoPack",
                EntityId = null,
                Action = "DEMOSEED",
                Username = User.Identity?.Name ?? "SYSTEM",
                Timestamp = DateTime.UtcNow,
                Description = $"Demo Pack v1: {result.TotalInserted} inserted, {result.TotalUpdated} updated, {result.TotalSkipped} skipped",
                AfterJson = summaryJson
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();
        }

        private async Task LoadDataSummaryV2Async()
        {
            DataSummaryV2 = new DemoPackV2Summary
            {
                DemoItemCount = await _db.Items.CountAsync(i => i.PartNumber.StartsWith("DEMO-PN-")),
                DemoManufacturerCount = await _db.Manufacturers.CountAsync(m => m.Code != null && m.Code.StartsWith("DEMO-MFR-")),
                TotalItemCount = await _db.Items.CountAsync(),
                TotalManufacturerCount = await _db.Manufacturers.CountAsync(),
                TotalMPNCount = await _db.ItemManufacturerParts.CountAsync(),
                TotalVPNCount = await _db.VendorItemParts.CountAsync(),
                TotalAVLCount = await _db.ItemApprovedVendors.CountAsync(),
                TotalAlternateCount = await _db.ItemAlternates.CountAsync(),
                TotalSupersessionCount = await _db.ItemSupersessions.CountAsync()
            };
        }

        private async Task WriteAuditLogV2Async(string correlationId, PipelineResult result)
        {
            var summaryJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                correlationId,
                pipeline = result.PipelineName,
                version = result.Version,
                executedBy = User.Identity?.Name ?? "Unknown",
                executedAt = DateTime.UtcNow.ToString("O"),
                duration = result.EndTime - result.StartTime,
                totalInserted = result.TotalInserted,
                totalUpdated = result.TotalUpdated,
                totalSkipped = result.TotalSkipped,
                steps = result.StepResults.Select(s => new
                {
                    step = s.StepName,
                    domain = s.DomainName,
                    inserted = s.Inserted,
                    updated = s.Updated,
                    skipped = s.Skipped,
                    failed = s.Failed
                })
            });

            var auditLog = new AuditLog
            {
                EntityType = "DemoPack",
                EntityId = null,
                Action = "DEMOSEED.V2",
                Username = User.Identity?.Name ?? "SYSTEM",
                Timestamp = DateTime.UtcNow,
                Description = $"Demo Pack v2: {result.TotalInserted} inserted, {result.TotalUpdated} updated, {result.TotalSkipped} skipped",
                AfterJson = summaryJson
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();
        }
    }

    public class DemoPackV2Summary
    {
        public int DemoItemCount { get; set; }
        public int DemoManufacturerCount { get; set; }
        public int TotalItemCount { get; set; }
        public int TotalManufacturerCount { get; set; }
        public int TotalMPNCount { get; set; }
        public int TotalVPNCount { get; set; }
        public int TotalAVLCount { get; set; }
        public int TotalAlternateCount { get; set; }
        public int TotalSupersessionCount { get; set; }
    }

    public class DemoDataSummary
    {
        public int DemoAssetCount { get; set; }
        public int DemoPMTemplateCount { get; set; }
        public int DemoPMScheduleCount { get; set; }
        public int DemoWorkOrderCount { get; set; }
        public int TotalAssetCount { get; set; }
        public int TotalPMTemplateCount { get; set; }
        public int TotalPMScheduleCount { get; set; }
        public int TotalWorkOrderCount { get; set; }
    }
}
