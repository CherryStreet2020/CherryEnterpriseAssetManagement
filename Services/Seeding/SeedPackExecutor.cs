using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Seeding
{
    public interface ISeedPackExecutor
    {
        Task<SeedPackResult> ExecuteAsync(SeedPack pack);
    }

    public class SeedPackResult
    {
        public bool Success { get; set; }
        public string PackName { get; set; } = string.Empty;
        public string PackId { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, SeedPackTableResult> TableResults { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        
        public int TotalInserted => TableResults.Values.Sum(t => t.Inserted);
        public int TotalSkipped => TableResults.Values.Sum(t => t.Skipped);
    }

    public class SeedPackTableResult
    {
        public string TableName { get; set; } = string.Empty;
        public int TargetCount { get; set; }
        public int ExistingCount { get; set; }
        public int Inserted { get; set; }
        public int Skipped { get; set; }
    }

    public class SeedPackExecutor : ISeedPackExecutor
    {
        private readonly AppDbContext _context;
        private readonly ISeedGuardService _guardService;
        private static readonly Random _random = new Random(42);

        public SeedPackExecutor(AppDbContext context, ISeedGuardService guardService)
        {
            _context = context;
            _guardService = guardService;
        }

        public async Task<SeedPackResult> ExecuteAsync(SeedPack pack)
        {
            var startTime = DateTime.UtcNow;
            var result = new SeedPackResult
            {
                PackName = pack.Name,
                PackId = pack.Id,
                ExecutedAt = startTime
            };

            var guardCheck = _guardService.CheckSeedPermission();
            if (!guardCheck.Allowed)
            {
                result.Success = false;
                result.Errors.Add(guardCheck.Reason);
                result.Duration = DateTime.UtcNow - startTime;
                return result;
            }

            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                result.TableResults["Books"] = await SeedBooksAsync();
                result.TableResults["Companies"] = await SeedCompaniesAsync(pack.CompanyCount);
                result.TableResults["Sites"] = await SeedSitesAsync(pack.SiteCount);
                result.TableResults["Locations"] = await SeedLocationsAsync(pack.LocationCount);
                result.TableResults["Vendors"] = await SeedVendorsAsync(pack.VendorCount);
                result.TableResults["Technicians"] = await SeedTechniciansAsync(pack.TechnicianCount);
                result.TableResults["DepreciationPolicies"] = await SeedDepreciationPoliciesAsync();
                result.TableResults["Assets"] = await SeedAssetsAsync(pack.AssetCount);
                result.TableResults["PMTemplates"] = await SeedPMTemplatesAsync(pack.PMTemplateCount);
                result.TableResults["PMTemplateAssets"] = await SeedPMTemplateAssetsAsync();
                result.TableResults["PMSchedules"] = await SeedPMSchedulesAsync();
                result.TableResults["MaintenanceEvents"] = await SeedMaintenanceEventsAsync(pack.MaintenanceEventCount);
                result.TableResults["MaintenanceSchedules"] = await SeedMaintenanceSchedulesAsync(pack.MaintenanceScheduleCount);
                result.TableResults["ScheduledEvents"] = await GenerateScheduledEventsAsync();

                await transaction.CommitAsync();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Seed pack execution failed: {ex.Message}");
            }

            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }

        private async Task<SeedPackTableResult> SeedBooksAsync()
        {
            var existing = await _context.Books.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "Books", TargetCount = 2, ExistingCount = existing };

            if (existing >= 2)
            {
                tableResult.Skipped = 2;
                return tableResult;
            }

            var books = new[]
            {
                new Book { Code = "GAAP", Name = "Financial (GAAP)", GlAccountAccumDep = "16010", GlAccountDepExp = "74010" },
                new Book { Code = "TAX", Name = "Tax", GlAccountAccumDep = "16010", GlAccountDepExp = "74010" }
            };

            foreach (var book in books)
            {
                if (!await _context.Books.AnyAsync(b => b.Code == book.Code))
                {
                    _context.Books.Add(book);
                    tableResult.Inserted++;
                }
                else
                {
                    tableResult.Skipped++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedCompaniesAsync(int targetCount)
        {
            var existing = await _context.Companies.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "Companies", TargetCount = targetCount, ExistingCount = existing };

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var companies = new List<(string Code, string Name, string Country, string Currency, int? ParentId)>
            {
                ("PWH", "PRESTIGE WORLDWIDE HOLDINGS", "USA", "USD", null),
                ("PWH-CAN", "PWH MANUFACTURING CANADA", "Canada", "CAD", 1),
                ("PWH-USA", "PWH MANUFACTURING USA", "USA", "USD", 1)
            };

            foreach (var (code, name, country, currency, parentId) in companies.Take(targetCount))
            {
                if (!await _context.Companies.AnyAsync(c => c.CompanyCode == code))
                {
                    _context.Companies.Add(new Company
                    {
                        CompanyCode = code,
                        Name = name,
                        Country = country,
                        Currency = currency,
                        IsActive = true,
                        ParentCompanyId = parentId
                    });
                    tableResult.Inserted++;
                }
                else
                {
                    tableResult.Skipped++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedSitesAsync(int targetCount)
        {
            var existing = await _context.Sites.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "Sites", TargetCount = targetCount, ExistingCount = existing };

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var companies = await _context.Companies.OrderBy(c => c.Id).ToListAsync();
            if (!companies.Any()) return tableResult;

            var sites = new List<(string Code, string Name, string City, string Country)>
            {
                ("MAIN", "MAIN MANUFACTURING PLANT", "DETROIT", "USA"),
                ("NORTH", "NORTH ASSEMBLY FACILITY", "TORONTO", "CANADA"),
                ("SOUTH", "SOUTH DISTRIBUTION CENTER", "ATLANTA", "USA"),
                ("EAST", "EAST MACHINING CENTER", "BOSTON", "USA"),
                ("WEST", "WEST FINISHING PLANT", "PHOENIX", "USA")
            };

            foreach (var (code, name, city, country) in sites.Take(targetCount))
            {
                if (!await _context.Sites.AnyAsync(s => s.SiteCode == code))
                {
                    var company = companies.FirstOrDefault(c => c.Country == country) ?? companies.First();
                    _context.Sites.Add(new Site
                    {
                        SiteCode = code,
                        Name = name,
                        City = city,
                        Country = country,
                        CompanyId = company.Id,
                        Status = SiteStatus.Active
                    });
                    tableResult.Inserted++;
                }
                else
                {
                    tableResult.Skipped++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedLocationsAsync(int targetCount)
        {
            var existing = await _context.Locations.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "Locations", TargetCount = targetCount, ExistingCount = existing };

            var sites = await _context.Sites.OrderBy(s => s.Id).ToListAsync();
            if (!sites.Any()) return tableResult;

            // Backfill: Fix existing locations with null CompanyId
            var locationsToFix = await _context.Locations
                .Where(l => l.CompanyId == null && l.SiteId != null)
                .ToListAsync();
            foreach (var loc in locationsToFix)
            {
                var site = sites.FirstOrDefault(s => s.Id == loc.SiteId);
                if (site != null)
                {
                    loc.CompanyId = site.CompanyId;
                }
            }
            if (locationsToFix.Any())
            {
                await _context.SaveChangesAsync();
            }

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var locationTemplates = new[]
            {
                "MACHINING AREA A", "MACHINING AREA B", "ASSEMBLY LINE 1", "ASSEMBLY LINE 2",
                "WAREHOUSE A", "WAREHOUSE B", "MAINTENANCE SHOP", "TOOL CRIB",
                "SHIPPING DOCK", "RECEIVING DOCK", "QC LAB", "PAINT BOOTH",
                "WELDING STATION 1", "WELDING STATION 2", "CNC CELL 1", "CNC CELL 2",
                "PRESS AREA", "GRINDING AREA", "HEAT TREAT", "PACKAGING LINE",
                "ADMIN OFFICES"
            };

            int locIndex = 0;
            foreach (var site in sites)
            {
                var locsPerSite = Math.Min(targetCount / sites.Count + 1, locationTemplates.Length);
                for (int i = 0; i < locsPerSite && locIndex < targetCount; i++)
                {
                    var locCode = $"{site.SiteCode}-{(i + 1):D2}";
                    if (!await _context.Locations.AnyAsync(l => l.Code == locCode))
                    {
                        _context.Locations.Add(new Location
                        {
                            Code = locCode,
                            Name = locationTemplates[i % locationTemplates.Length],
                            SiteId = site.Id,
                            CompanyId = site.CompanyId // CRITICAL: stamp CompanyId for tenant-scoped queries
                        });
                        tableResult.Inserted++;
                    }
                    else
                    {
                        tableResult.Skipped++;
                    }
                    locIndex++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedVendorsAsync(int targetCount)
        {
            var existing = await _context.Vendors.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "Vendors", TargetCount = targetCount, ExistingCount = existing };

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var vendors = new[]
            {
                ("SKF001", "SKF BEARINGS", "TORONTO", "ON", "CANADA"),
                ("FAG001", "FAG BEARINGS", "DETROIT", "MI", "USA"),
                ("GRA001", "GRAINGER INDUSTRIAL", "LAKE FOREST", "IL", "USA"),
                ("MSC001", "MSC INDUSTRIAL SUPPLY", "MELVILLE", "NY", "USA"),
                ("FAI001", "FASTENAL COMPANY", "WINONA", "MN", "USA"),
                ("HAA001", "HAAS AUTOMATION", "OXNARD", "CA", "USA"),
                ("MAZ001", "MAZAK CORPORATION", "FLORENCE", "KY", "USA"),
                ("SAN001", "SANDVIK TOOLING", "FAIR LAWN", "NJ", "USA"),
                ("KEN001", "KENNAMETAL INC", "LATROBE", "PA", "USA"),
                ("LIN001", "LINCOLN ELECTRIC", "CLEVELAND", "OH", "USA")
            };

            foreach (var (code, name, city, state, country) in vendors.Take(targetCount))
            {
                if (!await _context.Vendors.AnyAsync(v => v.Code == code))
                {
                    _context.Vendors.Add(new Vendor
                    {
                        Code = code,
                        Name = name,
                        City = city,
                        State = state,
                        Country = country,
                        IsActive = true
                    });
                    tableResult.Inserted++;
                }
                else
                {
                    tableResult.Skipped++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedTechniciansAsync(int targetCount)
        {
            var existing = await _context.Technicians.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "Technicians", TargetCount = targetCount, ExistingCount = existing };

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var technicians = new[]
            {
                ("JOHN SMITH", "SENIOR MAINTENANCE", 45.00m),
                ("MARIA GARCIA", "ELECTRICIAN", 48.00m),
                ("DAVID CHEN", "CNC SPECIALIST", 52.00m),
                ("SARAH JOHNSON", "HVAC TECHNICIAN", 44.00m),
                ("MICHAEL BROWN", "WELDER", 46.00m)
            };

            foreach (var (name, specialty, rate) in technicians.Take(targetCount))
            {
                if (!await _context.Technicians.AnyAsync(t => t.Name == name))
                {
                    _context.Technicians.Add(new Technician
                    {
                        Name = name,
                        Specialty = specialty,
                        HourlyRate = rate
                    });
                    tableResult.Inserted++;
                }
                else
                {
                    tableResult.Skipped++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedDepreciationPoliciesAsync()
        {
            var existing = await _context.DepreciationPolicies.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "DepreciationPolicies", TargetCount = 10, ExistingCount = existing };

            if (existing >= 10)
            {
                tableResult.Skipped = 10;
                return tableResult;
            }

            var policies = new[]
            {
                ("SL-5", "STRAIGHT LINE 5 YEAR", DepreciationMethod.StraightLine, 60),
                ("SL-7", "STRAIGHT LINE 7 YEAR", DepreciationMethod.StraightLine, 84),
                ("SL-10", "STRAIGHT LINE 10 YEAR", DepreciationMethod.StraightLine, 120),
                ("SL-15", "STRAIGHT LINE 15 YEAR", DepreciationMethod.StraightLine, 180),
                ("DDB-5", "DOUBLE DECLINING 5 YEAR", DepreciationMethod.DoubleDecliningBalance, 60),
                ("DDB-7", "DOUBLE DECLINING 7 YEAR", DepreciationMethod.DoubleDecliningBalance, 84),
                ("MACRS-5", "MACRS 5 YEAR PROPERTY", DepreciationMethod.MACRS, 60),
                ("MACRS-7", "MACRS 7 YEAR PROPERTY", DepreciationMethod.MACRS, 84),
                ("SYD-5", "SUM OF YEARS DIGITS 5 YEAR", DepreciationMethod.SumOfYearsDigits, 60),
                ("SYD-7", "SUM OF YEARS DIGITS 7 YEAR", DepreciationMethod.SumOfYearsDigits, 84)
            };

            foreach (var (code, name, method, life) in policies)
            {
                if (!await _context.DepreciationPolicies.AnyAsync(p => p.Code == code))
                {
                    _context.DepreciationPolicies.Add(new DepreciationPolicy
                    {
                        Code = code,
                        Name = name,
                        Method = method,
                        DefaultUsefulLifeMonths = life,
                        Convention = DepreciationConvention.HalfYear
                    });
                    tableResult.Inserted++;
                }
                else
                {
                    tableResult.Skipped++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedAssetsAsync(int targetCount)
        {
            var existing = await _context.Assets.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "Assets", TargetCount = targetCount, ExistingCount = existing };

            var companies = await _context.Companies.OrderBy(c => c.Id).ToListAsync();
            var sites = await _context.Sites.ToListAsync();
            var locations = await _context.Locations.ToListAsync();

            if (!companies.Any())
                return tableResult;

            // Backfill: Fix existing assets with null or inconsistent CompanyId/SiteId/LocationId
            var assetsToFix = await _context.Assets
                .Where(a => a.CompanyId == null || a.SiteId == null)
                .ToListAsync();
            foreach (var asset in assetsToFix)
            {
                // Infer CompanyId from SiteId if possible
                if (asset.CompanyId == null && asset.SiteId != null)
                {
                    var site = sites.FirstOrDefault(s => s.Id == asset.SiteId);
                    if (site != null)
                    {
                        asset.CompanyId = site.CompanyId;
                    }
                }
                // If still null, assign to first company
                if (asset.CompanyId == null)
                {
                    asset.CompanyId = companies.First().Id;
                }
                // Ensure SiteId is set and belongs to the company
                if (asset.SiteId == null)
                {
                    var companySite = sites.FirstOrDefault(s => s.CompanyId == asset.CompanyId);
                    if (companySite != null)
                    {
                        asset.SiteId = companySite.Id;
                    }
                }
                // Ensure LocationId belongs to the company/site
                if (asset.LocationId == null && asset.SiteId != null)
                {
                    var companyLocation = locations.FirstOrDefault(l => l.CompanyId == asset.CompanyId && l.SiteId == asset.SiteId);
                    if (companyLocation != null)
                    {
                        asset.LocationId = companyLocation.Id;
                    }
                }
            }
            if (assetsToFix.Any())
            {
                await _context.SaveChangesAsync();
            }

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var assetTypes = new[] { "CNC MACHINE", "LATHE", "MILL", "GRINDER", "PRESS", "WELDER", "ROBOT", "CONVEYOR", "COMPRESSOR", "HVAC UNIT" };
            var existingNumbers = await _context.Assets.Select(a => a.AssetNumber).ToListAsync();
            var startDate = new DateTime(2018, 1, 1);
            var toInsert = targetCount - existing;

            // Distribute assets across companies for proper tenant-scoped testing
            int assetIndex = existing + 1;
            int assetsPerCompany = Math.Max(1, toInsert / companies.Count);

            foreach (var company in companies)
            {
                // Get sites and locations for this company
                var companySites = sites.Where(s => s.CompanyId == company.Id).ToList();
                if (!companySites.Any()) continue;

                var companyLocations = locations.Where(l => l.CompanyId == company.Id).ToList();

                int assetsForThisCompany = (company == companies.Last()) 
                    ? (targetCount - assetIndex + 1) // Give remaining to last company
                    : assetsPerCompany;

                for (int i = 0; i < assetsForThisCompany && assetIndex <= targetCount; i++)
                {
                    var assetNumber = $"ASSET-{assetIndex:D5}";
                    
                    if (existingNumbers.Contains(assetNumber))
                    {
                        assetIndex++;
                        tableResult.Skipped++;
                        continue;
                    }

                    var assetType = assetTypes[_random.Next(assetTypes.Length)];
                    var site = companySites[_random.Next(companySites.Count)];
                    var siteLocations = companyLocations.Where(l => l.SiteId == site.Id).ToList();
                    var location = siteLocations.Any() ? siteLocations[_random.Next(siteLocations.Count)] : null;
                    var inServiceDate = startDate.AddDays(_random.Next(0, 2500));
                    var cost = Math.Round((decimal)(_random.NextDouble() * 450000 + 50000), 2);

                    _context.Assets.Add(new Asset
                    {
                        AssetNumber = assetNumber,
                        Description = $"{assetType} #{assetIndex}",
                        SerialNumber = $"SN-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                        Model = $"MODEL-{_random.Next(1000, 9999)}",
                        CompanyId = company.Id, // CRITICAL: stamp CompanyId for tenant-scoped queries
                        SiteId = site.Id,
                        LocationId = location?.Id,
                        InServiceDate = inServiceDate,
                        PurchaseDate = inServiceDate.AddDays(-_random.Next(1, 30)),
                        AcquisitionCost = cost,
                        ReplacementCost = cost,
                        SalvageValue = Math.Round(cost * 0.1m, 2),
                        Status = AssetStatus.Active,
                        AssetType = assetType,
                        UsefulLifeMonths = 84
                    });
                    tableResult.Inserted++;
                    assetIndex++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedMaintenanceEventsAsync(int targetCount)
        {
            var existing = await _context.MaintenanceEvents.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "MaintenanceEvents", TargetCount = targetCount, ExistingCount = existing };

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var assets = await _context.Assets.OrderBy(a => a.Id).Take(100).ToListAsync();
            var technicians = await _context.Technicians.ToListAsync();

            if (!assets.Any()) return tableResult;

            var maintenanceTypes = new[] { MaintenanceType.Preventative, MaintenanceType.Corrective, MaintenanceType.Predictive, MaintenanceType.Inspection };
            var toInsert = targetCount - existing;

            for (int i = 0; i < toInsert; i++)
            {
                var asset = assets[_random.Next(assets.Count)];
                var scheduledDate = DateTime.Today.AddDays(-_random.Next(0, 365));
                var maintType = maintenanceTypes[_random.Next(maintenanceTypes.Length)];
                var isComplete = _random.Next(100) < 70;

                var woNumber = $"WO-{DateTime.UtcNow:yy}-{(existing + i + 1):D5}";
                _context.MaintenanceEvents.Add(new MaintenanceEvent
                {
                    AssetId = asset.Id,
                    ScheduledDate = scheduledDate,
                    CompletedDate = isComplete ? scheduledDate : null,
                    Type = maintType,
                    Description = $"{maintType} maintenance for {asset.Description}",
                    TechnicianId = technicians.Any() ? technicians[_random.Next(technicians.Count)].Id : null,
                    EstimatedCost = Math.Round((decimal)(_random.NextDouble() * 500 + 50), 2),
                    ActualCost = isComplete ? Math.Round((decimal)(_random.NextDouble() * 500 + 50), 2) : null,
                    LaborCost = Math.Round((decimal)(_random.NextDouble() * 400 + 50), 2),
                    PartsCost = Math.Round((decimal)(_random.NextDouble() * 300), 2),
                    Status = isComplete ? MaintenanceStatus.Completed : (_random.Next(100) < 50 ? MaintenanceStatus.Scheduled : MaintenanceStatus.InProgress),
                    WorkOrderNumber = woNumber,
                    CreatedAt = DateTime.UtcNow
                });
                tableResult.Inserted++;
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedPMTemplatesAsync(int targetCount)
        {
            var existing = await _context.Set<PMTemplate>().CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "PMTemplates", TargetCount = targetCount, ExistingCount = existing };

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var templates = new[]
            {
                ("PM-OIL", "Oil Change & Lubrication", MaintenanceType.Preventative, RecurrenceType.Monthly, 1, 2.0m, "Quarterly oil change and bearing lubrication"),
                ("PM-INSP-D", "Daily Safety Inspection", MaintenanceType.Inspection, RecurrenceType.Daily, 1, 0.5m, "Daily pre-shift safety check"),
                ("PM-INSP-W", "Weekly System Check", MaintenanceType.Inspection, RecurrenceType.Weekly, 1, 1.0m, "Weekly system performance check"),
                ("PM-BELT", "Belt/Chain Inspection", MaintenanceType.Preventative, RecurrenceType.Monthly, 3, 1.5m, "Quarterly belt and chain inspection"),
                ("PM-FILTER", "Filter Replacement", MaintenanceType.Preventative, RecurrenceType.Monthly, 1, 1.0m, "Monthly air and oil filter replacement"),
                ("PM-ALIGN", "Alignment Check", MaintenanceType.Preventative, RecurrenceType.Quarterly, 1, 3.0m, "Quarterly alignment verification"),
                ("PM-CALIB", "Calibration Check", MaintenanceType.Calibration, RecurrenceType.Monthly, 6, 4.0m, "Semi-annual calibration verification"),
                ("PM-ELEC", "Electrical Inspection", MaintenanceType.Inspection, RecurrenceType.Quarterly, 1, 2.0m, "Quarterly electrical system inspection"),
                ("PM-HYD", "Hydraulic System Check", MaintenanceType.Preventative, RecurrenceType.Monthly, 1, 2.5m, "Monthly hydraulic fluid and pressure check"),
                ("PM-COOL", "Coolant System Service", MaintenanceType.Preventative, RecurrenceType.Quarterly, 1, 2.0m, "Quarterly coolant change and flush"),
                ("PM-ANNUAL", "Annual Overhaul", MaintenanceType.Preventative, RecurrenceType.Annually, 1, 24.0m, "Annual comprehensive overhaul"),
                ("PM-TOOL", "Tool Holder Inspection", MaintenanceType.Inspection, RecurrenceType.Weekly, 1, 0.5m, "Weekly tool holder check"),
                ("PM-WAY", "Way Lubrication", MaintenanceType.Preventative, RecurrenceType.Weekly, 1, 0.5m, "Weekly way lubrication"),
                ("PM-SPINDLE", "Spindle Service", MaintenanceType.Preventative, RecurrenceType.Quarterly, 1, 4.0m, "Quarterly spindle bearing check"),
                ("PM-AXIS", "Axis Backlash Check", MaintenanceType.Inspection, RecurrenceType.Monthly, 1, 1.5m, "Monthly axis backlash measurement")
            };

            foreach (var (code, name, type, interval, intervalValue, hours, desc) in templates.Take(targetCount))
            {
                if (!await _context.Set<PMTemplate>().AnyAsync(p => p.Code == code))
                {
                    _context.Set<PMTemplate>().Add(new PMTemplate
                    {
                        Code = code,
                        Name = name,
                        Type = type,
                        CalendarInterval = interval,
                        CalendarIntervalValue = intervalValue,
                        EstimatedHours = hours,
                        Description = desc,
                        Priority = type == MaintenanceType.Inspection ? PMPriority.Medium : PMPriority.High,
                        TriggerType = PMTriggerType.Calendar,
                        IsActive = true
                    });
                    tableResult.Inserted++;
                }
                else
                {
                    tableResult.Skipped++;
                }
            }
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedPMTemplateAssetsAsync()
        {
            var existing = await _context.Set<PMTemplateAsset>().CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "PMTemplateAssets", TargetCount = 0, ExistingCount = existing };

            var templates = await _context.Set<PMTemplate>().Where(t => t.IsActive).ToListAsync();
            var assets = await _context.Assets.Where(a => a.Status == AssetStatus.Active).Take(50).ToListAsync();

            if (!templates.Any() || !assets.Any()) return tableResult;

            var existingAssignments = await _context.Set<PMTemplateAsset>()
                .Select(pa => new { pa.PMTemplateId, pa.AssetId })
                .ToListAsync();
            var existingSet = existingAssignments.Select(e => $"{e.PMTemplateId}-{e.AssetId}").ToHashSet();

            foreach (var asset in assets)
            {
                var templatesToAssign = templates.OrderBy(_ => _random.Next()).Take(_random.Next(1, 4)).ToList();
                foreach (var template in templatesToAssign)
                {
                    var key = $"{template.Id}-{asset.Id}";
                    if (!existingSet.Contains(key))
                    {
                        var nextDue = DateTime.Today.AddDays(_random.Next(-30, 60));
                        _context.Set<PMTemplateAsset>().Add(new PMTemplateAsset
                        {
                            PMTemplateId = template.Id,
                            AssetId = asset.Id,
                            IsActive = true,
                            NextDueDate = nextDue,
                            LastCompletedDate = nextDue.AddDays(-_random.Next(7, 90))
                        });
                        existingSet.Add(key);
                        tableResult.Inserted++;
                    }
                    else
                    {
                        tableResult.Skipped++;
                    }
                }
            }
            tableResult.TargetCount = tableResult.Inserted + tableResult.Skipped;
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedPMSchedulesAsync()
        {
            var existing = await _context.PMSchedules.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "PMSchedules", TargetCount = 0, ExistingCount = existing };

            var templates = await _context.Set<PMTemplate>().Where(t => t.IsActive).ToListAsync();
            if (!templates.Any()) return tableResult;

            var company = await _context.Companies.FirstOrDefaultAsync();
            var site = await _context.Sites.FirstOrDefaultAsync();
            var tenant = await _context.Tenants.FirstOrDefaultAsync();

            var existingScheduleNames = await _context.PMSchedules.Select(s => s.Name).ToListAsync();
            var existingSet = existingScheduleNames.ToHashSet();

            var todayUtc = DateTime.UtcNow.Date;
            var scheduleTemplates = new[]
            {
                ("CNC Daily Inspection", "PM-CNC-DAILY", 1),
                ("CNC Weekly Maintenance", "PM-CNC-WEEKLY", 7),
                ("CNC Monthly Service", "PM-CNC-MONTHLY", 30),
                ("Compressor Weekly Check", "PM-COMP-WEEKLY", 7),
                ("Crane Monthly Inspection", "PM-CRANE-MONTHLY", 30),
                ("Robot Weekly Maintenance", "PM-ROBOT-WEEKLY", 7),
                ("HVAC Monthly Service", "PM-HVAC-MONTHLY", 30),
                ("Press Weekly Safety", "PM-PRESS-WEEKLY", 7)
            };

            foreach (var (name, templateCode, intervalDays) in scheduleTemplates)
            {
                if (existingSet.Contains(name)) 
                {
                    tableResult.Skipped++;
                    continue;
                }

                var template = templates.FirstOrDefault(t => t.Code == templateCode);
                if (template == null) continue;

                var schedule = new PMSchedule
                {
                    Name = name,
                    Description = $"Auto-generated {name} schedule",
                    PMTemplateId = template.Id,
                    Active = true,
                    CadenceType = PMCadenceType.IntervalDays,
                    IntervalDays = intervalDays,
                    StartDateUtc = todayUtc.AddDays(-30),
                    NextDueDateUtc = todayUtc.AddDays(_random.Next(-3, 7)),
                    TimeZoneId = "America/New_York",
                    TenantId = tenant?.Id,
                    CompanyId = company?.Id,
                    SiteId = site?.Id
                };

                _context.PMSchedules.Add(schedule);
                existingSet.Add(name);
                tableResult.Inserted++;
            }

            tableResult.TargetCount = tableResult.Inserted + tableResult.Skipped;
            await _context.SaveChangesAsync();
            return tableResult;
        }

        private async Task<SeedPackTableResult> SeedMaintenanceSchedulesAsync(int targetCount)
        {
            var existing = await _context.MaintenanceSchedules.CountAsync();
            var tableResult = new SeedPackTableResult { TableName = "MaintenanceSchedules", TargetCount = targetCount, ExistingCount = existing };

            if (existing >= targetCount)
            {
                tableResult.Skipped = targetCount;
                return tableResult;
            }

            var assets = await _context.Assets.Where(a => a.Status == AssetStatus.Active).Take(50).ToListAsync();
            if (!assets.Any()) return tableResult;

            var scheduleTemplates = new[]
            {
                ("Daily Safety Check", MaintenanceType.Inspection, RecurrenceType.Daily, 1),
                ("Weekly Lubrication", MaintenanceType.Preventative, RecurrenceType.Weekly, 1),
                ("Bi-Weekly Filter Check", MaintenanceType.Preventative, RecurrenceType.BiWeekly, 1),
                ("Monthly Oil Change", MaintenanceType.Preventative, RecurrenceType.Monthly, 1),
                ("Monthly Calibration", MaintenanceType.Calibration, RecurrenceType.Monthly, 1),
                ("Quarterly Belt Inspection", MaintenanceType.Preventative, RecurrenceType.Quarterly, 1),
                ("Quarterly Hydraulic Service", MaintenanceType.Preventative, RecurrenceType.Quarterly, 1),
                ("Quarterly Electrical Check", MaintenanceType.Inspection, RecurrenceType.Quarterly, 1),
                ("Semi-Annual Overhaul", MaintenanceType.Preventative, RecurrenceType.SemiAnnually, 1),
                ("Semi-Annual Safety Audit", MaintenanceType.Inspection, RecurrenceType.SemiAnnually, 1),
                ("Annual Certification", MaintenanceType.Calibration, RecurrenceType.Annually, 1),
                ("Annual Major Overhaul", MaintenanceType.Preventative, RecurrenceType.Annually, 1),
                ("Weekly Coolant Check", MaintenanceType.Preventative, RecurrenceType.Weekly, 1),
                ("Monthly Bearing Inspection", MaintenanceType.Inspection, RecurrenceType.Monthly, 1),
                ("Bi-Monthly Tool Calibration", MaintenanceType.Calibration, RecurrenceType.Monthly, 2),
                ("Quarterly Alignment Check", MaintenanceType.Preventative, RecurrenceType.Quarterly, 1),
                ("Monthly Pressure Test", MaintenanceType.Inspection, RecurrenceType.Monthly, 1),
                ("Weekly Drive Belt Check", MaintenanceType.Inspection, RecurrenceType.Weekly, 1),
                ("Monthly Air Filter Replace", MaintenanceType.Preventative, RecurrenceType.Monthly, 1),
                ("Quarterly Spindle Service", MaintenanceType.Preventative, RecurrenceType.Quarterly, 1),
                ("Semi-Annual Gearbox Service", MaintenanceType.Preventative, RecurrenceType.SemiAnnually, 1),
                ("Monthly Way Lubrication", MaintenanceType.Preventative, RecurrenceType.Monthly, 1),
                ("Quarterly Motor Inspection", MaintenanceType.Inspection, RecurrenceType.Quarterly, 1),
                ("Annual VFD Calibration", MaintenanceType.Calibration, RecurrenceType.Annually, 1),
                ("Monthly Vacuum System Check", MaintenanceType.Preventative, RecurrenceType.Monthly, 1)
            };

            var toCreate = targetCount - existing;
            var createdCount = 0;

            foreach (var (name, type, recurrence, interval) in scheduleTemplates.Take(toCreate))
            {
                var asset = assets[_random.Next(assets.Count)];
                var scheduleKey = $"{name}-{asset.Id}";
                
                if (!await _context.MaintenanceSchedules.AnyAsync(s => s.Name == name && s.AssetId == asset.Id))
                {
                    var startDate = DateTime.Today.AddDays(-_random.Next(30, 180));
                    var nextDue = CalculateNextDueDate(startDate, recurrence, interval);

                    var estimatedCost = type == MaintenanceType.Inspection ? 50m : (type == MaintenanceType.Calibration ? 200m : 150m);
                    _context.MaintenanceSchedules.Add(new MaintenanceSchedule
                    {
                        AssetId = asset.Id,
                        Name = name,
                        Description = $"Scheduled {name} for {asset.Description}",
                        Type = type,
                        Recurrence = recurrence,
                        IntervalValue = interval,
                        StartDate = startDate,
                        NextDueDate = nextDue,
                        LastGeneratedDate = DateTime.Today.AddDays(-_random.Next(1, 30)),
                        IsActive = true,
                        EstimatedCost = estimatedCost
                    });
                    tableResult.Inserted++;
                    createdCount++;
                }
                else
                {
                    tableResult.Skipped++;
                }

                if (createdCount >= toCreate) break;
            }

            await _context.SaveChangesAsync();
            return tableResult;
        }

        private DateTime CalculateNextDueDate(DateTime lastDate, RecurrenceType recurrence, int interval)
        {
            var today = DateTime.Today;
            var nextDue = lastDate;

            while (nextDue <= today)
            {
                nextDue = recurrence switch
                {
                    RecurrenceType.Daily => nextDue.AddDays(interval),
                    RecurrenceType.Weekly => nextDue.AddDays(7 * interval),
                    RecurrenceType.BiWeekly => nextDue.AddDays(14 * interval),
                    RecurrenceType.Monthly => nextDue.AddMonths(interval),
                    RecurrenceType.Quarterly => nextDue.AddMonths(3 * interval),
                    RecurrenceType.SemiAnnually => nextDue.AddMonths(6 * interval),
                    RecurrenceType.Annually => nextDue.AddYears(interval),
                    _ => nextDue.AddMonths(interval)
                };
            }
            return nextDue;
        }

        private async Task<SeedPackTableResult> GenerateScheduledEventsAsync()
        {
            var tableResult = new SeedPackTableResult { TableName = "ScheduledEvents (from Schedules)", TargetCount = 0, ExistingCount = 0 };

            var schedules = await _context.MaintenanceSchedules
                .Include(s => s.Asset)
                .Where(s => s.IsActive && s.NextDueDate.HasValue)
                .ToListAsync();

            if (!schedules.Any()) return tableResult;

            var technicians = await _context.Technicians.ToListAsync();
            var today = DateTime.Today;
            var horizonDays = new[] { 30, 60, 90 };

            foreach (var schedule in schedules)
            {
                if (!schedule.NextDueDate.HasValue || schedule.Asset == null) continue;

                var nextDue = schedule.NextDueDate.Value;
                var daysFromNow = (nextDue - today).TotalDays;

                foreach (var horizon in horizonDays)
                {
                    if (daysFromNow <= 0 || daysFromNow > horizon) continue;

                    var existingEvent = await _context.MaintenanceEvents.AnyAsync(e =>
                        e.AssetId == schedule.AssetId &&
                        e.Description != null &&
                        e.Description.Contains($"[Schedule:{schedule.Id}]") &&
                        e.ScheduledDate >= today);

                    if (!existingEvent)
                    {
                        var tech = technicians.Any() ? technicians[_random.Next(technicians.Count)] : null;
                        var woNumber = $"WO-SCH-{schedule.Id:D4}-{nextDue:yyyyMMdd}";

                        if (!await _context.MaintenanceEvents.AnyAsync(e => e.WorkOrderNumber == woNumber))
                        {
                            _context.MaintenanceEvents.Add(new MaintenanceEvent
                            {
                                AssetId = schedule.AssetId,
                                Type = schedule.Type,
                                Priority = schedule.Type == MaintenanceType.Inspection ? MaintenancePriority.Medium : MaintenancePriority.High,
                                Status = MaintenanceStatus.Scheduled,
                                ScheduledDate = nextDue,
                                Description = $"{schedule.Name} [Schedule:{schedule.Id}]",
                                WorkOrderNumber = woNumber,
                                TechnicianId = tech?.Id,
                                EstimatedCost = schedule.EstimatedCost > 0 ? schedule.EstimatedCost : 100m
                            });
                            tableResult.Inserted++;
                        }
                    }
                    else
                    {
                        tableResult.Skipped++;
                    }
                    break;
                }
            }

            tableResult.TargetCount = tableResult.Inserted + tableResult.Skipped;
            await _context.SaveChangesAsync();
            return tableResult;
        }
    }
}
