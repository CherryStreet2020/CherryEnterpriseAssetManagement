using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding
{
    // ADR-012 v0.2 / PR #119.5 — Primes the NumberSequence table.
    //
    // Seeds one global (TenantId NULL) row per (Classification, current year)
    // with the industry-standard prefix:
    //   PM-{YYYY}-{NNNN}   Maintenance
    //   NCR-{YYYY}-{NNNN}  Quality
    //   ECO-{YYYY}-{NNNN}  Engineering
    //   INC-{YYYY}-{NNNN}  HSE
    //   AFE-{YYYY}-{NNNN}  CIP
    //
    // NumberSequenceService.NextAsync can auto-create rows on demand
    // for unseen years/tenants, but pre-seeding the current year keeps
    // the first-WO-of-the-day fast (no auto-create transaction needed).
    //
    // Idempotent: bails if any row exists for the current year.
    public interface INumberSequenceSeeder
    {
        Task<int> SeedAsync(bool forceReseed = false);
    }

    public class NumberSequenceSeeder : INumberSequenceSeeder
    {
        private readonly AppDbContext _db;
        private readonly ILogger<NumberSequenceSeeder> _logger;

        public NumberSequenceSeeder(AppDbContext db, ILogger<NumberSequenceSeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> SeedAsync(bool forceReseed = false)
        {
            var year = DateTime.UtcNow.Year;

            var existing = await _db.Set<NumberSequence>()
                .Where(r => r.Year == year && r.TenantId == null)
                .CountAsync();
            if (existing > 0 && !forceReseed)
            {
                _logger.LogInformation(
                    "NumberSequenceSeeder: skipping, {Count} global rows for {Year} already present.",
                    existing, year);
                return 0;
            }

            if (forceReseed && existing > 0)
            {
                var globals = _db.Set<NumberSequence>()
                    .Where(r => r.Year == year && r.TenantId == null);
                _db.Set<NumberSequence>().RemoveRange(globals);
                await _db.SaveChangesAsync();
            }

            var now = DateTime.UtcNow;
            var rows = new List<NumberSequence>
            {
                Row(WorkOrderClassification.Maintenance, year, "PM",  now),
                Row(WorkOrderClassification.Quality,     year, "NCR", now),
                Row(WorkOrderClassification.Engineering, year, "ECO", now),
                Row(WorkOrderClassification.HSE,         year, "INC", now),
                Row(WorkOrderClassification.CIP,         year, "AFE", now),
            };
            await _db.Set<NumberSequence>().AddRangeAsync(rows);
            var saved = await _db.SaveChangesAsync();
            _logger.LogInformation(
                "NumberSequenceSeeder: seeded {Count} global rows for year {Year}.",
                saved, year);
            return saved;
        }

        private static NumberSequence Row(
            WorkOrderClassification cls, int year, string prefix, DateTime now) =>
            new()
            {
                Classification = cls,
                Year = year,
                Prefix = prefix,
                CurrentValue = 0,
                Padding = 4,
                YearSeparator = "-",
                CounterSeparator = "-",
                TenantId = null,
                CreatedAt = now,
                UpdatedAt = now,
            };
    }
}
