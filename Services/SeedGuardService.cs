using System.Data;
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Abs.FixedAssets.Services
{
    public interface ISeedGuardService
    {
        SeedGuardResult CheckSeedPermission();
        string GetEnvironmentProfile();
        string GetMaskedConnectionString();
        bool IsLabEnvironment();
        bool IsDemoEnvironment();
        bool IsDemoDataEnabled();

        /// <summary>
        /// Tries to acquire a Postgres session-level advisory lock on
        /// <see cref="SeedGuardService.SeedLockKey"/>. Returns true if
        /// acquired, false if another process already holds it. The lock
        /// must be released via <see cref="ReleaseSeedLockAsync"/> before
        /// the connection closes.
        /// </summary>
        Task<bool> TryAcquireSeedLockAsync(AppDbContext db, CancellationToken ct = default);

        /// <summary>
        /// Releases the Postgres session-level advisory lock acquired by
        /// <see cref="TryAcquireSeedLockAsync"/>. Safe to call even if the
        /// lock is not currently held; Postgres returns false in that case.
        /// </summary>
        Task ReleaseSeedLockAsync(AppDbContext db, CancellationToken ct = default);
    }

    public class SeedGuardResult
    {
        public bool Allowed { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string EnvironmentProfile { get; set; } = string.Empty;
        public List<string> FailedChecks { get; set; } = new();
        public List<string> PassedChecks { get; set; } = new();
    }

    public class SeedGuardService : ISeedGuardService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        // Constant key used for the Postgres session-level advisory lock
        // that serializes startup-time seeding across concurrent app
        // instances. Arbitrary 64-bit value, app-specific. If multiple
        // CherryAI EAM apps ever share a database (they shouldn't — each
        // tenant gets its own DB — but just in case), we want the same
        // key everywhere, so it's a constant rather than derived.
        public const long SeedLockKey = 4815162342L;

        public SeedGuardService(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        public async Task<bool> TryAcquireSeedLockAsync(AppDbContext db, CancellationToken ct = default)
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT pg_try_advisory_lock(@key)";
            var p = cmd.CreateParameter();
            p.ParameterName = "@key";
            p.Value = SeedLockKey;
            cmd.Parameters.Add(p);

            var raw = await cmd.ExecuteScalarAsync(ct);
            return raw is bool acquired && acquired;
        }

        public async Task ReleaseSeedLockAsync(AppDbContext db, CancellationToken ct = default)
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                return; // nothing to release if the connection's already gone

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT pg_advisory_unlock(@key)";
            var p = cmd.CreateParameter();
            p.ParameterName = "@key";
            p.Value = SeedLockKey;
            cmd.Parameters.Add(p);
            await cmd.ExecuteScalarAsync(ct);
        }

        public SeedGuardResult CheckSeedPermission()
        {
            var result = new SeedGuardResult
            {
                EnvironmentProfile = GetEnvironmentProfile()
            };

            if (!_env.IsDevelopment())
            {
                result.FailedChecks.Add("ASPNETCORE_ENVIRONMENT must be 'Development'");
                result.Reason = "Seeding blocked: Not in Development environment";
                result.Allowed = false;
                return result;
            }
            result.PassedChecks.Add("Environment is Development");

            if (IsDemoEnvironment())
            {
                var allowDemoSeed = _configuration["ALLOW_DEMO_SEED"];
                if (string.IsNullOrEmpty(allowDemoSeed) || !allowDemoSeed.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    result.FailedChecks.Add("ALLOW_DEMO_SEED must be 'true' to seed DEMO database");
                    result.Reason = "Seeding blocked: DEMO environment protected - set ALLOW_DEMO_SEED=true to override";
                    result.Allowed = false;
                    return result;
                }
                result.PassedChecks.Add("ALLOW_DEMO_SEED=true override present");
            }

            var dbUrl = _configuration["DATABASE_URL"] ?? _configuration.GetConnectionString("DefaultConnection") ?? "";
            var dbName = ExtractDatabaseName(dbUrl);
            
            if (!string.IsNullOrEmpty(dbName))
            {
                if (dbName.Contains("lab", StringComparison.OrdinalIgnoreCase))
                {
                    result.PassedChecks.Add($"Database name contains 'lab': {dbName}");
                }
                else if (dbName.Contains("demo", StringComparison.OrdinalIgnoreCase) || 
                         dbName.Contains("prod", StringComparison.OrdinalIgnoreCase))
                {
                    var allowDemoSeed = _configuration["ALLOW_DEMO_SEED"];
                    if (string.IsNullOrEmpty(allowDemoSeed) || !allowDemoSeed.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        result.FailedChecks.Add($"Database '{dbName}' appears protected - requires ALLOW_DEMO_SEED=true");
                        result.Reason = $"Seeding blocked: Database name '{dbName}' suggests protected environment";
                        result.Allowed = false;
                        return result;
                    }
                    result.PassedChecks.Add($"Protected database '{dbName}' with ALLOW_DEMO_SEED override");
                }
                else
                {
                    result.PassedChecks.Add($"Database: {dbName}");
                }
            }

            result.Allowed = true;
            result.Reason = "All seed guards passed";
            return result;
        }

        public string GetEnvironmentProfile()
        {
            var dbUrl = _configuration["DATABASE_URL"] ?? "";
            var dbName = ExtractDatabaseName(dbUrl);
            
            if (dbName.Contains("lab", StringComparison.OrdinalIgnoreCase))
                return "LAB";
            if (dbName.Contains("demo", StringComparison.OrdinalIgnoreCase))
                return "DEMO";
            if (dbName.Contains("prod", StringComparison.OrdinalIgnoreCase))
                return "PRODUCTION";
            
            var envProfile = _configuration["ENVIRONMENT_PROFILE"];
            if (!string.IsNullOrEmpty(envProfile))
                return envProfile.ToUpper();

            return _env.IsDevelopment() ? "LAB" : "UNKNOWN";
        }

        public string GetMaskedConnectionString()
        {
            var dbUrl = _configuration["DATABASE_URL"] ?? "";
            if (string.IsNullOrEmpty(dbUrl)) return "(not configured)";

            try
            {
                var uri = new Uri(dbUrl);
                var maskedPassword = new string('*', 8);
                var maskedUser = uri.UserInfo.Split(':').FirstOrDefault() ?? "***";
                return $"{uri.Scheme}://{maskedUser}:{maskedPassword}@{uri.Host}:{uri.Port}{uri.AbsolutePath}";
            }
            catch
            {
                if (dbUrl.Length > 20)
                    return dbUrl.Substring(0, 15) + "..." + new string('*', 10);
                return new string('*', dbUrl.Length);
            }
        }

        public bool IsLabEnvironment()
        {
            return GetEnvironmentProfile() == "LAB";
        }

        public bool IsDemoEnvironment()
        {
            var profile = GetEnvironmentProfile();
            return profile == "DEMO" || profile == "PRODUCTION";
        }

        public bool IsDemoDataEnabled()
        {
            var envVar = Environment.GetEnvironmentVariable("DEMO_DATA_ENABLED");
            if (!string.IsNullOrEmpty(envVar))
                return envVar.Equals("true", StringComparison.OrdinalIgnoreCase);

            var configValue = _configuration.GetValue<bool>("DemoData:Enabled");
            return configValue;
        }

        private string ExtractDatabaseName(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return "";

            try
            {
                if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
                {
                    var uri = new Uri(connectionString);
                    return uri.AbsolutePath.TrimStart('/');
                }

                var parts = connectionString.Split(';');
                foreach (var part in parts)
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2 && kv[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase))
                    {
                        return kv[1].Trim();
                    }
                }
            }
            catch { }

            return "";
        }
    }
}
