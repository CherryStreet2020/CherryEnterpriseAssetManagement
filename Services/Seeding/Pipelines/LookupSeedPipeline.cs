using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Abs.FixedAssets.Services.Seeding.Pipelines;

public class LookupSeedPipeline : ISeedPipeline
{
    public string Name => "LookupReferenceData";
    public string Version => "2.0.0";
    public string Description => "Seeds LookupType and LookupValue reference data from JSON files";
    public bool IsDevOnly => false;

    private readonly List<ISeedStep> _steps;
    public IReadOnlyList<ISeedStep> Steps => _steps;

    public LookupSeedPipeline(AppDbContext context, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<LookupSeedPipeline>();
        _steps = new List<ISeedStep>
        {
            new LookupTypeSeedStep(context, logger),
            new LookupValueSeedStep(context, logger)
        };
    }
}

public class LookupTypeSeedStep : BaseSeedStep<LookupType>
{
    public override string StepName => "LookupTypes";
    public override string DomainName => "LookupTypes";
    public override string NaturalKeyDescription => "TenantId+CompanyId+Key";

    private int? _tenantId;
    private List<LookupSeedFile> _seedFiles = new();

    public LookupTypeSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

    protected override async Task OnBeforeExecuteAsync(CancellationToken cancellationToken)
    {
        var tenant = await Context.Tenants.FirstOrDefaultAsync(cancellationToken);
        _tenantId = tenant?.Id;
        _seedFiles = LookupSeedFileLoader.LoadAll();
    }

    protected override IEnumerable<LookupType> GetSeedData()
    {
        var now = DateTime.UtcNow;
        return _seedFiles.Select(f => new LookupType
        {
            TenantId = _tenantId, CompanyId = null, Key = f.LookupKey, Name = f.Name,
            IsSystem = f.IsSystem, IsActive = true, CreatedAt = now, UpdatedAt = now
        });
    }

    protected override async Task<LookupType?> FindByNaturalKeyAsync(LookupType item, CancellationToken cancellationToken)
    {
        return await Context.LookupTypes.FirstOrDefaultAsync(
            lt => lt.TenantId == item.TenantId && lt.CompanyId == item.CompanyId && lt.Key == item.Key,
            cancellationToken);
    }

    protected override string GetNaturalKeyValue(LookupType item) => $"{item.TenantId}/{item.CompanyId}/{item.Key}";

    protected override bool ShouldUpdate(LookupType existing, LookupType incoming)
        => existing.Name != incoming.Name;

    protected override void UpdateEntity(LookupType existing, LookupType incoming)
    {
        existing.Name = incoming.Name;
        existing.UpdatedAt = DateTime.UtcNow;
    }
}

public class LookupValueSeedStep : BaseSeedStep<LookupValue>
{
    public override string StepName => "LookupValues";
    public override string DomainName => "LookupValues";
    public override string NaturalKeyDescription => "LookupTypeId+Code";

    private readonly Dictionary<string, int> _typeIdMap = new();
    private List<LookupSeedFile> _seedFiles = new();

    public LookupValueSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

    protected override async Task OnBeforeExecuteAsync(CancellationToken cancellationToken)
    {
        var types = await Context.LookupTypes.ToListAsync(cancellationToken);
        foreach (var t in types)
            _typeIdMap[t.Key] = t.Id;
        _seedFiles = LookupSeedFileLoader.LoadAll();
    }

    private int TypeId(string key) => _typeIdMap.TryGetValue(key, out var id) ? id : 0;

    protected override IEnumerable<LookupValue> GetSeedData()
    {
        var now = DateTime.UtcNow;
        var values = new List<LookupValue>();

        foreach (var file in _seedFiles)
        {
            var typeId = TypeId(file.LookupKey);
            if (typeId == 0) continue;

            foreach (var v in file.Values)
            {
                JsonDocument? metadata = null;
                if (v.Metadata != null && v.Metadata.Count > 0)
                    metadata = JsonDocument.Parse(JsonSerializer.Serialize(v.Metadata));

                values.Add(new LookupValue
                {
                    LookupTypeId = typeId, Code = v.Code, Name = v.Name, SortOrder = v.SortOrder,
                    IsActive = v.IsActive, Metadata = metadata, CreatedAt = now, UpdatedAt = now
                });
            }
        }

        return values;
    }

