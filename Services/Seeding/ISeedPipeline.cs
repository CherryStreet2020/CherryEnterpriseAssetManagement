using System.Text.Json;

namespace Abs.FixedAssets.Services.Seeding
{
    public class SeedStepResult
    {
        public string StepName { get; set; } = string.Empty;
        public string DomainName { get; set; } = string.Empty;
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public bool Success => Failed == 0 && Errors.Count == 0;
        public int TotalProcessed => Inserted + Updated + Skipped + Failed;

        public string ToSummaryJson() => JsonSerializer.Serialize(new
        {
            step = StepName,
            domain = DomainName,
            inserted = Inserted,
            updated = Updated,
            skipped = Skipped,
            failed = Failed,
            warnings = Warnings.Count,
            errors = Errors.Count,
            durationMs = Duration.TotalMilliseconds
        });
    }

    public class PipelineResult
    {
        public string PipelineName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<SeedStepResult> StepResults { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public bool Success => StepResults.All(r => r.Success);
        public string Status => Success ? "Completed" : "Failed";

        public int TotalInserted => StepResults.Sum(r => r.Inserted);
        public int TotalUpdated => StepResults.Sum(r => r.Updated);
        public int TotalSkipped => StepResults.Sum(r => r.Skipped);
        public int TotalFailed => StepResults.Sum(r => r.Failed);

        public string ToSummaryJson() => JsonSerializer.Serialize(new
        {
            pipeline = PipelineName,
            version = Version,
            status = Status,
            startTime = StartTime,
            endTime = EndTime,
            durationMs = Duration.TotalMilliseconds,
            totalInserted = TotalInserted,
            totalUpdated = TotalUpdated,
            totalSkipped = TotalSkipped,
            totalFailed = TotalFailed,
            stepsCompleted = StepResults.Count,
            stepsFailed = StepResults.Count(r => !r.Success)
        });
    }

    public class PreviewStepResult
    {
        public string StepName { get; set; } = string.Empty;
        public string DomainName { get; set; } = string.Empty;
        public int WouldCreate { get; set; }
        public int WouldUpdate { get; set; }
        public int WouldSkip { get; set; }
        public int TotalInSeedData { get; set; }
        public DateTime PreviewTime { get; set; } = DateTime.UtcNow;
    }

    public class PreviewResult
    {
        public string PipelineName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<PreviewStepResult> StepPreviews { get; set; } = new();
        public DateTime PreviewTime { get; set; } = DateTime.UtcNow;
        public bool IsPreviewOnly => true;
        
        public int TotalWouldCreate => StepPreviews.Sum(s => s.WouldCreate);
        public int TotalWouldUpdate => StepPreviews.Sum(s => s.WouldUpdate);
        public int TotalWouldSkip => StepPreviews.Sum(s => s.WouldSkip);
        
        public string ToSummaryJson() => JsonSerializer.Serialize(new
        {
            pipeline = PipelineName,
            version = Version,
            isPreviewOnly = true,
            previewTime = PreviewTime,
            totalWouldCreate = TotalWouldCreate,
            totalWouldUpdate = TotalWouldUpdate,
            totalWouldSkip = TotalWouldSkip,
            stepsAnalyzed = StepPreviews.Count
        });
    }

    public interface ISeedStep
    {
        string StepName { get; }
        string DomainName { get; }
        string NaturalKeyDescription { get; }
        Task<SeedStepResult> ExecuteAsync(CancellationToken cancellationToken = default);
        Task<PreviewStepResult> PreviewAsync(CancellationToken cancellationToken = default);
    }

    public interface ISeedPipeline
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }
        bool IsDevOnly { get; }
        IReadOnlyList<ISeedStep> Steps { get; }
    }

    public class ValidationResult
    {
        public string TableName { get; set; } = string.Empty;
        public string NaturalKey { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int DuplicateCount { get; set; }
        public bool IsValid => DuplicateCount == 0;
        public List<string> DuplicateValues { get; set; } = new();
    }

    public class SeedValidationReport
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<ValidationResult> Results { get; set; } = new();
        public bool AllValid => Results.All(r => r.IsValid);
        public int TablesChecked => Results.Count;
        public int TablesWithIssues => Results.Count(r => !r.IsValid);
    }
}
