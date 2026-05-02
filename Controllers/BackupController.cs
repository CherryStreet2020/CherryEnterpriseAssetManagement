using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace Abs.FixedAssets.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public BackupController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportDatabase()
        {
            if (!_environment.IsDevelopment())
            {
                return BadRequest(new { error = "Database export only available in Development environment" });
            }

            var allowBackup = Environment.GetEnvironmentVariable("ALLOW_DB_BACKUP");
            if (allowBackup?.Equals("true", StringComparison.OrdinalIgnoreCase) != true)
            {
                return BadRequest(new { error = "Set ALLOW_DB_BACKUP=true to enable database exports" });
            }

            try
            {
                var backup = new Dictionary<string, object>();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

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
                        
                        var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                            .GetMethods()
                            .First(m => m.Name == "ToListAsync" && m.GetParameters().Length == 2);
                        
                        var genericMethod = toListAsyncMethod.MakeGenericMethod(entityType);
                        dynamic task = genericMethod.Invoke(null, new object[] { dbSet, CancellationToken.None })!;
                        var data = await task;

                        backup[prop.Name] = data;
                    }
                    catch
                    {
                        backup[prop.Name] = new { error = "Failed to export" };
                    }
                }

                backup["_metadata"] = new
                {
                    exportedAt = DateTime.UtcNow,
                    environment = _environment.EnvironmentName,
                    database = Environment.GetEnvironmentVariable("PGDATABASE"),
                    tableCount = backup.Count - 1
                };

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var filename = $"db_backup_{timestamp}.json";
                
                var json = JsonSerializer.Serialize(backup, options);
                return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", filename);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                environment = _environment.EnvironmentName,
                isDevelopment = _environment.IsDevelopment(),
                database = Environment.GetEnvironmentVariable("PGDATABASE"),
                backupEnabled = Environment.GetEnvironmentVariable("ALLOW_DB_BACKUP")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
                resetProtected = Environment.GetEnvironmentVariable("ALLOW_DB_RESET")?.Equals("true", StringComparison.OrdinalIgnoreCase) != true
            });
        }
    }
}
