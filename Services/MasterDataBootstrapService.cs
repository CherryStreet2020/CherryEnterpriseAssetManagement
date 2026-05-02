using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class SeedResult
    {
        public string Domain { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Success => Failed == 0;
    }

    public class BootstrapReport
    {
        public List<SeedResult> Results { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success => Results.All(r => r.Success);
        public int TotalInserted => Results.Sum(r => r.Inserted);
        public int TotalUpdated => Results.Sum(r => r.Updated);
        public int TotalFailed => Results.Sum(r => r.Failed);
    }

    public interface IMasterDataBootstrapService
    {
        Task<BootstrapReport> RunSystemReferenceSeedAsync();
        Task<BootstrapReport> RunCustomerMasterLoadAsync();
        Task<BootstrapReport> RunDemoSeedAsync();
        Task<SeedResult> ImportFromCsvAsync<T>(string csvContent, string domain) where T : class;
    }

    public class MasterDataBootstrapService : IMasterDataBootstrapService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MasterDataBootstrapService> _logger;

        public MasterDataBootstrapService(AppDbContext context, ILogger<MasterDataBootstrapService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BootstrapReport> RunSystemReferenceSeedAsync()
        {
            var report = new BootstrapReport { StartTime = DateTime.UtcNow };

            report.Results.Add(await SeedWorkOrderTypesAsync());
            report.Results.Add(await SeedFailureCodesAsync());
            report.Results.Add(await SeedCauseCodesAsync());
            report.Results.Add(await SeedPriorityLevelsAsync());
            report.Results.Add(await SeedCraftsAsync());
            report.Results.Add(await SeedNumberingSequencesAsync());
            report.Results.Add(await SeedPaymentTermsAsync());
            report.Results.Add(await SeedCurrenciesAsync());
            report.Results.Add(await SeedSection179LimitsAsync());
            report.Results.Add(await SeedBonusDepreciationRatesAsync());

            report.EndTime = DateTime.UtcNow;
            return report;
        }

        public async Task<BootstrapReport> RunCustomerMasterLoadAsync()
        {
            var report = new BootstrapReport { StartTime = DateTime.UtcNow };

            report.Results.Add(await SeedGlAccountsAsync());
            report.Results.Add(await SeedSitesAsync());
            report.Results.Add(await SeedDepartmentsAsync());
            report.Results.Add(await SeedCostCentersAsync());
            report.Results.Add(await SeedAssetCategoriesAsync());

            report.EndTime = DateTime.UtcNow;
            return report;
        }

        public async Task<BootstrapReport> RunDemoSeedAsync()
        {
            var report = new BootstrapReport { StartTime = DateTime.UtcNow };

            report.Results.Add(await SeedPMTemplatesAsync());

            report.EndTime = DateTime.UtcNow;
            return report;
        }

        public async Task<SeedResult> ImportFromCsvAsync<T>(string csvContent, string domain) where T : class
        {
            var result = new SeedResult { Domain = domain };

            try
            {
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2)
                {
                    result.Errors.Add("CSV must have header row and at least one data row");
                    result.Failed = 1;
                    return result;
                }

                var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
                result.TotalRecords = lines.Length - 1;

                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var values = ParseCsvLine(lines[i]);
                        if (values.Length != headers.Length)
                        {
                            result.Errors.Add($"Row {i}: Column count mismatch");
                            result.Failed++;
                            continue;
                        }

                        var data = new Dictionary<string, string>();
                        for (int j = 0; j < headers.Length; j++)
                        {
                            data[headers[j]] = values[j];
                        }

                        var (inserted, updated) = await UpsertRecordAsync<T>(data);
                        if (inserted) result.Inserted++;
                        else if (updated) result.Updated++;
                        else result.Skipped++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Row {i}: {ex.Message}");
                        result.Failed++;
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Import failed: {ex.Message}");
                result.Failed = result.TotalRecords;
            }

            return result;
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim());
            return result.ToArray();
        }

        private Task<(bool inserted, bool updated)> UpsertRecordAsync<T>(Dictionary<string, string> data) where T : class
        {
            return Task.FromResult((false, false));
        }

        private async Task<SeedResult> SeedWorkOrderTypesAsync()
        {
            var result = new SeedResult { Domain = "WorkOrderTypes" };
            var types = new[]
            {
                ("PM", "Preventive Maintenance", "Scheduled maintenance to prevent failures"),
                ("CM", "Corrective Maintenance", "Repair after failure or issue discovered"),
                ("PDM", "Predictive Maintenance", "Condition-based maintenance"),
                ("EM", "Emergency Maintenance", "Urgent unplanned repairs"),
                ("PRJ", "Project Work", "Capital improvement or modification"),
                ("INSP", "Inspection", "Regulatory or safety inspection"),
                ("CAL", "Calibration", "Instrument calibration"),
                ("SAF", "Safety Work", "LOTO or safety-related work"),
                ("INST", "Installation", "New equipment installation"),
                ("DEMO", "Demolition/Removal", "Equipment removal or decommission"),
                ("RELO", "Relocation", "Equipment move or transfer"),
                ("MOD", "Modification", "Equipment modification or upgrade")
            };

            result.TotalRecords = types.Length;

            foreach (var (code, name, desc) in types)
            {
                var existing = await _context.WorkOrderTypes.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.WorkOrderTypes.Add(new WorkOrderType { Code = code, Name = name, Description = desc, IsActive = true });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedFailureCodesAsync()
        {
            var result = new SeedResult { Domain = "FailureCodes" };
            var codes = new[]
            {
                ("MECH-WEAR", "Mechanical Wear", "Component worn beyond tolerance"),
                ("MECH-BREAK", "Mechanical Breakage", "Component broke or fractured"),
                ("MECH-MISALIGN", "Misalignment", "Shaft or coupling misalignment"),
                ("ELEC-SHORT", "Electrical Short", "Short circuit or ground fault"),
                ("ELEC-OPEN", "Open Circuit", "Wire break or loose connection"),
                ("ELEC-OVERLOAD", "Electrical Overload", "Motor or circuit overloaded"),
                ("ELEC-CTRL", "Control Failure", "PLC or control system failure"),
                ("HYD-LEAK", "Hydraulic Leak", "Fluid leak in hydraulic system"),
                ("HYD-PUMP", "Hydraulic Pump Failure", "Pump failure or degradation"),
                ("PNEU-LEAK", "Pneumatic Leak", "Air leak in pneumatic system"),
                ("PNEU-VALVE", "Pneumatic Valve Failure", "Valve stuck or failed"),
                ("LUBR-LACK", "Lack of Lubrication", "Insufficient lubrication"),
                ("LUBR-CONTAM", "Lubricant Contamination", "Contaminated lubricant"),
                ("STRUC-CRACK", "Structural Crack", "Crack in frame or structure"),
                ("STRUC-CORR", "Corrosion", "Corrosion or rust damage"),
                ("INST-CAL", "Calibration Drift", "Instrument out of calibration"),
                ("INST-SENSOR", "Sensor Failure", "Sensor malfunction"),
                ("SOFT-ERROR", "Software Error", "Program or logic error"),
                ("SOFT-COMM", "Communication Failure", "Network or protocol failure"),
                ("OPER-ABUSE", "Operator Abuse", "Improper operation"),
                ("OPER-OVERLOAD", "Operational Overload", "Exceeded capacity"),
                ("ENV-TEMP", "Temperature Extreme", "Heat or cold damage"),
                ("ENV-CONTAM", "Environmental Contamination", "Dust, dirt, or debris"),
                ("UNK", "Unknown", "Root cause undetermined")
            };

            result.TotalRecords = codes.Length;

            foreach (var (code, name, desc) in codes)
            {
                var existing = await _context.FailureCodes.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.FailureCodes.Add(new FailureCode { Code = code, Name = name, Description = desc, IsActive = true });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedCauseCodesAsync()
        {
            var result = new SeedResult { Domain = "CauseCodes" };
            var codes = new[]
            {
                ("WEAR", "Normal Wear", "End of component life"),
                ("MAINT-LACK", "Lack of Maintenance", "PM not performed"),
                ("MAINT-IMPROPER", "Improper Maintenance", "Incorrect PM procedures"),
                ("MISALIGN", "Misalignment", "Installation or operational misalignment"),
                ("OVERLOAD", "Overload", "Exceeded design capacity"),
                ("CONTAM", "Contamination", "Foreign material intrusion"),
                ("CORR", "Corrosion", "Chemical or environmental corrosion"),
                ("FATIGUE", "Fatigue", "Cyclic stress failure"),
                ("HEAT", "Overheating", "Thermal damage"),
                ("VIBR", "Excessive Vibration", "Vibration-induced damage"),
                ("DESIGN", "Design Deficiency", "Inherent design flaw"),
                ("INSTALL", "Installation Error", "Improper installation"),
                ("OPER-ERROR", "Operator Error", "Human error in operation"),
                ("MATERIAL", "Material Defect", "Manufacturing or material defect"),
                ("AGE", "Age Degradation", "Time-based deterioration"),
                ("POWER", "Power Quality", "Voltage sag, surge, or transient"),
                ("UNK", "Unknown", "Cause undetermined")
            };

            result.TotalRecords = codes.Length;

            foreach (var (code, name, desc) in codes)
            {
                var existing = await _context.CauseCodes.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.CauseCodes.Add(new CauseCode { Code = code, Name = name, Description = desc, IsActive = true });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedPriorityLevelsAsync()
        {
            var result = new SeedResult { Domain = "PriorityLevels" };
            var levels = new[]
            {
                ("P1", "Emergency", 1, 4, "Safety or production critical - immediate response"),
                ("P2", "Urgent", 2, 24, "Major impact - respond within 24 hours"),
                ("P3", "High", 3, 72, "Significant impact - respond within 3 days"),
                ("P4", "Medium", 4, 168, "Moderate impact - respond within 1 week"),
                ("P5", "Low", 5, 336, "Minor impact - respond within 2 weeks"),
                ("P6", "Planned", 6, 720, "No immediate impact - schedule as available")
            };

            result.TotalRecords = levels.Length;

            foreach (var (code, name, sortOrder, slaHours, desc) in levels)
            {
                var existing = await _context.PriorityLevels.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.PriorityLevels.Add(new PriorityLevel 
                    { 
                        Code = code, 
                        Name = name, 
                        Description = desc, 
                        Level = sortOrder,
                        ResponseTimeHours = slaHours,
                        IsActive = true 
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedCraftsAsync()
        {
            var result = new SeedResult { Domain = "Crafts" };
            var crafts = new[]
            {
                ("MECH", "Mechanic", 45.00m),
                ("ELEC", "Electrician", 50.00m),
                ("WELD", "Welder", 48.00m),
                ("PIPE", "Pipefitter", 46.00m),
                ("HVAC", "HVAC Technician", 52.00m),
                ("INST", "Instrument Technician", 55.00m),
                ("PLC", "PLC Programmer", 60.00m),
                ("MILL", "Millwright", 50.00m),
                ("MACH", "Machinist", 48.00m),
                ("LUBE", "Lubrication Technician", 38.00m),
                ("OPER", "Operator", 35.00m),
                ("HELP", "Helper", 28.00m),
                ("LEAD", "Lead Technician", 58.00m),
                ("SUPV", "Supervisor", 65.00m),
                ("ENGR", "Engineer", 75.00m)
            };

            result.TotalRecords = crafts.Length;

            foreach (var (code, name, rate) in crafts)
            {
                var existing = await _context.Crafts.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.Crafts.Add(new Craft 
                    { 
                        Code = code, 
                        Name = name, 
                        DefaultHourlyRate = rate,
                        IsActive = true 
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedNumberingSequencesAsync()
        {
            var result = new SeedResult { Domain = "NumberingSequences" };
            var sequences = new[]
            {
                ("PO", "PO-", 1, "Purchase Orders"),
                ("PR", "PR-", 1, "Purchase Requisitions"),
                ("WO", "WO-", 1, "Work Orders"),
                ("INV", "INV-", 1, "Vendor Invoices"),
                ("GR", "GR-", 1, "Goods Receipts"),
                ("AST", "AST-", 1, "Assets"),
                ("JE", "JE-", 1, "Journal Entries"),
                ("PAY", "PAY-", 1, "Payments"),
                ("CIP", "CIP-", 1, "CIP Projects"),
                ("MR", "MR-", 1, "Material Requests")
            };

            result.TotalRecords = sequences.Length;

            foreach (var (code, prefix, next, name) in sequences)
            {
                var existing = await _context.NumberingSequences.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.NumberingSequences.Add(new NumberingSequence 
                    { 
                        Code = code, 
                        Name = name,
                        Prefix = prefix, 
                        NextNumber = next,
                        IsActive = true 
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedPaymentTermsAsync()
        {
            var result = new SeedResult { Domain = "PaymentTerms" };
            var terms = new[]
            {
                ("NET10", "Net 10", 10, 0m),
                ("NET15", "Net 15", 15, 0m),
                ("NET30", "Net 30", 30, 0m),
                ("NET45", "Net 45", 45, 0m),
                ("NET60", "Net 60", 60, 0m),
                ("NET90", "Net 90", 90, 0m),
                ("2/10NET30", "2% 10 Net 30", 30, 2m),
                ("1/10NET30", "1% 10 Net 30", 30, 1m),
                ("COD", "Cash on Delivery", 0, 0m),
                ("CIA", "Cash in Advance", -1, 0m),
                ("EOM", "End of Month", 30, 0m),
                ("15MFI", "15th of Month Following Invoice", 45, 0m),
                ("DOR", "Due on Receipt", 0, 0m)
            };

            result.TotalRecords = terms.Length;

            foreach (var (code, name, days, discount) in terms)
            {
                var existing = await _context.PaymentTerms.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.PaymentTerms.Add(new PaymentTerm 
                    { 
                        Code = code, 
                        Name = name, 
                        DueDays = days,
                        DiscountPercent = discount,
                        IsActive = true 
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedCurrenciesAsync()
        {
            var result = new SeedResult { Domain = "Currencies" };
            var currencies = new[]
            {
                ("USD", "US Dollar", "$", 2),
                ("CAD", "Canadian Dollar", "C$", 2),
                ("EUR", "Euro", "€", 2),
                ("GBP", "British Pound", "£", 2),
                ("MXN", "Mexican Peso", "MX$", 2),
                ("JPY", "Japanese Yen", "¥", 0),
                ("CNY", "Chinese Yuan", "¥", 2),
                ("CHF", "Swiss Franc", "CHF", 2),
                ("AUD", "Australian Dollar", "A$", 2),
                ("INR", "Indian Rupee", "₹", 2)
            };

            result.TotalRecords = currencies.Length;

            foreach (var (code, name, symbol, decimals) in currencies)
            {
                var existing = await _context.Currencies.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.Currencies.Add(new Currency 
                    { 
                        Code = code, 
                        Name = name, 
                        Symbol = symbol,
                        DecimalPlaces = decimals,
                        IsActive = true 
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedSection179LimitsAsync()
        {
            var result = new SeedResult { Domain = "Section179Limits" };
            var limits = new[]
            {
                (2020, 1040000m, 2590000m),
                (2021, 1050000m, 2620000m),
                (2022, 1080000m, 2700000m),
                (2023, 1160000m, 2890000m),
                (2024, 1220000m, 3050000m),
                (2025, 1250000m, 3130000m),
                (2026, 1280000m, 3200000m)
            };

            result.TotalRecords = limits.Length;

            foreach (var (year, maxDeduction, phaseout) in limits)
            {
                var existing = await _context.Section179Limits.FirstOrDefaultAsync(x => x.TaxYear == year);
                if (existing == null)
                {
                    _context.Section179Limits.Add(new Section179Limits 
                    { 
                        TaxYear = year, 
                        MaxDeduction = maxDeduction, 
                        PhaseoutThreshold = phaseout
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedBonusDepreciationRatesAsync()
        {
            var result = new SeedResult { Domain = "BonusDepreciationRates" };
            var rates = new[]
            {
                (2020, 100m),
                (2021, 100m),
                (2022, 100m),
                (2023, 80m),
                (2024, 60m),
                (2025, 40m),
                (2026, 20m),
                (2027, 0m)
            };

            result.TotalRecords = rates.Length;

            foreach (var (year, rate) in rates)
            {
                var existing = await _context.BonusDepreciationRates.FirstOrDefaultAsync(x => x.TaxYear == year);
                if (existing == null)
                {
                    _context.BonusDepreciationRates.Add(new BonusDepreciationRates 
                    { 
                        TaxYear = year, 
                        Rate = rate
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedGlAccountsAsync()
        {
            var result = new SeedResult { Domain = "GlAccounts" };
            
            var accounts = new[]
            {
                ("1000", "Cash", GlAccountType.Asset, GlAccountCategory.CashAndReceivables, NormalBalance.Debit),
                ("1100", "Accounts Receivable", GlAccountType.Asset, GlAccountCategory.CashAndReceivables, NormalBalance.Debit),
                ("1200", "Inventory", GlAccountType.Asset, GlAccountCategory.MroInventory, NormalBalance.Debit),
                ("1500", "Fixed Assets", GlAccountType.Asset, GlAccountCategory.FixedAssetsMachinery, NormalBalance.Debit),
                ("1510", "Machinery & Equipment", GlAccountType.Asset, GlAccountCategory.FixedAssetsMachinery, NormalBalance.Debit),
                ("1520", "Vehicles", GlAccountType.Asset, GlAccountCategory.FixedAssetsVehicles, NormalBalance.Debit),
                ("1530", "Furniture & Fixtures", GlAccountType.Asset, GlAccountCategory.FixedAssetsMachinery, NormalBalance.Debit),
                ("1540", "Computer Equipment", GlAccountType.Asset, GlAccountCategory.FixedAssetsTechnology, NormalBalance.Debit),
                ("1550", "Buildings", GlAccountType.Asset, GlAccountCategory.FixedAssetsLandBuildings, NormalBalance.Debit),
                ("1560", "Land", GlAccountType.Asset, GlAccountCategory.FixedAssetsLandBuildings, NormalBalance.Debit),
                ("1570", "Leasehold Improvements", GlAccountType.Asset, GlAccountCategory.FixedAssetsLandBuildings, NormalBalance.Debit),
                ("1580", "Construction in Progress", GlAccountType.Asset, GlAccountCategory.WorkInProgress, NormalBalance.Debit),
                ("1600", "Accumulated Depreciation", GlAccountType.ContraAsset, GlAccountCategory.AccumulatedDepreciation, NormalBalance.Credit),
                ("1610", "Accum Depr - Machinery", GlAccountType.ContraAsset, GlAccountCategory.AccumulatedDepreciation, NormalBalance.Credit),
                ("1620", "Accum Depr - Vehicles", GlAccountType.ContraAsset, GlAccountCategory.AccumulatedDepreciation, NormalBalance.Credit),
                ("1630", "Accum Depr - Furniture", GlAccountType.ContraAsset, GlAccountCategory.AccumulatedDepreciation, NormalBalance.Credit),
                ("1640", "Accum Depr - Computers", GlAccountType.ContraAsset, GlAccountCategory.AccumulatedDepreciation, NormalBalance.Credit),
                ("1650", "Accum Depr - Buildings", GlAccountType.ContraAsset, GlAccountCategory.AccumulatedDepreciation, NormalBalance.Credit),
                ("2000", "Accounts Payable", GlAccountType.Liability, GlAccountCategory.CurrentLiabilities, NormalBalance.Credit),
                ("2100", "Accrued Liabilities", GlAccountType.Liability, GlAccountCategory.CurrentLiabilities, NormalBalance.Credit),
                ("3000", "Common Stock", GlAccountType.Equity, GlAccountCategory.Equity, NormalBalance.Credit),
                ("3100", "Retained Earnings", GlAccountType.Equity, GlAccountCategory.Equity, NormalBalance.Credit),
                ("4000", "Revenue", GlAccountType.Revenue, GlAccountCategory.RevenueAndGains, NormalBalance.Credit),
                ("5000", "Cost of Goods Sold", GlAccountType.Expense, GlAccountCategory.CostOfSales, NormalBalance.Debit),
                ("6000", "Operating Expenses", GlAccountType.Expense, GlAccountCategory.OperatingExpenses, NormalBalance.Debit),
                ("6100", "Depreciation Expense", GlAccountType.Expense, GlAccountCategory.DepreciationExpense, NormalBalance.Debit),
                ("6200", "Maintenance Expense", GlAccountType.Expense, GlAccountCategory.MaintenanceLabor, NormalBalance.Debit),
                ("6300", "Utilities Expense", GlAccountType.Expense, GlAccountCategory.UtilitiesInfrastructure, NormalBalance.Debit),
                ("7000", "Gain on Asset Sale", GlAccountType.Revenue, GlAccountCategory.RevenueAndGains, NormalBalance.Credit),
                ("7100", "Loss on Asset Disposal", GlAccountType.Expense, GlAccountCategory.AssetLosses, NormalBalance.Debit)
            };

            var company = await _context.Companies.FirstOrDefaultAsync();
            if (company == null)
            {
                result.Errors.Add("No company exists - cannot seed GL accounts");
                result.Failed = accounts.Length;
                return result;
            }

            result.TotalRecords = accounts.Length;

            foreach (var (acctNum, name, type, category, balance) in accounts)
            {
                var existing = await _context.GlAccounts.FirstOrDefaultAsync(x => x.AccountNumber == acctNum && x.CompanyId == company.Id);
                if (existing == null)
                {
                    _context.GlAccounts.Add(new GlAccount 
                    { 
                        AccountNumber = acctNum, 
                        Name = name, 
                        AccountType = type,
                        Category = category,
                        NormalBalance = balance,
                        CompanyId = company.Id,
                        IsActive = true 
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedSitesAsync()
        {
            var result = new SeedResult { Domain = "Sites" };
            
            var company = await _context.Companies.FirstOrDefaultAsync();
            if (company == null)
            {
                result.Errors.Add("No company exists - cannot seed sites");
                return result;
            }

            var sites = new[]
            {
                ("MAIN", "Main Manufacturing Plant", SiteType.Manufacturing),
                ("WHSE", "Central Warehouse", SiteType.Warehouse),
                ("HQ", "Corporate Headquarters", SiteType.Office),
                ("DC01", "Distribution Center East", SiteType.Distribution),
                ("SVC01", "Service Center", SiteType.ServiceCenter)
            };

            result.TotalRecords = sites.Length;

            foreach (var (code, name, type) in sites)
            {
                var existing = await _context.Sites.FirstOrDefaultAsync(x => x.SiteCode == code);
                if (existing == null)
                {
                    _context.Sites.Add(new Site 
                    { 
                        SiteCode = code, 
                        Name = name, 
                        Type = type,
                        Status = SiteStatus.Active,
                        CompanyId = company.Id
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedDepartmentsAsync()
        {
            var result = new SeedResult { Domain = "Departments" };
            var depts = new[]
            {
                ("MAINT", "Maintenance"),
                ("PROD", "Production"),
                ("QA", "Quality Assurance"),
                ("ENG", "Engineering"),
                ("ADMIN", "Administration"),
                ("IT", "Information Technology"),
                ("FACIL", "Facilities"),
                ("SAFE", "Safety & Environmental"),
                ("WHSE", "Warehouse"),
                ("SHIP", "Shipping & Receiving")
            };

            result.TotalRecords = depts.Length;

            foreach (var (code, name) in depts)
            {
                var existing = await _context.Departments.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.Departments.Add(new Department { Code = code, Name = name, IsActive = true });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedCostCentersAsync()
        {
            var result = new SeedResult { Domain = "CostCenters" };
            var centers = new[]
            {
                ("CC100", "Production Line 1"),
                ("CC200", "Production Line 2"),
                ("CC300", "Packaging"),
                ("CC400", "Warehouse Operations"),
                ("CC500", "Plant Maintenance"),
                ("CC600", "Quality Control"),
                ("CC700", "Engineering Support"),
                ("CC800", "General & Administrative"),
                ("CC900", "Research & Development")
            };

            result.TotalRecords = centers.Length;

            foreach (var (code, name) in centers)
            {
                var existing = await _context.CostCenters.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.CostCenters.Add(new CostCenter { Code = code, Name = name, IsActive = true });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedAssetCategoriesAsync()
        {
            var result = new SeedResult { Domain = "AssetCategories" };
            var categories = new[]
            {
                ("MACH", "Machinery & Equipment", "1510", "1610", 7),
                ("VEH", "Vehicles", "1520", "1620", 5),
                ("FURN", "Furniture & Fixtures", "1530", "1630", 7),
                ("COMP", "Computer Equipment", "1540", "1640", 5),
                ("BLDG", "Buildings", "1550", "1650", 39),
                ("LAND", "Land", "1560", null, 0),
                ("LHLD", "Leasehold Improvements", "1570", "1650", 15),
                ("TOOL", "Tools & Dies", "1510", "1610", 3),
                ("ELEC", "Electrical Equipment", "1510", "1610", 7),
                ("HVAC", "HVAC Equipment", "1510", "1610", 15),
                ("SAFE", "Safety Equipment", "1510", "1610", 5),
                ("SOFT", "Software", "1540", "1640", 3)
            };

            result.TotalRecords = categories.Length;

            foreach (var (code, name, assetGl, accumGl, life) in categories)
            {
                var existing = await _context.AssetCategories.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.AssetCategories.Add(new AssetCategory 
                    { 
                        Code = code, 
                        Name = name,
                        DefaultUsefulLifeMonths = life
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task<SeedResult> SeedPMTemplatesAsync()
        {
            var result = new SeedResult { Domain = "PMTemplates" };
            var templates = new[]
            {
                ("PM-MOTOR-30D", "Motor PM - Monthly", MaintenanceType.Preventative, PMTriggerType.Calendar, RecurrenceType.Monthly, 1),
                ("PM-MOTOR-90D", "Motor PM - Quarterly", MaintenanceType.Preventative, PMTriggerType.Calendar, RecurrenceType.Quarterly, 1),
                ("PM-PUMP-WK", "Pump Inspection - Weekly", MaintenanceType.Inspection, PMTriggerType.Calendar, RecurrenceType.Weekly, 1),
                ("PM-HVAC-MO", "HVAC Filter Change - Monthly", MaintenanceType.Preventative, PMTriggerType.Calendar, RecurrenceType.Monthly, 1),
                ("PM-VEH-OIL", "Vehicle Oil Change", MaintenanceType.Preventative, PMTriggerType.Meter, RecurrenceType.Custom, 0),
                ("CAL-INST-YR", "Annual Instrument Calibration", MaintenanceType.Calibration, PMTriggerType.Calendar, RecurrenceType.Annually, 1),
                ("INSP-SAFE-QTR", "Quarterly Safety Inspection", MaintenanceType.Inspection, PMTriggerType.Calendar, RecurrenceType.Quarterly, 1),
                ("PM-CONV-WK", "Conveyor Belt Inspection", MaintenanceType.Inspection, PMTriggerType.Calendar, RecurrenceType.Weekly, 1)
            };

            result.TotalRecords = templates.Length;

            foreach (var (code, name, type, trigger, interval, intervalValue) in templates)
            {
                var existing = await _context.PMTemplates.FirstOrDefaultAsync(x => x.Code == code);
                if (existing == null)
                {
                    _context.PMTemplates.Add(new PMTemplate 
                    { 
                        Code = code, 
                        Name = name,
                        Type = type,
                        TriggerType = trigger,
                        CalendarInterval = interval,
                        CalendarIntervalValue = intervalValue,
                        IsActive = true 
                    });
                    result.Inserted++;
                }
                else
                {
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }
    }
}