    protected override async Task<LookupValue?> FindByNaturalKeyAsync(LookupValue item, CancellationToken cancellationToken)
    {
        return await Context.LookupValues.FirstOrDefaultAsync(
            lv => lv.LookupTypeId == item.LookupTypeId && lv.Code == item.Code,
            cancellationToken);
    }

    protected override string GetNaturalKeyValue(LookupValue item) => $"{item.LookupTypeId}/{item.Code}";

    protected override bool ShouldUpdate(LookupValue existing, LookupValue incoming)
        => existing.Name != incoming.Name || existing.SortOrder != incoming.SortOrder;

    protected override void UpdateEntity(LookupValue existing, LookupValue incoming)
    {
        existing.Name = incoming.Name;
        existing.SortOrder = incoming.SortOrder;
        existing.Metadata = incoming.Metadata;
        existing.UpdatedAt = DateTime.UtcNow;
    }
}

public class LookupSeedFile
{
    public string LookupKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;
    public List<LookupSeedValue> Values { get; set; } = new();
}

public class LookupSeedValue
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

public static class LookupSeedFileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<LookupSeedFile> LoadAll()
    {
        var seedDir = FindSeedDirectory();
        if (seedDir == null || !Directory.Exists(seedDir))
            return new List<LookupSeedFile>();

        var files = new List<LookupSeedFile>();
        foreach (var path in Directory.GetFiles(seedDir, "*.json").OrderBy(p => p))
        {
            try
            {
                var json = File.ReadAllText(path);
                var seedFile = JsonSerializer.Deserialize<LookupSeedFile>(json, JsonOptions);
                if (seedFile != null && !string.IsNullOrEmpty(seedFile.LookupKey))
                    files.Add(seedFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LookupSeedLoader] WARNING: Failed to parse {path}: {ex.Message}");
            }
        }
        return files;
    }

    private static string? FindSeedDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "seed", "reference-data"),
            Path.Combine(Directory.GetCurrentDirectory(), "seed", "reference-data"),
            "seed/reference-data"
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
}

public static class LookupDirectSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var seedFiles = LookupSeedFileLoader.LoadAll();
        if (seedFiles.Count == 0)
        {
            Console.WriteLine("[LookupDirectSeeder] No seed files found in seed/reference-data/");
            return;
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync();
        int? tenantId = tenant?.Id;
        var now = DateTime.UtcNow;

        foreach (var file in seedFiles)
        {
            var exists = await db.LookupTypes.AnyAsync(lt => lt.TenantId == tenantId && lt.CompanyId == null && lt.Key == file.LookupKey);
            if (!exists)
            {
                db.LookupTypes.Add(new LookupType
                {
                    TenantId = tenantId, CompanyId = null, Key = file.LookupKey, Name = file.Name,
                    IsSystem = file.IsSystem, IsActive = true, CreatedAt = now, UpdatedAt = now
                });
            }
        }
        await db.SaveChangesAsync();

        var typeIdMap = await db.LookupTypes
            .Where(lt => lt.TenantId == tenantId && lt.CompanyId == null)
            .ToDictionaryAsync(lt => lt.Key, lt => lt.Id);

        foreach (var file in seedFiles)
        {
            if (!typeIdMap.TryGetValue(file.LookupKey, out var typeId) || typeId == 0) continue;

            foreach (var v in file.Values)
            {
                var valueExists = await db.LookupValues.AnyAsync(lv => lv.LookupTypeId == typeId && lv.Code == v.Code);
                if (!valueExists)
                {
                    JsonDocument? metadata = null;
                    if (v.Metadata != null && v.Metadata.Count > 0)
                        metadata = JsonDocument.Parse(JsonSerializer.Serialize(v.Metadata));

                    db.LookupValues.Add(new LookupValue
                    {
                        LookupTypeId = typeId, Code = v.Code, Name = v.Name, SortOrder = v.SortOrder,
                        IsActive = v.IsActive, Metadata = metadata, CreatedAt = now, UpdatedAt = now
                    });
                }
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"[LookupDirectSeeder] Seeded {seedFiles.Count} lookup types from JSON files");

        await EnforceBaselinesAsync(db);
    }

    private static async Task EnforceBaselinesAsync(AppDbContext db)
    {
        var result = await LookupBaselineEnforcer.EnforceAllAsync(db);
        Console.WriteLine($"[LookupBaselineEnforcer] Enforced baselines across {result.KeysProcessed} key(s), {result.ValuesAdded} value(s) added, {result.ValuesUpdated} updated");
    }
}

