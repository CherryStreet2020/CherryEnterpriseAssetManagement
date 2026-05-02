// TENANT SCOPING EXCEPTION: Seed pipeline executor operates cross-tenant by design.
// Executes seed pipelines that populate reference/demo data for all companies.
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Abs.FixedAssets.Services.Seeding
{
    public interface ISeedPipelineExecutor
    {
        Task<PipelineResult> ExecuteAsync(ISeedPipeline pipeline, CancellationToken cancellationToken = default);
        Task<PipelineResult> ExecuteWithinTransactionAsync(ISeedPipeline pipeline, CancellationToken cancellationToken = default);
        Task<PreviewResult> PreviewAsync(ISeedPipeline pipeline, CancellationToken cancellationToken = default);
        Task<List<PipelineResult>> GetRecentRunsAsync(int count = 10);
        Task<SeedValidationReport> ValidateSeedDataAsync();
    }

    public class SeedPipelineExecutor : ISeedPipelineExecutor
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SeedPipelineExecutor> _logger;
        private readonly IWebHostEnvironment _env;

        public SeedPipelineExecutor(
            AppDbContext context,
            ILogger<SeedPipelineExecutor> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }

        public async Task<PipelineResult> ExecuteAsync(ISeedPipeline pipeline, CancellationToken cancellationToken = default)
        {
            var result = new PipelineResult
            {
                PipelineName = pipeline.Name,
                Version = pipeline.Version,
                StartTime = DateTime.UtcNow
            };

            if (pipeline.IsDevOnly && !_env.IsDevelopment())
            {
                _logger.LogWarning("Pipeline {Pipeline} is dev-only and will not run in {Environment}",
                    pipeline.Name, _env.EnvironmentName);
                result.EndTime = DateTime.UtcNow;
                result.StepResults.Add(new SeedStepResult
                {
                    StepName = "DevGate",
                    DomainName = "System",
                    Errors = new List<string> { $"Pipeline is dev-only. Current environment: {_env.EnvironmentName}" }
                });
                return result;
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    _logger.LogInformation("Starting pipeline {Pipeline} v{Version} with {StepCount} steps",
                        pipeline.Name, pipeline.Version, pipeline.Steps.Count);

                    foreach (var step in pipeline.Steps)
                    {
                        _logger.LogInformation("Executing step: {Step} ({Domain})", step.StepName, step.DomainName);

                        var stepResult = await step.ExecuteAsync(cancellationToken);
                        result.StepResults.Add(stepResult);

                        await WriteStepAuditLogAsync(pipeline, stepResult, cancellationToken);

                        if (!stepResult.Success)
                        {
                            _logger.LogError("Step {Step} failed with {ErrorCount} errors",
                                step.StepName, stepResult.Errors.Count);
                            throw new InvalidOperationException($"Step {step.StepName} failed: {string.Join("; ", stepResult.Errors)}");
                        }

                        _logger.LogInformation("Step {Step} completed: Inserted={Inserted}, Updated={Updated}, Skipped={Skipped}",
                            step.StepName, stepResult.Inserted, stepResult.Updated, stepResult.Skipped);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("Pipeline {Pipeline} committed successfully", pipeline.Name);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Pipeline {Pipeline} rolled back due to error", pipeline.Name);
                    throw;
                }
            });

            result.EndTime = DateTime.UtcNow;

            await WritePipelineAuditLogAsync(result, cancellationToken);

            return result;
        }

        public async Task<PipelineResult> ExecuteWithinTransactionAsync(ISeedPipeline pipeline, CancellationToken cancellationToken = default)
        {
            var result = new PipelineResult
            {
                PipelineName = pipeline.Name,
                Version = pipeline.Version,
                StartTime = DateTime.UtcNow
            };

            if (pipeline.IsDevOnly && !_env.IsDevelopment())
            {
                _logger.LogWarning("Pipeline {Pipeline} is dev-only and will not run in {Environment}",
                    pipeline.Name, _env.EnvironmentName);
                result.EndTime = DateTime.UtcNow;
                result.StepResults.Add(new SeedStepResult
                {
                    StepName = "DevGate",
                    DomainName = "System",
                    Errors = new List<string> { $"Pipeline is dev-only. Current environment: {_env.EnvironmentName}" }
                });
                return result;
            }

            _logger.LogInformation("Starting pipeline {Pipeline} v{Version} with {StepCount} steps (within existing transaction)",
                pipeline.Name, pipeline.Version, pipeline.Steps.Count);

            foreach (var step in pipeline.Steps)
            {
                _logger.LogInformation("Executing step: {Step} ({Domain})", step.StepName, step.DomainName);

                var stepResult = await step.ExecuteAsync(cancellationToken);
                result.StepResults.Add(stepResult);

                if (!stepResult.Success)
                {
                    _logger.LogError("Step {Step} failed with {ErrorCount} errors",
                        step.StepName, stepResult.Errors.Count);
                    break;
                }

                _logger.LogInformation("Step {Step} completed: Inserted={Inserted}, Updated={Updated}, Skipped={Skipped}",
                    step.StepName, stepResult.Inserted, stepResult.Updated, stepResult.Skipped);
            }

            result.EndTime = DateTime.UtcNow;
            _logger.LogInformation("Pipeline {Pipeline} execution completed (transaction managed externally)", pipeline.Name);

            return result;
        }

        public async Task<PreviewResult> PreviewAsync(ISeedPipeline pipeline, CancellationToken cancellationToken = default)
        {
            var result = new PreviewResult
            {
                PipelineName = pipeline.Name,
                Version = pipeline.Version,
                PreviewTime = DateTime.UtcNow
            };

            if (pipeline.IsDevOnly && !_env.IsDevelopment())
            {
                _logger.LogWarning("Pipeline {Pipeline} is dev-only and cannot be previewed in {Environment}",
                    pipeline.Name, _env.EnvironmentName);
                return result;
            }

            _logger.LogInformation("Starting preview for pipeline {Pipeline} v{Version} with {StepCount} steps",
                pipeline.Name, pipeline.Version, pipeline.Steps.Count);

            foreach (var step in pipeline.Steps)
            {
                _logger.LogInformation("Previewing step: {Step} ({Domain})", step.StepName, step.DomainName);
                
                var stepPreview = await step.PreviewAsync(cancellationToken);
                result.StepPreviews.Add(stepPreview);

                _logger.LogInformation("Step {Step} preview: WouldCreate={Create}, WouldUpdate={Update}, WouldSkip={Skip}",
                    step.StepName, stepPreview.WouldCreate, stepPreview.WouldUpdate, stepPreview.WouldSkip);
            }

            _logger.LogInformation("Pipeline {Pipeline} preview completed: TotalWouldCreate={Create}, TotalWouldUpdate={Update}, TotalWouldSkip={Skip}",
                pipeline.Name, result.TotalWouldCreate, result.TotalWouldUpdate, result.TotalWouldSkip);

            return result;
        }

        private async Task WriteStepAuditLogAsync(ISeedPipeline pipeline, SeedStepResult stepResult, CancellationToken cancellationToken)
        {
            var auditLog = new AuditLog
            {
                EntityType = $"SeedStep:{pipeline.Name}",
                EntityId = null,
                Action = "SeedStepExecute",
                Username = "SYSTEM",
                Timestamp = DateTime.UtcNow,
                Description = $"{stepResult.DomainName}: I={stepResult.Inserted} U={stepResult.Updated} S={stepResult.Skipped} F={stepResult.Failed}",
                AfterJson = stepResult.ToSummaryJson()
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task WritePipelineAuditLogAsync(PipelineResult result, CancellationToken cancellationToken)
        {
            var bulkOp = new BulkOperation
            {
                OperationType = BulkOperationType.StatusChange,
                OperationDate = result.StartTime,
                AssetsAffected = result.TotalInserted + result.TotalUpdated,
                Description = $"Seed Pipeline: {result.PipelineName} v{result.Version} - {result.Status}",
                ProcessedBy = "SYSTEM",
                CreatedAt = DateTime.UtcNow,
                AssetIds = result.ToSummaryJson()
            };

            _context.BulkOperations.Add(bulkOp);

            var auditLog = new AuditLog
            {
                EntityType = "SeedPipeline",
                EntityId = null,
                Action = result.Success ? "PipelineCompleted" : "PipelineFailed",
                Username = "SYSTEM",
                Timestamp = DateTime.UtcNow,
                Description = $"{result.PipelineName} v{result.Version}: {result.StepResults.Count} steps, {result.TotalInserted + result.TotalUpdated} records",
                AfterJson = result.ToSummaryJson()
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<PipelineResult>> GetRecentRunsAsync(int count = 10)
        {
            var logs = await _context.AuditLogs
                .Where(a => a.EntityType == "SeedPipeline" && a.AfterJson != null)
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToListAsync();

            var results = new List<PipelineResult>();
            foreach (var log in logs)
            {
                try
                {
                    var summary = JsonSerializer.Deserialize<JsonElement>(log.AfterJson!);
                    results.Add(new PipelineResult
                    {
                        PipelineName = summary.GetProperty("pipeline").GetString() ?? "",
                        Version = summary.GetProperty("version").GetString() ?? "",
                        StartTime = summary.TryGetProperty("startTime", out var st) ? st.GetDateTime() : log.Timestamp,
                        EndTime = summary.TryGetProperty("endTime", out var et) ? et.GetDateTime() : log.Timestamp
                    });
                }
                catch { }
            }

            return results;
        }

        public async Task<SeedValidationReport> ValidateSeedDataAsync()
        {
            var report = new SeedValidationReport();

            report.Results.Add(await ValidateTableAsync("WorkOrderTypes", "Code",
                async () => await _context.WorkOrderTypes.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("FailureCodes", "Code",
                async () => await _context.FailureCodes.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("CauseCodes", "Code",
                async () => await _context.CauseCodes.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("Crafts", "Code",
                async () => await _context.Crafts.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("GlAccounts", "AccountNumber",
                async () => await _context.GlAccounts.GroupBy(x => x.AccountNumber).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("Sites", "SiteCode",
                async () => await _context.Sites.GroupBy(x => x.SiteCode).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("Departments", "Code",
                async () => await _context.Departments.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("CostCenters", "Code",
                async () => await _context.CostCenters.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("AssetCategories", "Code",
                async () => await _context.AssetCategories.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("Currencies", "Code",
                async () => await _context.Currencies.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("NumberingSequences", "Code",
                async () => await _context.NumberingSequences.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("PaymentTerms", "Code",
                async () => await _context.PaymentTerms.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("Vendors", "Code",
                async () => await _context.Vendors.GroupBy(x => x.Code).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            report.Results.Add(await ValidateTableAsync("Items", "PartNumber",
                async () => await _context.Items.GroupBy(x => x.PartNumber).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync()));

            return report;
        }

        private async Task<ValidationResult> ValidateTableAsync(string tableName, string naturalKey, Func<Task<List<string>>> findDuplicates)
        {
            var duplicates = await findDuplicates();
            return new ValidationResult
            {
                TableName = tableName,
                NaturalKey = naturalKey,
                DuplicateCount = duplicates.Count,
                DuplicateValues = duplicates.Take(10).ToList()
            };
        }
    }
}
