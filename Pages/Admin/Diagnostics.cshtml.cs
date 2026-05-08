using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using System.Reflection;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DiagnosticsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public DiagnosticsModel(AppDbContext context, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
        }

        public string EnvironmentName { get; set; } = string.Empty;
        public string DatabaseHost { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string AppVersion { get; set; } = string.Empty;
        public string CommitHash { get; set; } = string.Empty;
        public bool IsDangerZone { get; set; }
        public string DangerReason { get; set; } = string.Empty;
        public List<MigrationInfo> AppliedMigrations { get; set; } = new();
        public List<EntityCountInfo> EntityCounts { get; set; } = new();
        public int TotalTableCount { get; set; }
        
        // EF Warning tracking
        public bool ThrowOnFirstWarning { get; set; }
        public bool AutoSeedEnabled { get; set; }
        public string SeedingStatus { get; set; } = string.Empty;
        
        // Core Loop Health
        public int AssetCount { get; set; }
        public int PMTemplateCount { get; set; }
        public int PMTemplateAssetCount { get; set; }
        public int MaintenanceEventCount { get; set; }
        public int PMTALinkedWOCount { get; set; }
        public bool CoreLoopReady => PMTemplateCount > 0 && PMTemplateAssetCount > 0;
        
        // Seed Coverage Health
        public List<MasterHealthItem> CriticalMasters { get; set; } = new();
        public List<BulkOperation> RecentBulkOperations { get; set; } = new();
        public DateTime? LastSeedRun { get; set; }
        public int CriticalMissingCount => CriticalMasters.Count(m => !m.IsHealthy && m.IsCritical);
        public int WarningCount => CriticalMasters.Count(m => !m.IsHealthy && !m.IsCritical);

        public async Task OnGetAsync()
        {
            EnvironmentName = _environment.EnvironmentName;
            DatabaseHost = Environment.GetEnvironmentVariable("PGHOST") ?? "unknown";
            DatabaseName = Environment.GetEnvironmentVariable("PGDATABASE") ?? "unknown";
            
            // Warning/Seeding configuration status
            ThrowOnFirstWarning = Environment.GetEnvironmentVariable("THROW_EF_FIRST_WARNING")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            AutoSeedEnabled = Environment.GetEnvironmentVariable("AUTO_SEED_ON_EMPTY")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            var runSeedEnabled = Environment.GetEnvironmentVariable("RUN_SEED")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            SeedingStatus = runSeedEnabled ? "RUN_SEED=true (will seed on restart)" : (AutoSeedEnabled ? "AUTO_SEED_ON_EMPTY=true" : "Disabled (safe mode)");
            
            var assembly = Assembly.GetExecutingAssembly();
            AppVersion = assembly.GetName().Version?.ToString() ?? "1.0.0";
            CommitHash = Environment.GetEnvironmentVariable("REPL_ID") ?? "local";

            IsDangerZone = EnvironmentName != "Development" || 
                           (!DatabaseName.Contains("dev", StringComparison.OrdinalIgnoreCase) && 
                            !DatabaseName.Contains("helium", StringComparison.OrdinalIgnoreCase));
            
            if (IsDangerZone)
            {
                DangerReason = EnvironmentName != "Development" 
                    ? $"Environment is '{EnvironmentName}' (not Development)" 
                    : $"Database name '{DatabaseName}' doesn't match expected dev naming";
            }

            await LoadMigrationsAsync();
            await LoadEntityCountsAsync();
            await LoadTableCountAsync();
            await LoadCoreLoopHealthAsync();
            await LoadSeedCoverageHealthAsync();
        }
        
        private async Task LoadSeedCoverageHealthAsync()
        {
            try
            {
                CriticalMasters = new List<MasterHealthItem>
                {
                    new("Companies", await _context.Companies.CountAsync(), 1, true),
                    new("Sites", await _context.Sites.CountAsync(), 1, true),
                    new("Locations", await _context.Locations.CountAsync(), 1, false),
                    new("GlAccounts", await _context.GlAccounts.CountAsync(), 10, true),
                    new("Departments", await _context.Departments.CountAsync(), 1, false),
                    new("CostCenters", await _context.CostCenters.CountAsync(), 1, false),
                    new("AssetCategories", await _context.AssetCategories.CountAsync(), 1, true),
                    new("Vendors", await _context.Vendors.CountAsync(), 1, false),
                    new("Items", await _context.Items.CountAsync(), 0, false),
                    new("Assets", await _context.Assets.CountAsync(), 0, false),
                    new("Technicians", await _context.Technicians.CountAsync(), 1, false),
                    new("PMTemplates", await _context.PMTemplates.CountAsync(), 0, false),
                    new("WorkOrderTypes", await _context.WorkOrderTypes.CountAsync(), 1, true),
                    new("FailureCodes", await _context.FailureCodes.CountAsync(), 1, false),
                    new("PriorityLevels", await _context.PriorityLevels.CountAsync(), 1, true),
                    new("Currencies", await _context.Currencies.CountAsync(), 1, true),
                    new("PaymentTerms", await _context.PaymentTerms.CountAsync(), 1, false),
                    new("NumberingSequences", await _context.NumberingSequences.CountAsync(), 1, true),
                };
                
                RecentBulkOperations = await _context.BulkOperations
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(10)
                    .ToListAsync();
                
                var seedOp = await _context.BulkOperations
                    .Where(b => b.Description != null && b.Description.Contains("SEED PIPELINE"))
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();
                
                LastSeedRun = seedOp?.CreatedAt;
            }
            catch
            {
                // Silently fail - seed coverage health will show empty
            }
        }
        
        private async Task LoadCoreLoopHealthAsync()
        {
            try
            {
                AssetCount = await _context.Assets.CountAsync();
                PMTemplateCount = await _context.Set<Models.PMTemplate>().CountAsync();
                PMTemplateAssetCount = await _context.Set<Models.PMTemplateAsset>().CountAsync();
                MaintenanceEventCount = await _context.MaintenanceEvents.CountAsync();
                // S1-2: count via FK (preferred) OR legacy CustomField1 marker
                // for in-flight rows from before the FK migration.
                PMTALinkedWOCount = await _context.MaintenanceEvents
                    .Where(e => e.PMTemplateAssetId != null
                        || (e.CustomField1 != null && e.CustomField1.StartsWith("PMTA:")))
                    .CountAsync();
            }
            catch
            {
                // Silently fail - counts remain 0
            }
        }

        private async Task LoadMigrationsAsync()
        {
            try
            {
                var sql = "SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC";
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    AppliedMigrations.Add(new MigrationInfo
                    {
                        MigrationId = reader.GetString(0),
                        ProductVersion = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                AppliedMigrations.Add(new MigrationInfo { MigrationId = $"Error: {ex.Message}", ProductVersion = "" });
            }
        }

        private async Task LoadEntityCountsAsync()
        {
            var dbSetProperties = typeof(AppDbContext)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType && 
                           p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .OrderBy(p => p.Name);

            foreach (var prop in dbSetProperties)
            {
                try
                {
                    var dbSet = prop.GetValue(_context);
                    if (dbSet == null) continue;

                    var entityType = prop.PropertyType.GetGenericArguments()[0];
                    var countMethod = typeof(EntityFrameworkQueryableExtensions)
                        .GetMethods()
                        .First(m => m.Name == "CountAsync" && m.GetParameters().Length == 2);
                    
                    var genericMethod = countMethod.MakeGenericMethod(entityType);
                    var task = (Task<int>)genericMethod.Invoke(null, new object[] { dbSet, CancellationToken.None })!;
                    var count = await task;

                    EntityCounts.Add(new EntityCountInfo
                    {
                        EntityName = prop.Name,
                        Count = count
                    });
                }
                catch
                {
                    EntityCounts.Add(new EntityCountInfo
                    {
                        EntityName = prop.Name,
                        Count = -1
                    });
                }
            }

            EntityCounts = EntityCounts.OrderByDescending(e => e.Count).ToList();
        }

        private async Task LoadTableCountAsync()
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'";
                var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                var result = await command.ExecuteScalarAsync();
                TotalTableCount = Convert.ToInt32(result);
            }
            catch
            {
                TotalTableCount = -1;
            }
        }

        public class MigrationInfo
        {
            public string MigrationId { get; set; } = string.Empty;
            public string ProductVersion { get; set; } = string.Empty;
        }

        public class EntityCountInfo
        {
            public string EntityName { get; set; } = string.Empty;
            public int Count { get; set; }
        }
        
        public class MasterHealthItem
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public int MinRequired { get; set; }
            public bool IsCritical { get; set; }
            public bool IsHealthy => Count >= MinRequired;
            public string Status => IsHealthy ? "OK" : (IsCritical ? "CRITICAL" : "WARNING");
            public string CssClass => IsHealthy ? "health-ok" : (IsCritical ? "health-critical" : "health-warning");

            public MasterHealthItem(string name, int count, int minRequired, bool isCritical)
            {
                Name = name;
                Count = count;
                MinRequired = minRequired;
                IsCritical = isCritical;
            }
        }
    }
}