public static class LookupBaselineEnforcer
{
    public static async Task<BaselineEnforcementResult> EnforceAllAsync(AppDbContext db, string? filterKey = null, int? filterLookupTypeId = null)
    {
        var baselines = LookupBaselineLoader.Load();
        if (baselines.Count == 0) return new BaselineEnforcementResult();

        if (filterKey != null)
            baselines = baselines.Where(b => b.LookupKey.Equals(filterKey, StringComparison.OrdinalIgnoreCase)).ToList();

        var now = DateTime.UtcNow;
        int added = 0, updated = 0;

        foreach (var baseline in baselines)
        {
            IQueryable<LookupType> query = db.LookupTypes.Where(lt => lt.Key == baseline.LookupKey);
            if (filterLookupTypeId.HasValue)
                query = query.Where(lt => lt.Id == filterLookupTypeId.Value);

            var matchingTypes = await query.ToListAsync();

            foreach (var lt in matchingTypes)
            {
                foreach (var bv in baseline.Values)
                {
                    var existing = await db.LookupValues
                        .FirstOrDefaultAsync(lv => lv.LookupTypeId == lt.Id
                            && lv.Code.ToLower() == bv.Code.ToLower());

                    JsonDocument? metaDoc = null;
                    if (bv.Metadata != null && bv.Metadata.Count > 0)
                        metaDoc = JsonDocument.Parse(JsonSerializer.Serialize(bv.Metadata));

                    if (existing == null)
                    {
                        db.LookupValues.Add(new LookupValue
                        {
                            LookupTypeId = lt.Id,
                            Code = bv.Code,
                            Name = bv.Name,
                            SortOrder = bv.SortOrder,
                            IsActive = bv.IsActive,
                            Metadata = metaDoc,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                        added++;
                        Console.WriteLine($"[LookupBaselineEnforcer] Added baseline '{bv.Code}' to {lt.Key} (TenantId={lt.TenantId}, CompanyId={lt.CompanyId})");
                    }
                    else
                    {
                        bool needsUpdate = existing.Name != bv.Name || existing.SortOrder != bv.SortOrder || existing.IsActive != bv.IsActive;
                        if (needsUpdate)
                        {
                            existing.Name = bv.Name;
                            existing.SortOrder = bv.SortOrder;
                            existing.IsActive = bv.IsActive;
                            existing.UpdatedAt = now;
                            updated++;
                            Console.WriteLine($"[LookupBaselineEnforcer] Updated baseline '{bv.Code}' in {lt.Key} (TenantId={lt.TenantId}, CompanyId={lt.CompanyId})");
                        }
                        if (metaDoc != null && existing.Metadata == null)
                        {
                            existing.Metadata = metaDoc;
                            existing.UpdatedAt = now;
                        }
                    }
                }
            }
        }

        if (added > 0 || updated > 0)
            await db.SaveChangesAsync();

        return new BaselineEnforcementResult { KeysProcessed = baselines.Count, ValuesAdded = added, ValuesUpdated = updated };
    }

    public static async Task EnforceForLookupTypeAsync(AppDbContext db, int lookupTypeId, string lookupKey)
    {
        var result = await EnforceAllAsync(db, filterKey: lookupKey, filterLookupTypeId: lookupTypeId);
        if (result.ValuesAdded > 0)
            Console.WriteLine($"[LookupBaselineEnforcer] Auto-enforced {result.ValuesAdded} baseline(s) for new LookupType {lookupKey} (Id={lookupTypeId})");
    }
}

public class BaselineEnforcementResult
{
    public int KeysProcessed { get; set; }
    public int ValuesAdded { get; set; }
    public int ValuesUpdated { get; set; }
}

public class LookupBaselineConfig
{
    public List<LookupBaselineRule> Baselines { get; set; } = new();
}

public class LookupBaselineRule
{
    public string LookupKey { get; set; } = string.Empty;
    public List<LookupBaselineValue> Values { get; set; } = new();
}

public class LookupBaselineValue
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

public static class LookupBaselineLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<LookupBaselineRule> Load()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "config", "lookup_baselines.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "config", "lookup_baselines.json"),
            "config/lookup_baselines.json"
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path == null) return new List<LookupBaselineRule>();

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<LookupBaselineConfig>(json, JsonOptions);
            return config?.Baselines ?? new List<LookupBaselineRule>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LookupBaselineLoader] WARNING: Failed to parse {path}: {ex.Message}");
            return new List<LookupBaselineRule>();
        }
    }
}
