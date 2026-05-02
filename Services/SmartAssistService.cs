using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Abs.FixedAssets.Services;

public class SmartAssistResult
{
    public int? SuggestedAssetId { get; set; }
    public string? SuggestedAssetName { get; set; }
    public WorkRequestPriority SuggestedPriority { get; set; } = WorkRequestPriority.Medium;
    public string? SuggestedFailureCode { get; set; }
    public string? SuggestedCauseCode { get; set; }
    public string? SuggestedActionCode { get; set; }
    public string? SuggestedWorkOrderType { get; set; }
    public decimal EstimatedLaborHours { get; set; } = 2.0m;
    public string? SuggestedCraft { get; set; }
    public List<string> SuggestedTasks { get; set; } = new();
    public string Confidence { get; set; } = "Medium";
    public List<string> FactorsUsed { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
    public bool UsedAI { get; set; } = false;
}

public interface ISmartAssistService
{
    Task<SmartAssistResult> AnalyzeRequestAsync(WorkRequest request, int? siteId);
    Task<MaintenanceEvent?> GenerateDraftWorkOrderAsync(WorkRequest request, SmartAssistResult assist, string username);
}

public class SmartAssistService : ISmartAssistService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SmartAssistService> _logger;

    private static readonly Dictionary<string, WorkRequestPriority> PriorityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "emergency", WorkRequestPriority.Emergency },
        { "urgent", WorkRequestPriority.Critical },
        { "critical", WorkRequestPriority.Critical },
        { "safety", WorkRequestPriority.Critical },
        { "fire", WorkRequestPriority.Emergency },
        { "explosion", WorkRequestPriority.Emergency },
        { "injury", WorkRequestPriority.Emergency },
        { "hazard", WorkRequestPriority.Critical },
        { "downtime", WorkRequestPriority.High },
        { "production stopped", WorkRequestPriority.Critical },
        { "not working", WorkRequestPriority.High },
        { "broken", WorkRequestPriority.High },
        { "failed", WorkRequestPriority.High },
        { "leak", WorkRequestPriority.High },
        { "smoke", WorkRequestPriority.Emergency },
        { "sparks", WorkRequestPriority.Critical },
        { "electrical", WorkRequestPriority.High },
        { "heat", WorkRequestPriority.High },
        { "overheating", WorkRequestPriority.Critical },
        { "noise", WorkRequestPriority.Medium },
        { "vibration", WorkRequestPriority.Medium },
        { "minor", WorkRequestPriority.Low },
        { "cosmetic", WorkRequestPriority.Low },
        { "when convenient", WorkRequestPriority.Low }
    };

    private static readonly Dictionary<string, string> FailureKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "motor", "MOTOR-FAIL" },
        { "pump", "PUMP-FAIL" },
        { "bearing", "BEARING-WEAR" },
        { "belt", "BELT-WORN" },
        { "electrical", "ELEC-FAIL" },
        { "hydraulic", "HYD-LEAK" },
        { "pneumatic", "PNEU-FAIL" },
        { "leak", "LEAK-GEN" },
        { "vibration", "VIB-EXCESS" },
        { "overheating", "OVERHEAT" },
        { "noise", "NOISE-ABN" },
        { "corrosion", "CORROSION" },
        { "wear", "WEAR-GEN" },
        { "crack", "STRUCT-CRACK" },
        { "alignment", "MISALIGN" },
        { "sensor", "SENSOR-FAIL" },
        { "valve", "VALVE-FAIL" },
        { "control", "CTRL-FAIL" }
    };

    private static readonly Dictionary<string, string> ActionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "replace", "REPLACE" },
        { "repair", "REPAIR" },
        { "adjust", "ADJUST" },
        { "clean", "CLEAN" },
        { "lubricate", "LUBE" },
        { "inspect", "INSPECT" },
        { "calibrate", "CALIBRATE" },
        { "test", "TEST" },
        { "overhaul", "OVERHAUL" }
    };

    private static readonly Dictionary<string, string> CraftKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "electrical", "Electrician" },
        { "motor", "Electrician" },
        { "wiring", "Electrician" },
        { "hydraulic", "Hydraulic Tech" },
        { "pneumatic", "Pneumatic Tech" },
        { "plumbing", "Plumber" },
        { "pipe", "Plumber" },
        { "welding", "Welder" },
        { "weld", "Welder" },
        { "machine", "Machinist" },
        { "cnc", "Machinist" },
        { "hvac", "HVAC Tech" },
        { "heating", "HVAC Tech" },
        { "cooling", "HVAC Tech" },
        { "mechanical", "Mechanic" },
        { "bearing", "Mechanic" },
        { "pump", "Mechanic" },
        { "belt", "Mechanic" }
    };

    public SmartAssistService(AppDbContext db, IConfiguration config, ITenantContext tenantContext, ILogger<SmartAssistService> logger)
    {
        _db = db;
        _config = config;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SmartAssistResult> AnalyzeRequestAsync(WorkRequest request, int? siteId)
    {
        var result = new SmartAssistResult();
        var factors = new List<string>();
        var text = request.RequestText.ToLower();

        var smartAssistEnabled = _config.GetValue<bool>("SMART_ASSIST_ENABLED", true) ||
                        Environment.GetEnvironmentVariable("SMART_ASSIST_ENABLED")?.ToLower() != "false";
        
        if (!smartAssistEnabled)
        {
            factors.Add("Smart Assist is disabled");
            return result;
        }
        
        factors.Add("Using rule-based Smart Assist analysis");

        if (request.AssetId == null && siteId != null)
        {
            var suggestedAsset = await FindBestMatchingAssetAsync(text, siteId.Value);
            if (suggestedAsset != null)
            {
                result.SuggestedAssetId = suggestedAsset.Id;
                result.SuggestedAssetName = suggestedAsset.Description;
                factors.Add($"Asset matched by keyword similarity: '{suggestedAsset.Description}'");
            }
        }
        else if (request.AssetId != null)
        {
            var asset = await _db.Assets.Where(a => a.Id == request.AssetId).FirstOrDefaultAsync();
            if (asset != null)
            {
                result.SuggestedAssetId = asset.Id;
                result.SuggestedAssetName = asset.Description;
                factors.Add("Asset provided by requester");
            }
        }

        result.SuggestedPriority = DeterminePriority(text, factors);

        result.SuggestedFailureCode = DetermineCode(text, FailureKeywords, "failure", factors);
        result.SuggestedActionCode = DetermineCode(text, ActionKeywords, "action", factors) ?? "INSPECT";
        result.SuggestedCraft = DetermineCode(text, CraftKeywords, "craft", factors) ?? "Mechanic";

        result.EstimatedLaborHours = EstimateLaborHours(result.SuggestedPriority, result.SuggestedActionCode);
        factors.Add($"Estimated labor: {result.EstimatedLaborHours}h based on priority and action type");

        result.SuggestedTasks = GenerateTasks(text, result);
        factors.Add($"Generated {result.SuggestedTasks.Count} task steps");

        result.Confidence = DetermineConfidence(factors.Count, result);
        result.FactorsUsed = factors;
        result.Explanation = GenerateExplanation(result, factors);
        result.UsedAI = false;

        return result;
    }

    private async Task<Asset?> FindBestMatchingAssetAsync(string text, int siteId)
    {
        var assets = await _db.Assets
            .Where(a => a.LocationRef != null && a.LocationRef.SiteId == siteId && a.Status == AssetStatus.Active)
            .Select(a => new { a.Id, a.Description, a.AssetNumber })
            .Take(500)
            .ToListAsync();

        var words = Regex.Split(text, @"\W+").Where(w => w.Length > 3).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bestMatch = assets
            .Select(a => new
            {
                Asset = a,
                Score = words.Count(w =>
                    (a.Description?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.AssetNumber?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestMatch != null)
        {
            return await _db.Assets.Where(a => a.Id == bestMatch.Asset.Id).FirstOrDefaultAsync();
        }

        return null;
    }

    private WorkRequestPriority DeterminePriority(string text, List<string> factors)
    {
        var highest = WorkRequestPriority.Medium;
        var matchedKeyword = "";

        foreach (var kv in PriorityKeywords)
        {
            if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (kv.Value > highest)
                {
                    highest = kv.Value;
                    matchedKeyword = kv.Key;
                }
            }
        }

        if (!string.IsNullOrEmpty(matchedKeyword))
        {
            factors.Add($"Priority '{highest}' from keyword: '{matchedKeyword}'");
        }
        else
        {
            factors.Add("Priority defaulted to Medium (no urgency keywords detected)");
        }

        return highest;
    }

    private string? DetermineCode(string text, Dictionary<string, string> keywords, string codeType, List<string> factors)
    {
        foreach (var kv in keywords)
        {
            if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                factors.Add($"{codeType.ToUpper()} code '{kv.Value}' from keyword: '{kv.Key}'");
                return kv.Value;
            }
        }
        return null;
    }

    private decimal EstimateLaborHours(WorkRequestPriority priority, string? action)
    {
        var baseHours = priority switch
        {
            WorkRequestPriority.Emergency => 4.0m,
            WorkRequestPriority.Critical => 3.0m,
            WorkRequestPriority.High => 2.5m,
            WorkRequestPriority.Medium => 2.0m,
            WorkRequestPriority.Low => 1.0m,
            _ => 2.0m
        };

        var actionMultiplier = action switch
        {
            "OVERHAUL" => 2.0m,
            "REPLACE" => 1.5m,
            "REPAIR" => 1.25m,
            "CALIBRATE" => 1.0m,
            "ADJUST" => 0.75m,
            "CLEAN" => 0.5m,
            "INSPECT" => 0.5m,
            _ => 1.0m
        };

        return Math.Round(baseHours * actionMultiplier, 1);
    }

    private List<string> GenerateTasks(string text, SmartAssistResult result)
    {
        var tasks = new List<string>();

        tasks.Add("Review work request and gather safety equipment");

        if (result.SuggestedAssetId != null)
        {
            tasks.Add($"Locate asset and perform initial assessment");
        }

        if (result.SuggestedPriority >= WorkRequestPriority.High)
        {
            tasks.Add("Notify supervisor of high-priority issue");
        }

        var action = result.SuggestedActionCode ?? "INSPECT";
        tasks.Add(action switch
        {
            "REPLACE" => "Remove faulty component and install replacement",
            "REPAIR" => "Diagnose root cause and perform repair",
            "ADJUST" => "Adjust settings and parameters as needed",
            "CLEAN" => "Clean affected area and components",
            "CALIBRATE" => "Perform calibration procedure per specifications",
            "OVERHAUL" => "Disassemble, inspect, and rebuild assembly",
            _ => "Inspect condition and document findings"
        });

        tasks.Add("Test operation and verify issue is resolved");
        tasks.Add("Complete work order documentation and close out");

        return tasks;
    }

    private string DetermineConfidence(int factorCount, SmartAssistResult result)
    {
        var score = 0;

        if (result.SuggestedAssetId != null) score += 2;
        if (result.SuggestedFailureCode != null) score += 2;
        if (result.SuggestedActionCode != null) score += 1;
        if (result.SuggestedCraft != null) score += 1;
        if (result.SuggestedPriority != WorkRequestPriority.Medium) score += 1;

        return score switch
        {
            >= 5 => "High",
            >= 3 => "Medium",
            _ => "Low"
        };
    }

    private string GenerateExplanation(SmartAssistResult result, List<string> factors)
    {
        var lines = new List<string>
        {
            $"Confidence: {result.Confidence}",
            "",
            "Analysis factors:"
        };

        foreach (var factor in factors.Take(8))
        {
            lines.Add($"• {factor}");
        }

        return string.Join("\n", lines);
    }

    public async Task<MaintenanceEvent?> GenerateDraftWorkOrderAsync(WorkRequest request, SmartAssistResult assist, string username)
    {
        if (assist.SuggestedAssetId == null)
        {
            _logger.LogWarning("Cannot create work order without asset ID");
            return null;
        }

        var woNumber = await GenerateWorkOrderNumberAsync();

        var workOrder = new MaintenanceEvent
        {
            AssetId = assist.SuggestedAssetId.Value,
            Type = MaintenanceType.Corrective,
            Description = $"[Smart Assist Draft] {request.RequestText}".Truncate(200),
            ScheduledDate = DateTime.UtcNow.Date,
            Status = MaintenanceStatus.Scheduled,
            Priority = assist.SuggestedPriority switch
            {
                WorkRequestPriority.Emergency => MaintenancePriority.Critical,
                WorkRequestPriority.Critical => MaintenancePriority.Critical,
                WorkRequestPriority.High => MaintenancePriority.High,
                WorkRequestPriority.Medium => MaintenancePriority.Medium,
                _ => MaintenancePriority.Low
            },
            LaborHours = assist.EstimatedLaborHours,
            WorkOrderNumber = woNumber,
            FailureCode = assist.SuggestedFailureCode,
            CorrectiveAction = assist.SuggestedActionCode,
            Notes = $"Generated from Work Request #{request.RequestNumber}\n\n{assist.Explanation}",
            CreatedBy = username,
            CreatedAt = DateTime.UtcNow,
            RequestedById = null,
            RequestedAt = request.RequestedAt,
            ApprovalStatus = WorkOrderApprovalStatus.PendingApproval
        };

        _db.MaintenanceEvents.Add(workOrder);
        await _db.SaveChangesAsync();

        var seq = 10;
        foreach (var taskDesc in assist.SuggestedTasks)
        {
            var operation = new WorkOrderOperation
            {
                MaintenanceEventId = workOrder.Id,
                OperationNumber = $"OP{seq:D3}",
                Sequence = seq,
                Type = OperationType.Inspection,
                Title = taskDesc.Truncate(200),
                Status = OperationStatus.Pending,
                PlannedHours = Math.Round(assist.EstimatedLaborHours / assist.SuggestedTasks.Count, 2),
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };
            _db.WorkOrderOperations.Add(operation);
            seq += 10;
        }

        await _db.SaveChangesAsync();

        request.GeneratedWorkOrderId = workOrder.Id;
        request.Status = WorkRequestStatus.ConvertedToWO;
        request.IsAIAssisted = true;
        request.AIConfidence = assist.Confidence;
        request.AIExplanation = assist.Explanation;
        request.ModifiedAt = DateTime.UtcNow;
        request.ModifiedBy = username;
        await _db.SaveChangesAsync();

        await LogAuditAsync(request, workOrder, assist, username);

        return workOrder;
    }

    private async Task<string> GenerateWorkOrderNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"WO-{today:yyyyMM}-";
        var count = await _db.MaintenanceEvents
            .CountAsync(e => e.WorkOrderNumber != null && e.WorkOrderNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task LogAuditAsync(WorkRequest request, MaintenanceEvent wo, SmartAssistResult assist, string username)
    {
        var auditEntry = new AuditLog
        {
            EntityType = "WorkRequest",
            EntityId = request.Id,
            Action = "SmartAssist",
            Username = username,
            Timestamp = DateTime.UtcNow,
            Description = $"Smart Assist generated Work Order {wo.WorkOrderNumber} from Request {request.RequestNumber}",
            AfterJson = JsonSerializer.Serialize(new
            {
                WorkRequestId = request.Id,
                WorkOrderId = wo.Id,
                WorkOrderNumber = wo.WorkOrderNumber,
                Confidence = assist.Confidence,
                UsedAI = assist.UsedAI,
                FactorsCount = assist.FactorsUsed.Count,
                TasksGenerated = assist.SuggestedTasks.Count
            })
        };

        _db.AuditLogs.Add(auditEntry);
        await _db.SaveChangesAsync();
    }
}

public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
