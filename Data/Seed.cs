using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Abs.FixedAssets.Data
{
    public static class Seed
    {
        private static readonly Random _random = new Random(42);

        public static async Task InitializeAsync(AppDbContext db)
        {
            await db.Database.EnsureCreatedAsync();

            if (!await db.Books.AnyAsync())
            {
                db.Books.AddRange(
                    new Book { Code = "GAAP", Name = "Financial (GAAP)", GlAccountAccumDep = "16010", GlAccountDepExp = "74010" },
                    new Book { Code = "TAX",  Name = "Tax",               GlAccountAccumDep = "16010", GlAccountDepExp = "74010" }
                );
                await db.SaveChangesAsync();
            }

            await SeedCompaniesAsync(db);
            await SeedIntercompanyGlAccountsAsync(db);
            await SeedVendorsAsync(db);
            await SeedTechniciansAsync(db);
            await SeedLocationsAsync(db);
            await SeedDepreciationPoliciesAsync(db);
            await SeedAssetsAsync(db);
            await SeedMaintenanceEventsAsync(db);
            await SeedWorkOrderOperationsAsync(db);
            Console.WriteLine("[Seed] Database initialization complete");
        }

        private static async Task SeedCompaniesAsync(AppDbContext db)
        {
            if (await db.Companies.AnyAsync())
                return;

            var companies = new List<Company>
            {
                new Company { Id = 1, CompanyCode = "PWH", Name = "PRESTIGE WORLDWIDE HOLDINGS", Country = "USA", Currency = "USD", IsActive = true, ParentCompanyId = null },
                new Company { Id = 2, CompanyCode = "PWH-CAN", Name = "PWH MANUFACTURING CANADA", Country = "Canada", Currency = "CAD", IsActive = true, ParentCompanyId = 1 },
                new Company { Id = 3, CompanyCode = "PWH-USA", Name = "PWH MANUFACTURING USA", Country = "USA", Currency = "USD", IsActive = true, ParentCompanyId = 1 }
            };

            db.Companies.AddRange(companies);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('\"Companies\"', 'Id'), (SELECT MAX(\"Id\") FROM \"Companies\"))");
            Console.WriteLine("[Seed] Seeded 3 companies");
        }

        private static async Task SeedVendorsAsync(AppDbContext db)
        {
            if (await db.Vendors.AnyAsync())
                return;

            var vendors = new List<Vendor>
            {
                new Vendor { Code = "SKF001", Name = "SKF BEARINGS", Address = "123 INDUSTRIAL WAY", City = "TORONTO", State = "ON", PostalCode = "M5V 3K2", Country = "CANADA", Phone = "416-555-0100", Email = "ORDERS@SKF.COM", IsActive = true },
                new Vendor { Code = "FAG001", Name = "FAG BEARINGS", Address = "456 MANUFACTURING DR", City = "DETROIT", State = "MI", PostalCode = "48201", Country = "USA", Phone = "313-555-0200", Email = "SALES@FAG.COM", IsActive = true },
                new Vendor { Code = "GRA001", Name = "GRAINGER INDUSTRIAL", Address = "100 GRAINGER PKWY", City = "LAKE FOREST", State = "IL", PostalCode = "60045", Country = "USA", Phone = "800-555-0300", Email = "ORDERS@GRAINGER.COM", IsActive = true },
                new Vendor { Code = "MSC001", Name = "MSC INDUSTRIAL SUPPLY", Address = "75 MAXESS RD", City = "MELVILLE", State = "NY", PostalCode = "11747", Country = "USA", Phone = "800-555-0400", Email = "SERVICE@MSCDIRECT.COM", IsActive = true },
                new Vendor { Code = "FAI001", Name = "FASTENAL COMPANY", Address = "2001 THEURER BLVD", City = "WINONA", State = "MN", PostalCode = "55987", Country = "USA", Phone = "507-555-0500", Email = "ORDERS@FASTENAL.COM", IsActive = true },
                new Vendor { Code = "HAA001", Name = "HAAS AUTOMATION", Address = "2800 STURGIS RD", City = "OXNARD", State = "CA", PostalCode = "93030", Country = "USA", Phone = "805-555-0600", Email = "SERVICE@HAASCNC.COM", IsActive = true },
                new Vendor { Code = "MAZ001", Name = "MAZAK CORPORATION", Address = "8025 PRODUCTION DR", City = "FLORENCE", State = "KY", PostalCode = "41042", Country = "USA", Phone = "859-555-0700", Email = "PARTS@MAZAKUSA.COM", IsActive = true },
                new Vendor { Code = "SAN001", Name = "SANDVIK TOOLING", Address = "1702 NEVINS RD", City = "FAIR LAWN", State = "NJ", PostalCode = "07410", Country = "USA", Phone = "201-555-0800", Email = "TOOLS@SANDVIK.COM", IsActive = true },
                new Vendor { Code = "KEN001", Name = "KENNAMETAL INC", Address = "600 GRANT AVE", City = "LATROBE", State = "PA", PostalCode = "15650", Country = "USA", Phone = "724-555-0900", Email = "SERVICE@KENNAMETAL.COM", IsActive = true },
                new Vendor { Code = "LIN001", Name = "LINCOLN ELECTRIC", Address = "22801 ST CLAIR AVE", City = "CLEVELAND", State = "OH", PostalCode = "44117", Country = "USA", Phone = "216-555-1000", Email = "SALES@LINCOLNELECTRIC.COM", IsActive = true }
            };

            db.Vendors.AddRange(vendors);
            await db.SaveChangesAsync();
            Console.WriteLine("[Seed] Seeded 10 vendors");
        }

        private static async Task SeedTechniciansAsync(AppDbContext db)
        {
            if (await db.Technicians.AnyAsync())
                return;

            var technicians = new List<Technician>
            {
                new Technician { Name = "JOHN SMITH", Specialty = "SENIOR MAINTENANCE", Phone = "416-555-1001", Email = "JSMITH@COMPANY.COM", HourlyRate = 45.00m },
                new Technician { Name = "MARIA GARCIA", Specialty = "ELECTRICIAN", Phone = "416-555-1002", Email = "MGARCIA@COMPANY.COM", HourlyRate = 48.00m },
                new Technician { Name = "DAVID CHEN", Specialty = "CNC SPECIALIST", Phone = "416-555-1003", Email = "DCHEN@COMPANY.COM", HourlyRate = 52.00m },
                new Technician { Name = "SARAH JOHNSON", Specialty = "HVAC TECHNICIAN", Phone = "416-555-1004", Email = "SJOHNSON@COMPANY.COM", HourlyRate = 44.00m },
                new Technician { Name = "MICHAEL BROWN", Specialty = "WELDER", Phone = "416-555-1005", Email = "MBROWN@COMPANY.COM", HourlyRate = 46.00m }
            };

            db.Technicians.AddRange(technicians);
            await db.SaveChangesAsync();
            Console.WriteLine("[Seed] Seeded 5 technicians");
        }

        private static async Task SeedDepreciationPoliciesAsync(AppDbContext db)
        {
            if (await db.DepreciationPolicies.AnyAsync())
                return;

            var policies = new List<DepreciationPolicy>
            {
                new DepreciationPolicy { Code = "SL-5", Name = "STRAIGHT LINE 5 YEAR", Method = DepreciationMethod.StraightLine, DefaultUsefulLifeMonths = 60, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "SL-7", Name = "STRAIGHT LINE 7 YEAR", Method = DepreciationMethod.StraightLine, DefaultUsefulLifeMonths = 84, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "SL-10", Name = "STRAIGHT LINE 10 YEAR", Method = DepreciationMethod.StraightLine, DefaultUsefulLifeMonths = 120, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "SL-15", Name = "STRAIGHT LINE 15 YEAR", Method = DepreciationMethod.StraightLine, DefaultUsefulLifeMonths = 180, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "DDB-5", Name = "DOUBLE DECLINING 5 YEAR", Method = DepreciationMethod.DoubleDecliningBalance, DefaultUsefulLifeMonths = 60, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "DDB-7", Name = "DOUBLE DECLINING 7 YEAR", Method = DepreciationMethod.DoubleDecliningBalance, DefaultUsefulLifeMonths = 84, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "MACRS-5", Name = "MACRS 5 YEAR PROPERTY", Method = DepreciationMethod.MACRS, DefaultUsefulLifeMonths = 60, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "MACRS-7", Name = "MACRS 7 YEAR PROPERTY", Method = DepreciationMethod.MACRS, DefaultUsefulLifeMonths = 84, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "SYD-5", Name = "SUM OF YEARS DIGITS 5 YEAR", Method = DepreciationMethod.SumOfYearsDigits, DefaultUsefulLifeMonths = 60, Convention = DepreciationConvention.HalfYear },
                new DepreciationPolicy { Code = "SYD-7", Name = "SUM OF YEARS DIGITS 7 YEAR", Method = DepreciationMethod.SumOfYearsDigits, DefaultUsefulLifeMonths = 84, Convention = DepreciationConvention.HalfYear }
            };

            db.DepreciationPolicies.AddRange(policies);
            await db.SaveChangesAsync();
            Console.WriteLine("[Seed] Seeded 10 depreciation policies");
        }

        private static readonly string[] _assetTypes = { "CNC MACHINE", "LATHE", "MILL", "GRINDER", "PRESS", "WELDER", "ROBOT", "CONVEYOR", "COMPRESSOR", "HVAC UNIT", "FORKLIFT", "CRANE", "PUMP", "MOTOR", "TRANSFORMER" };
        private static readonly string[] _manufacturers = { "HAAS", "MAZAK", "DMG MORI", "OKUMA", "MAKINO", "FANUC", "ABB", "KUKA", "SIEMENS", "LINCOLN", "MILLER", "TOYOTA", "CATERPILLAR", "INGERSOLL RAND", "TRANE" };

        private static async Task SeedAssetsAsync(AppDbContext db)
        {
            if (await db.Assets.AnyAsync())
                return;

            var locations = await db.Locations.ToListAsync();
            var policies = await db.DepreciationPolicies.ToListAsync();
            var companies = await db.Companies.ToListAsync();
            
            if (!locations.Any() || !policies.Any() || !companies.Any())
            {
                Console.WriteLine("[Seed] Skipping assets - missing dependencies");
                return;
            }

            var assets = new List<Asset>();
            var startDate = new DateTime(2018, 1, 1);
            
            for (int i = 1; i <= 321; i++)
            {
                var assetType = _assetTypes[_random.Next(_assetTypes.Length)];
                var manufacturer = _manufacturers[_random.Next(_manufacturers.Length)];
                var location = locations[_random.Next(locations.Count)];
                var policy = policies[_random.Next(policies.Count)];
                var company = companies.OrderBy(c => c.Id).Skip(1).FirstOrDefault() ?? companies.OrderBy(c => c.Id).First();
                var acquiredDate = startDate.AddDays(_random.Next(0, 2500));
                var cost = Math.Round((decimal)(_random.NextDouble() * 450000 + 50000), 2);
                
                assets.Add(new Asset
                {
                    AssetNumber = $"AST-{i:D5}",
                    Description = $"{manufacturer} {assetType} #{i}",
                    LongDescription = $"{manufacturer} {assetType} - PRODUCTION EQUIPMENT",
                    SerialNumber = $"SN{_random.Next(100000, 999999)}",
                    AssetType = assetType,
                    Model = $"MODEL-{_random.Next(1000, 9999)}",
                    PurchaseDate = acquiredDate,
                    InServiceDate = acquiredDate.AddDays(_random.Next(7, 30)),
                    AcquisitionCost = cost,
                    SalvageValue = Math.Round(cost * 0.1m, 2),
                    Status = AssetStatus.Active,
                    LocationId = location.Id,
                    CompanyId = company.Id,
                    UsefulLifeMonths = policy.DefaultUsefulLifeMonths
                });
            }

            db.Assets.AddRange(assets);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Seed] Seeded {assets.Count} assets");
        }

        private static async Task SeedMaintenanceEventsAsync(AppDbContext db)
        {
            if (await db.MaintenanceEvents.AnyAsync())
                return;

            var assets = await db.Assets.Take(100).ToListAsync();
            var technicians = await db.Technicians.ToListAsync();
            
            if (!assets.Any() || !technicians.Any())
            {
                Console.WriteLine("[Seed] Skipping maintenance events - missing dependencies");
                return;
            }

            var events = new List<MaintenanceEvent>();
            var types = new[] { MaintenanceType.Preventative, MaintenanceType.Corrective, MaintenanceType.Inspection, MaintenanceType.Calibration };
            var statuses = new[] { MaintenanceStatus.Completed, MaintenanceStatus.InProgress, MaintenanceStatus.Scheduled };

            foreach (var asset in assets)
            {
                var numEvents = _random.Next(1, 5);
                for (int j = 0; j < numEvents; j++)
                {
                    var tech = technicians[_random.Next(technicians.Count)];
                    var scheduledDate = DateTime.Today.AddDays(_random.Next(-180, 90));
                    var status = statuses[_random.Next(statuses.Length)];
                    
                    var year = DateTime.UtcNow.Year.ToString().Substring(2);
                    var woSeq = events.Count + 1;
                    events.Add(new MaintenanceEvent
                    {
                        AssetId = asset.Id,
                        Type = types[_random.Next(types.Length)],
                        Description = $"SCHEDULED MAINTENANCE",
                        ScheduledDate = scheduledDate,
                        CompletedDate = status == MaintenanceStatus.Completed ? scheduledDate.AddHours(_random.Next(1, 8)) : null,
                        Status = status,
                        TechnicianId = tech.Id,
                        TechnicianName = tech.Name,
                        LaborHours = status == MaintenanceStatus.Completed ? _random.Next(1, 10) : null,
                        LaborCost = status == MaintenanceStatus.Completed ? Math.Round((decimal)_random.Next(100, 1000), 2) : null,
                        PartsCost = status == MaintenanceStatus.Completed ? Math.Round((decimal)_random.Next(50, 500), 2) : null,
                        Notes = "ROUTINE MAINTENANCE PER SCHEDULE",
                        WorkOrderNumber = $"WO-{year}-{woSeq:D5}",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            db.MaintenanceEvents.AddRange(events);
            await db.SaveChangesAsync();
            Console.WriteLine($"[Seed] Seeded {events.Count} maintenance events");
        }

        private static async Task SeedWorkOrderOperationsAsync(AppDbContext db)
        {
            if (await db.WorkOrderOperations.AnyAsync())
                return;

            var workOrders = await db.MaintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.InProgress || m.Status == MaintenanceStatus.Scheduled)
                .Take(10)
                .ToListAsync();
            var technicians = await db.Technicians.Take(5).ToListAsync();
            var crafts = await db.Crafts.Take(5).ToListAsync();
            var items = await db.Items.Take(10).ToListAsync();

            if (!workOrders.Any())
            {
                Console.WriteLine("[Seed] Skipping WO operations - no work orders found");
                return;
            }

            var operations = new List<WorkOrderOperation>();
            var laborEntries = new List<WorkOrderOperationLabor>();
            var tools = new List<WorkOrderOperationTool>();
            var parts = new List<WorkOrderOperationPart>();

            foreach (var wo in workOrders)
            {
                wo.WorkOrderNumber = $"WO-{wo.Id:D6}";
                
                var op1 = new WorkOrderOperation
                {
                    MaintenanceEventId = wo.Id,
                    OperationNumber = "OP-001",
                    Sequence = 10,
                    Title = "MECHANICAL INSPECTION",
                    Type = OperationType.Mechanical,
                    Description = "INSPECT MECHANICAL COMPONENTS FOR WEAR AND DAMAGE",
                    Status = OperationStatus.Completed,
                    PlannedHours = 2,
                    ActualHours = 1.5m,
                    RequiresShutdown = true,
                    RequiresLOTO = true,
                    LOTOProcedureId = "LOTO-001",
                    CraftId = crafts.Any() ? crafts[0].Id : null
                };
                operations.Add(op1);

                var op2 = new WorkOrderOperation
                {
                    MaintenanceEventId = wo.Id,
                    OperationNumber = "OP-002",
                    Sequence = 20,
                    Title = "ELECTRICAL SYSTEMS CHECK",
                    Type = OperationType.Electrical,
                    Description = "CHECK ALL ELECTRICAL CONNECTIONS AND MOTOR PERFORMANCE",
                    Status = OperationStatus.InProgress,
                    PlannedHours = 1.5m,
                    ActualHours = 0.5m,
                    RequiresShutdown = true,
                    CraftId = crafts.Count > 1 ? crafts[1].Id : null
                };
                operations.Add(op2);

                var op3 = new WorkOrderOperation
                {
                    MaintenanceEventId = wo.Id,
                    OperationNumber = "OP-003",
                    Sequence = 30,
                    Title = "FINAL INSPECTION AND TESTING",
                    Type = OperationType.Inspection,
                    Description = "PERFORM FINAL INSPECTION AND OPERATIONAL TEST",
                    Status = OperationStatus.Pending,
                    PlannedHours = 1,
                    ActualHours = 0
                };
                operations.Add(op3);
            }

            db.WorkOrderOperations.AddRange(operations);
            await db.SaveChangesAsync();

            var savedOps = await db.WorkOrderOperations.ToListAsync();
            foreach (var op in savedOps)
            {
                if (technicians.Any())
                {
                    laborEntries.Add(new WorkOrderOperationLabor
                    {
                        WorkOrderOperationId = op.Id,
                        TechnicianId = technicians[_random.Next(technicians.Count)].Id,
                        WorkDate = DateTime.UtcNow.Date,
                        Hours = 1.5m,
                        HourlyRate = 65m,
                        Notes = "ROUTINE LABOR"
                    });
                }

                tools.Add(new WorkOrderOperationTool
                {
                    WorkOrderOperationId = op.Id,
                    ToolName = "TORQUE WRENCH",
                    QuantityRequired = 1,
                    QuantityUsed = 1
                });

                if (items.Any())
                {
                    var item = items[_random.Next(items.Count)];
                    parts.Add(new WorkOrderOperationPart
                    {
                        WorkOrderOperationId = op.Id,
                        ItemId = item.Id,
                        QuantityPlanned = 2,
                        QuantityIssued = 2,
                        QuantityUsed = 1,
                        UnitCost = 25.50m
                    });
                }
            }

            if (laborEntries.Any()) db.WorkOrderOperationLabors.AddRange(laborEntries);
            if (tools.Any()) db.WorkOrderOperationTools.AddRange(tools);
            if (parts.Any()) db.WorkOrderOperationParts.AddRange(parts);
            await db.SaveChangesAsync();

            Console.WriteLine($"[Seed] Seeded {operations.Count} WO operations with labor, tools, parts");
        }

        private static async Task SeedLocationsAsync(AppDbContext db)
        {
            if (await db.Locations.AnyAsync())
                return;

            var locations = new List<Location>
            {
                new Location { Id = 1, Code = "MISS", Name = "Mississauga Plant", Type = LocationType.Building, CompanyId = 2, SortOrder = 100 },
                new Location { Id = 2, Code = "BRAM", Name = "Brampton Facility", Type = LocationType.Building, CompanyId = 2, SortOrder = 200 },
                new Location { Id = 3, Code = "BURL", Name = "Burlington Operations", Type = LocationType.Building, CompanyId = 2, SortOrder = 300 },
                new Location { Id = 4, Code = "DET", Name = "Detroit Main Plant", Type = LocationType.Building, CompanyId = 3, SortOrder = 400 },
                new Location { Id = 10, Code = "MISS-A", Name = "Building A - CNC", Type = LocationType.Building, ParentLocationId = 1, CompanyId = 2, SortOrder = 110 },
                new Location { Id = 11, Code = "MISS-B", Name = "Building B - Welding", Type = LocationType.Building, ParentLocationId = 1, CompanyId = 2, SortOrder = 120 },
                new Location { Id = 12, Code = "MISS-C", Name = "Building C - Assembly", Type = LocationType.Building, ParentLocationId = 1, CompanyId = 2, SortOrder = 130 },
                new Location { Id = 13, Code = "MISS-WH", Name = "Warehouse", Type = LocationType.Storage, ParentLocationId = 1, CompanyId = 2, SortOrder = 140 },
                new Location { Id = 20, Code = "MISS-A-B1", Name = "Bay 1 - 3-Axis CNC", Bay = "Bay 1", Type = LocationType.Bay, ParentLocationId = 10, CompanyId = 2, SortOrder = 111 },
                new Location { Id = 21, Code = "MISS-A-B2", Name = "Bay 2 - 5-Axis CNC", Bay = "Bay 2", Type = LocationType.Bay, ParentLocationId = 10, CompanyId = 2, SortOrder = 112 },
                new Location { Id = 22, Code = "MISS-A-B3", Name = "Bay 3 - Lathes", Bay = "Bay 3", Type = LocationType.Bay, ParentLocationId = 10, CompanyId = 2, SortOrder = 113 },
                new Location { Id = 25, Code = "MISS-B-B1", Name = "Bay 1 - MIG Welding", Bay = "Bay 1", Type = LocationType.Bay, ParentLocationId = 11, CompanyId = 2, SortOrder = 121 },
                new Location { Id = 26, Code = "MISS-B-B2", Name = "Bay 2 - TIG Welding", Bay = "Bay 2", Type = LocationType.Bay, ParentLocationId = 11, CompanyId = 2, SortOrder = 122 },
                new Location { Id = 30, Code = "BRAM-A", Name = "Main Building", Type = LocationType.Building, ParentLocationId = 2, CompanyId = 2, SortOrder = 210 },
                new Location { Id = 31, Code = "BRAM-WH", Name = "Warehouse", Type = LocationType.Storage, ParentLocationId = 2, CompanyId = 2, SortOrder = 220 },
                new Location { Id = 40, Code = "BURL-A", Name = "Main Building", Type = LocationType.Building, ParentLocationId = 3, CompanyId = 2, SortOrder = 310 },
                new Location { Id = 41, Code = "1601", Name = "1601 Corporate", Description = "Corporate office location", Type = LocationType.Building, ParentLocationId = 3, CompanyId = 2, SortOrder = 320 },
                new Location { Id = 50, Code = "DET-A", Name = "Main Production", Type = LocationType.Building, ParentLocationId = 4, CompanyId = 3, SortOrder = 410 },
                new Location { Id = 51, Code = "DET-WH", Name = "Warehouse", Type = LocationType.Storage, ParentLocationId = 4, CompanyId = 3, SortOrder = 420 },
                new Location { Id = 60, Code = "STORAGE", Name = "General Storage", Type = LocationType.Storage, CompanyId = 2, SortOrder = 900 },
                new Location { Id = 61, Code = "OFFSITE", Name = "Off-site / External", Type = LocationType.Storage, CompanyId = 2, SortOrder = 910 }
            };

            db.Locations.AddRange(locations);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("SELECT setval(pg_get_serial_sequence('\"Locations\"', 'Id'), (SELECT MAX(\"Id\") FROM \"Locations\"))");
            Console.WriteLine("[Seed] Seeded 21 locations");
        }

        private static async Task SeedIntercompanyGlAccountsAsync(AppDbContext db)
        {
            var intercompanyAccounts = new List<GlAccount>
            {
                new GlAccount { AccountNumber = "1940-IC", Name = "Due From Subsidiaries", Description = "Intercompany receivables from subsidiary companies", AccountType = GlAccountType.Asset, Category = GlAccountCategory.IntercompanyReceivables, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Debit, IsSystemAccount = true, AllowManualEntry = false, SortOrder = 50, CompanyId = 1 },
                new GlAccount { AccountNumber = "1941-IC", Name = "Due From PWH-CAN", Description = "Intercompany receivable from PWH Manufacturing Canada", AccountType = GlAccountType.Asset, Category = GlAccountCategory.IntercompanyReceivables, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Debit, SortOrder = 51, CompanyId = 1 },
                new GlAccount { AccountNumber = "1942-IC", Name = "Due From PWH-USA", Description = "Intercompany receivable from PWH Manufacturing USA", AccountType = GlAccountType.Asset, Category = GlAccountCategory.IntercompanyReceivables, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Debit, SortOrder = 52, CompanyId = 1 },
                new GlAccount { AccountNumber = "1945-IC", Name = "Investment in Subsidiaries", Description = "Parent company investment in subsidiary entities", AccountType = GlAccountType.Asset, Category = GlAccountCategory.InvestmentInSubsidiaries, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Debit, IsSystemAccount = true, AllowManualEntry = false, SortOrder = 60, CompanyId = 1 },
                new GlAccount { AccountNumber = "1946-IC", Name = "Investment in PWH-CAN", Description = "Investment in PWH Manufacturing Canada", AccountType = GlAccountType.Asset, Category = GlAccountCategory.InvestmentInSubsidiaries, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Debit, SortOrder = 61, CompanyId = 1 },
                new GlAccount { AccountNumber = "1947-IC", Name = "Investment in PWH-USA", Description = "Investment in PWH Manufacturing USA", AccountType = GlAccountType.Asset, Category = GlAccountCategory.InvestmentInSubsidiaries, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Debit, SortOrder = 62, CompanyId = 1 },
                new GlAccount { AccountNumber = "2150-IC", Name = "Due To Parent/Affiliates", Description = "Intercompany payables to parent or affiliate companies", AccountType = GlAccountType.Liability, Category = GlAccountCategory.IntercompanyPayables, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, IsSystemAccount = true, AllowManualEntry = false, SortOrder = 70, CompanyId = 1 },
                new GlAccount { AccountNumber = "2151-IC", Name = "Due To PWH Holdings", Description = "Intercompany payable to Prestige Worldwide Holdings", AccountType = GlAccountType.Liability, Category = GlAccountCategory.IntercompanyPayables, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, SortOrder = 71, CompanyId = 1 },
                new GlAccount { AccountNumber = "2152-IC", Name = "Due To PWH-CAN", Description = "Intercompany payable to PWH Manufacturing Canada", AccountType = GlAccountType.Liability, Category = GlAccountCategory.IntercompanyPayables, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, SortOrder = 72, CompanyId = 1 },
                new GlAccount { AccountNumber = "2153-IC", Name = "Due To PWH-USA", Description = "Intercompany payable to PWH Manufacturing USA", AccountType = GlAccountType.Liability, Category = GlAccountCategory.IntercompanyPayables, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, SortOrder = 73, CompanyId = 1 },
                new GlAccount { AccountNumber = "3500-IC", Name = "Intercompany Eliminations", Description = "Consolidated eliminations for intercompany transactions", AccountType = GlAccountType.Equity, Category = GlAccountCategory.IntercompanyEliminations, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, IsSystemAccount = true, AllowManualEntry = false, SortOrder = 80, CompanyId = 1 },
                new GlAccount { AccountNumber = "3510-IC", Name = "Elimination - Investments", Description = "Elimination of parent investment in subsidiaries", AccountType = GlAccountType.Equity, Category = GlAccountCategory.IntercompanyEliminations, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, SortOrder = 81, CompanyId = 1 },
                new GlAccount { AccountNumber = "3520-IC", Name = "Elimination - Intercompany Balances", Description = "Elimination of intercompany receivables and payables", AccountType = GlAccountType.Equity, Category = GlAccountCategory.IntercompanyEliminations, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, SortOrder = 82, CompanyId = 1 },
                new GlAccount { AccountNumber = "3600-IC", Name = "Currency Translation Adjustment", Description = "Foreign currency translation adjustment for multi-currency subsidiaries", AccountType = GlAccountType.Equity, Category = GlAccountCategory.CurrencyTranslation, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, IsSystemAccount = true, AllowManualEntry = false, SortOrder = 90, CompanyId = 1 },
                new GlAccount { AccountNumber = "3610-IC", Name = "CTA - PWH-CAN", Description = "Currency translation adjustment for PWH Manufacturing Canada (CAD)", AccountType = GlAccountType.Equity, Category = GlAccountCategory.CurrencyTranslation, SubCategory = GlAccountSubCategory.None, NormalBalance = NormalBalance.Credit, SortOrder = 91, CompanyId = 1 }
            };

            foreach (var account in intercompanyAccounts)
            {
                var exists = await db.GlAccounts.AnyAsync(a => a.AccountNumber == account.AccountNumber);
                if (!exists)
                {
                    db.GlAccounts.Add(account);
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
