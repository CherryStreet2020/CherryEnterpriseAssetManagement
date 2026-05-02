using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Abs.FixedAssets.Services;

public class ImportService
{
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;

    public ImportService(AppDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

    public async Task<ImportResult> ImportAssetsFromCsvAsync(Stream csvStream)
    {
        var companyId = GetCompanyId();
        var result = new ImportResult();
        using var reader = new StreamReader(csvStream);
        
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(headerLine))
        {
            result.Errors.Add("CSV file is empty");
            return result;
        }

        var headers = ParseCsvLine(headerLine);
        var columnMap = BuildColumnMap(headers);

        int lineNumber = 1;
        while (!reader.EndOfStream)
        {
            lineNumber++;
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var values = ParseCsvLine(line);
                var asset = MapToAsset(values, columnMap);
                asset.CompanyId = companyId;

                if (string.IsNullOrEmpty(asset.AssetNumber))
                {
                    result.Errors.Add($"Line {lineNumber}: Missing asset number");
                    result.FailedCount++;
                    continue;
                }

                var existing = await _context.Assets.AnyAsync(a => a.AssetNumber == asset.AssetNumber && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0));
                if (existing)
                {
                    result.Errors.Add($"Line {lineNumber}: Asset {asset.AssetNumber} already exists");
                    result.SkippedCount++;
                    continue;
                }

                _context.Assets.Add(asset);
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Line {lineNumber}: {ex.Message}");
                result.FailedCount++;
            }
        }

        if (result.ImportedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return result;
    }

    private List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = "";

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.Trim());
                current = "";
            }
            else
            {
                current += c;
            }
        }
        result.Add(current.Trim());
        return result;
    }

    private Dictionary<string, int> BuildColumnMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i].Trim().ToLower().Replace(" ", "").Replace("_", "");
            map[header] = i;
        }
        return map;
    }

    private Asset MapToAsset(List<string> values, Dictionary<string, int> columnMap)
    {
        var asset = new Asset
        {
            Status = AssetStatus.Active,
            Currency = "CAD"
        };

        if (TryGetValue(values, columnMap, "assetnumber", out var assetNumber) ||
            TryGetValue(values, columnMap, "assetno", out assetNumber) ||
            TryGetValue(values, columnMap, "asset#", out assetNumber))
        {
            asset.AssetNumber = assetNumber;
        }

        if (TryGetValue(values, columnMap, "description", out var desc) ||
            TryGetValue(values, columnMap, "name", out desc))
        {
            asset.Description = desc;
        }

        if (TryGetValue(values, columnMap, "model", out var model))
            asset.Model = model;

        if (TryGetValue(values, columnMap, "serialnumber", out var serial) ||
            TryGetValue(values, columnMap, "serial#", out serial))
            asset.SerialNumber = serial;


        if (TryGetValue(values, columnMap, "department", out var dept))
            asset.Department = dept;

        if (TryGetValue(values, columnMap, "acquisitioncost", out var cost) ||
            TryGetValue(values, columnMap, "cost", out cost) ||
            TryGetValue(values, columnMap, "purchaseprice", out cost))
        {
            if (decimal.TryParse(cost.Replace("$", "").Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var costValue))
                asset.AcquisitionCost = costValue;
        }

        if (TryGetValue(values, columnMap, "inservicedate", out var inService) ||
            TryGetValue(values, columnMap, "purchasedate", out inService) ||
            TryGetValue(values, columnMap, "acquireddate", out inService))
        {
            if (DateTime.TryParse(inService, out var date))
                asset.InServiceDate = date;
        }

        if (TryGetValue(values, columnMap, "usefullife", out var life) ||
            TryGetValue(values, columnMap, "usefullifeyears", out life))
        {
            if (int.TryParse(life, out var years))
                asset.UsefulLifeMonths = years * 12;
        }

        if (TryGetValue(values, columnMap, "usefullifemonths", out var lifeMonths))
        {
            if (int.TryParse(lifeMonths, out var months))
                asset.UsefulLifeMonths = months;
        }

        if (TryGetValue(values, columnMap, "salvagevalue", out var salvage) ||
            TryGetValue(values, columnMap, "residualvalue", out salvage))
        {
            if (decimal.TryParse(salvage.Replace("$", "").Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var salvageValue))
                asset.SalvageValue = salvageValue;
        }

        return asset;
    }

    private bool TryGetValue(List<string> values, Dictionary<string, int> map, string key, out string value)
    {
        value = "";
        if (!map.TryGetValue(key, out var index) || index >= values.Count)
            return false;
        value = values[index];
        return !string.IsNullOrEmpty(value);
    }
}

public class ImportResult
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool HasErrors => Errors.Any();
}
