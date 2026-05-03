using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Abs.FixedAssets.Services
{
    public class ReportBuilderService
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public ReportBuilderService(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<List<Dictionary<string, object>>> BuildAssetReportAsync(
            List<string> selectedFields,
            string? locationFilter = null,
            string? statusFilter = null,
            decimal? minValue = null,
            decimal? maxValue = null)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var query = _context.Assets.Where(a => visibleIds.Contains(a.CompanyId ?? 0));

            if (!string.IsNullOrEmpty(locationFilter))
                query = query.Where(a => a.LocationRef != null && a.LocationRef.Name == locationFilter);

            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<AssetStatus>(statusFilter, out var status))
                query = query.Where(a => a.Status == status);

            if (minValue.HasValue)
                query = query.Where(a => a.AcquisitionCost >= minValue.Value);

            if (maxValue.HasValue)
                query = query.Where(a => a.AcquisitionCost <= maxValue.Value);

            var assets = await query.ToListAsync();

            var results = new List<Dictionary<string, object>>();
            foreach (var asset in assets)
            {
                var row = new Dictionary<string, object>();
                foreach (var field in selectedFields)
                {
                    row[field] = GetFieldValue(asset, field);
                }
                results.Add(row);
            }

            return results;
        }

        private object GetFieldValue(Asset asset, string field)
        {
            return field switch
            {
                "AssetNumber" => asset.AssetNumber ?? "",
                "Description" => asset.Description ?? "",
                "Location" => asset.LocationRef?.Name ?? "",
                "Department" => asset.DepartmentRef?.Name ?? "",
                "SerialNumber" => asset.SerialNumber ?? "",
                "Model" => asset.Model ?? "",
                "Vendor" => asset.VendorRef?.Name ?? "",
                "AcquisitionCost" => asset.AcquisitionCost,
                "AccumulatedDepreciation" => asset.AccumulatedDepreciation,
                "NetBookValue" => asset.AcquisitionCost - asset.AccumulatedDepreciation,
                "FairMarketValue" => asset.FairMarketValue ?? 0,
                "InServiceDate" => asset.InServiceDate.ToString("yyyy-MM-dd"),
                "UsefulLife" => asset.UsefulLifeMonths / 12,
                "Status" => asset.Status.ToString(),
                "Currency" => asset.Currency ?? "CAD",
                "Bay" => asset.Bay ?? "",
                _ => ""
            };
        }

        public async Task<DepreciationScheduleReport> GetDepreciationScheduleAsync(int year)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var assets = await _context.Assets
                .Where(a => a.Status == AssetStatus.Active)
                .Where(a => visibleIds.Contains(a.CompanyId ?? 0))
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();

            var report = new DepreciationScheduleReport
            {
                Year = year,
                GeneratedAt = DateTime.UtcNow,
                Items = new List<DepreciationScheduleItem>()
            };

            foreach (var asset in assets)
            {
                var monthlyDepr = asset.UsefulLifeMonths > 0 
                    ? (asset.AcquisitionCost - asset.SalvageValue) / asset.UsefulLifeMonths 
                    : 0;

                var item = new DepreciationScheduleItem
                {
                    AssetNumber = asset.AssetNumber ?? "",
                    Description = asset.Description ?? "",
                    AcquisitionCost = asset.AcquisitionCost,
                    AccumulatedDepreciationBOY = asset.AccumulatedDepreciation - (monthlyDepr * 12),
                    DepreciationExpense = monthlyDepr * 12,
                    AccumulatedDepreciationEOY = asset.AccumulatedDepreciation,
                    NetBookValue = asset.AcquisitionCost - asset.AccumulatedDepreciation
                };
                report.Items.Add(item);
            }

            report.TotalCost = report.Items.Sum(i => i.AcquisitionCost);
            report.TotalDepreciation = report.Items.Sum(i => i.DepreciationExpense);
            report.TotalNetBookValue = report.Items.Sum(i => i.NetBookValue);

            return report;
        }

        public async Task<TaxSummaryReport> GetTaxSummaryAsync(int year)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var assets = await _context.Assets.Where(a => visibleIds.Contains(a.CompanyId ?? 0)).ToListAsync();
            var ccaBalances = await _context.CcaClassBalances
                .Where(b => b.FiscalYear == year && visibleIds.Contains(b.CompanyId))
                .Include(b => b.CcaClass)
                .ToListAsync();

            var report = new TaxSummaryReport
            {
                Year = year,
                GeneratedAt = DateTime.UtcNow,
                TotalAssets = assets.Count,
                TotalCost = assets.Sum(a => a.AcquisitionCost),
                TotalAccumulatedDepreciation = assets.Sum(a => a.AccumulatedDepreciation),
                CcaClasses = new List<CcaSummaryItem>()
            };

            foreach (var balance in ccaBalances)
            {
                report.CcaClasses.Add(new CcaSummaryItem
                {
                    ClassName = balance.CcaClass != null ? $"Class {balance.CcaClass.ClassNumber}" : $"Class {balance.CcaClassId}",
                    Rate = balance.CcaClass?.Rate ?? 0,
                    OpeningUCC = balance.OpeningUcc,
                    Additions = balance.Additions,
                    Disposals = balance.Dispositions,
                    CcaClaimed = balance.CcaClaimed,
                    ClosingUCC = balance.ClosingUcc
                });
            }

            return report;
        }

        public async Task<AuditTrailReport> GetAuditTrailAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            var logs = await query.OrderByDescending(a => a.Timestamp).Take(500).ToListAsync();

            return new AuditTrailReport
            {
                GeneratedAt = DateTime.UtcNow,
                StartDate = startDate,
                EndDate = endDate,
                TotalEntries = logs.Count,
                Entries = logs.Select(l => new AuditEntry
                {
                    Timestamp = l.Timestamp,
                    EntityType = l.EntityType ?? "",
                    EntityId = l.EntityId?.ToString() ?? "",
                    Action = l.Action ?? "",
                    Username = l.Username ?? "",
                    Changes = l.Description ?? ""
                }).ToList()
            };
        }

        public List<string> GetAvailableFields()
        {
            return new List<string>
            {
                "AssetNumber", "Description", "Location", "Department", "Vendor",
                "SerialNumber", "Model", "Currency", "Bay",
                "AcquisitionCost", "AccumulatedDepreciation", "NetBookValue", "FairMarketValue",
                "InServiceDate", "UsefulLife", "Status"
            };
        }

        public async Task<List<string>> GetLocationsAsync()
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            return await _context.Locations
                .Where(l => l.IsActive && (l.Site == null || visibleIds.Contains(l.Site.CompanyId)))
                .OrderBy(l => l.SortOrder)
                .Select(l => l.Name)
                .ToListAsync();
        }
    }

    public class DepreciationScheduleReport
    {
        public int Year { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<DepreciationScheduleItem> Items { get; set; } = new();
        public decimal TotalCost { get; set; }
        public decimal TotalDepreciation { get; set; }
        public decimal TotalNetBookValue { get; set; }
    }

    public class DepreciationScheduleItem
    {
        public string AssetNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal AcquisitionCost { get; set; }
        public decimal AccumulatedDepreciationBOY { get; set; }
        public decimal DepreciationExpense { get; set; }
        public decimal AccumulatedDepreciationEOY { get; set; }
        public decimal NetBookValue { get; set; }
    }

    public class TaxSummaryReport
    {
        public int Year { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int TotalAssets { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalAccumulatedDepreciation { get; set; }
        public List<CcaSummaryItem> CcaClasses { get; set; } = new();
    }

    public class CcaSummaryItem
    {
        public string ClassName { get; set; } = "";
        public decimal Rate { get; set; }
        public decimal OpeningUCC { get; set; }
        public decimal Additions { get; set; }
        public decimal Disposals { get; set; }
        public decimal CcaClaimed { get; set; }
        public decimal ClosingUCC { get; set; }
    }

    public class AuditTrailReport
    {
        public DateTime GeneratedAt { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalEntries { get; set; }
        public List<AuditEntry> Entries { get; set; } = new();
    }

    public class AuditEntry
    {
        public DateTime Timestamp { get; set; }
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Action { get; set; } = "";
        public string Username { get; set; } = "";
        public string Changes { get; set; } = "";
    }
}
