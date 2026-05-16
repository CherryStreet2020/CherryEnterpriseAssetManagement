using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding
{
    /// <summary>
    /// Sprint 1 fixture seeder — generates the corrective WO history needed
    /// to populate the upcoming Pareto / MTBF / Reliability-tile dashboards
    /// without waiting for organic operator data. Produces a realistic 80/20
    /// failure-mode distribution across multiple assets and ~90 days of
    /// activity, with matching balanced JEs so dashboard cost tiles and the
    /// PR #93 Maintenance Spend report also light up.
    ///
    /// Idempotent: every seeded WorkOrderNumber begins with "FIX-S1-". A
    /// second run that finds at least one such WO short-circuits without
    /// re-seeding. To re-seed, an admin deletes the FIX-S1-* MaintenanceEvents
    /// (and their JE rows) manually first.
    /// </summary>
    public class Sprint1FixtureSeeder
    {
        private readonly AppDbContext _db;
        private readonly ILogger<Sprint1FixtureSeeder> _logger;

        public Sprint1FixtureSeeder(AppDbContext db, ILogger<Sprint1FixtureSeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public record SeedResult(
            int WorkOrdersCreated,
            int JournalEntriesCreated,
            decimal TotalLaborCost,
            decimal TotalMaterialsCost,
            Dictionary<string, int> WorkOrdersByFailureCode,
            string Message);

        public async Task<SeedResult> SeedAsync(int? requestedCompanyId = null)
        {
            // Idempotency guard
            var alreadySeeded = await _db.MaintenanceEvents
                .AnyAsync(m => m.WorkOrderNumber != null && m.WorkOrderNumber.StartsWith("FIX-S1-"));
            if (alreadySeeded)
            {
                var existingCount = await _db.MaintenanceEvents
                    .CountAsync(m => m.WorkOrderNumber != null && m.WorkOrderNumber.StartsWith("FIX-S1-"));
                _logger.LogInformation("Sprint1FixtureSeeder: {Count} FIX-S1-* WOs already present, skipping.", existingCount);
                return new SeedResult(0, 0, 0m, 0m, new(), $"Already seeded ({existingCount} FIX-S1-* WOs in dataset). To re-seed, delete them first.");
            }

            // Resolve the company we'll seed against. Prefer a passed-in id,
            // otherwise pick the first company that has at least one active
            // asset (skipping demo-empty tenants).
            int companyId = requestedCompanyId ?? await _db.Assets
                .Where(a => a.Active && a.CompanyId.HasValue)
                .GroupBy(a => a.CompanyId!.Value)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync();
            if (companyId == 0)
            {
                return new SeedResult(0, 0, 0m, 0m, new(), "No company with active assets found. Seed an asset first.");
            }

            // Pick 8 assets in that company. If the company doesn't have 8 yet,
            // fall back to whatever it has — we just spread WOs more thinly.
            var seedAssets = await _db.Assets
                .Where(a => a.CompanyId == companyId && a.Active)
                .OrderBy(a => a.Id)
                .Take(8)
                .Select(a => new { a.Id, a.AssetNumber, a.Description, a.SiteId })
                .ToListAsync();
            if (seedAssets.Count == 0)
            {
                return new SeedResult(0, 0, 0m, 0m, new(), $"Company {companyId} has no active assets.");
            }

            // Pick 8 failure codes from the seeded master. The Pareto chart
            // looks most compelling with a wide distribution, so favor codes
            // that span a few categories (mechanical, electrical, etc.).
            var failureCodes = await _db.FailureCodes
                .Where(fc => fc.IsActive)
                .OrderBy(fc => fc.Code)
                .Take(8)
                .Select(fc => new { fc.Id, fc.Code, fc.Name })
                .ToListAsync();
            if (failureCodes.Count == 0)
            {
                return new SeedResult(0, 0, 0m, 0m, new(), "No FailureCode master rows found. Seed the lookup data first.");
            }

            // 80/20 distribution: top 3 codes get 60% of WOs, next 3 get 30%,
            // remaining codes split the last 10%. Total = 30 WOs.
            var distribution = new[] { 8, 7, 5, 3, 3, 2, 1, 1 }; // sums to 30
            int totalWos = distribution.Sum();
            var rand = new Random(20260516); // deterministic seed so repeated runs (after wipe) produce identical demos

            // Resolve GL accounts we'll use on the JEs. Fail loud if any are
            // missing — the JE balance guard would refuse to save with
            // null accounts anyway, so surface the gap immediately.
            var materialsAcct = await ResolveGlAccountCodeAsync(companyId, GlAccountKind.MaintenanceMaterials);
            var inventoryAcct = await ResolveGlAccountCodeAsync(companyId, GlAccountKind.Inventory);
            var laborAcct = await ResolveGlAccountCodeAsync(companyId, GlAccountKind.MaintenanceLabor);
            var accruedAcct = await ResolveGlAccountCodeAsync(companyId, GlAccountKind.AccruedLabor);
            if (materialsAcct == null || inventoryAcct == null || laborAcct == null || accruedAcct == null)
            {
                return new SeedResult(0, 0, 0m, 0m, new(),
                    $"Missing GL account on company {companyId}: " +
                    $"MaintenanceMaterials={materialsAcct ?? "MISSING"}, " +
                    $"Inventory={inventoryAcct ?? "MISSING"}, " +
                    $"MaintenanceLabor={laborAcct ?? "MISSING"}, " +
                    $"AccruedLabor={accruedAcct ?? "MISSING"}.");
            }

            // Pre-pick lookup values for WO Type / Status etc. via raw enum
            // ints — the LookupValue FK is nullable on MaintenanceEvent so
            // we can leave it null on the seed rows; the legacy enum suffices.
            int woCreated = 0;
            int jeCreated = 0;
            decimal totalLabor = 0m;
            decimal totalMaterials = 0m;
            var byCode = new Dictionary<string, int>();

            int sequenceIdx = 0;
            var today = DateTime.UtcNow.Date;
            for (int codeIdx = 0; codeIdx < failureCodes.Count; codeIdx++)
            {
                var fc = failureCodes[codeIdx];
                var countForCode = distribution[codeIdx];
                byCode[fc.Code] = countForCode;
                for (int n = 0; n < countForCode; n++)
                {
                    // Asset rotation — different WOs land on different assets
                    // so per-asset MTBF (PR #110) gets multiple data points.
                    var asset = seedAssets[sequenceIdx % seedAssets.Count];
                    sequenceIdx++;

                    // Spread CompletedDate across the last 90 days. The
                    // RNG-based offset gives variety without being so noisy
                    // that adjacent WOs collide on the same minute (which
                    // would break MTBF gap computation).
                    int daysAgo = rand.Next(2, 90);
                    var completedAt = today.AddDays(-daysAgo).AddHours(rand.Next(7, 17)).AddMinutes(rand.Next(0, 60));
                    var startedAt = completedAt.AddHours(-rand.Next(2, 9));
                    var scheduledAt = startedAt.AddDays(-rand.Next(0, 4));

                    // Realistic per-WO costs. Labor 2-12 hrs @ $65-$110/hr.
                    // Materials varies more — cheap inspections ($25) up to
                    // major mechanical repairs ($800).
                    var laborHours = (decimal)Math.Round(2 + rand.NextDouble() * 10, 1);
                    var laborRate = 65m + (decimal)(rand.NextDouble() * 45);
                    var laborCost = Math.Round(laborHours * laborRate, 2);
                    var materialsCost = (decimal)Math.Round(25 + rand.NextDouble() * 775, 2);

                    // WorkOrderNumber: FIX-S1-{seq}-{code-prefix} — gives the
                    // operator a quick visual hint when scrolling the WO list
                    // that these are seeded rows. seq is zero-padded so they
                    // sort cleanly.
                    var woNumber = $"FIX-S1-{(woCreated + 1):D3}-{fc.Code}";

                    var me = new MaintenanceEvent
                    {
                        AssetId = asset.Id,
                        Type = MaintenanceType.Corrective,
                        Status = MaintenanceStatus.Completed,
                        Priority = (MaintenancePriority)((sequenceIdx % 3) + 1), // mix Low/Med/High
                        ScheduledDate = scheduledAt,
                        StartedAt = startedAt,
                        CompletedDate = completedAt,
                        WorkOrderNumber = woNumber,
                        Description = $"Corrective: {fc.Name} on {asset.AssetNumber}",
                        FailureCodeId = fc.Id,
                        FailureCode = fc.Name,
                        LaborCost = laborCost,
                        MaterialsCost = materialsCost,
                        PartsCost = materialsCost, // legacy alias
                        ActualCost = laborCost + materialsCost,
                        EstimatedCost = laborCost + materialsCost, // best-effort estimate (decimal, not nullable)
                        ApprovalStatus = WorkOrderApprovalStatus.Approved,
                        CreatedAt = scheduledAt
                    };
                    _db.MaintenanceEvents.Add(me);
                    await _db.SaveChangesAsync(); // need the Id for the JE Reference
                    woCreated++;
                    totalLabor += laborCost;
                    totalMaterials += materialsCost;

                    // Post the labor JE — DR MaintenanceLabor / CR WagesAccrued.
                    // Same shape PR #92 produces from the real labor-add path.
                    var laborTicks = scheduledAt.Ticks + n; // unique per WO
                    var laborRef = $"WO-LBR-{me.Id}-{laborTicks}";
                    var laborJe = new JournalEntry
                    {
                        BookId = null,
                        Batch = laborRef,
                        Period = int.Parse(completedAt.ToString("yyyyMM")),
                        PostingDate = completedAt,
                        Source = "WO-LBR",
                        Reference = laborRef,
                        Description = $"Labor for {woNumber} ({laborHours} hrs @ {laborRate:C}/hr)",
                        CreatedUtc = DateTime.UtcNow,
                        Lines = new List<JournalLine>
                        {
                            new() { LineNo = 1, Account = laborAcct, Description = $"Labor - {woNumber}", Debit = laborCost, Credit = 0m },
                            new() { LineNo = 2, Account = accruedAcct, Description = $"Wages accrued - {woNumber}", Debit = 0m, Credit = laborCost }
                        }
                    };
                    _db.JournalEntries.Add(laborJe);
                    jeCreated++;

                    // Post the materials JE — DR MaintenanceMaterials / CR
                    // Inventory. Use WO-ISS-OP so the new closeout rollup
                    // (PR #106) picks both this and the labor entry on close.
                    var matTicks = laborTicks + 1;
                    var matRef = $"WO-ISS-OP-{me.Id}-op0-p0-{matTicks}";
                    var matJe = new JournalEntry
                    {
                        BookId = null,
                        Batch = matRef,
                        Period = int.Parse(completedAt.ToString("yyyyMM")),
                        PostingDate = completedAt,
                        Source = "WO-ISS-OP",
                        Reference = matRef,
                        Description = $"Materials for {woNumber}",
                        CreatedUtc = DateTime.UtcNow,
                        Lines = new List<JournalLine>
                        {
                            new() { LineNo = 1, Account = materialsAcct, Description = $"Materials - {woNumber}", Debit = materialsCost, Credit = 0m },
                            new() { LineNo = 2, Account = inventoryAcct, Description = $"Inventory issued - {woNumber}", Debit = 0m, Credit = materialsCost }
                        }
                    };
                    _db.JournalEntries.Add(matJe);
                    jeCreated++;

                    await _db.SaveChangesAsync();
                }
            }

            _logger.LogInformation(
                "Sprint1FixtureSeeder: seeded {Wos} corrective WOs ({Jes} JEs, {Labor:C} labor, {Mat:C} materials) on company {Company}.",
                woCreated, jeCreated, totalLabor, totalMaterials, companyId);

            return new SeedResult(
                woCreated,
                jeCreated,
                totalLabor,
                totalMaterials,
                byCode,
                $"Seeded {woCreated} corrective WOs across {failureCodes.Count} failure codes and {seedAssets.Count} assets on company {companyId}. Total spend: {(totalLabor + totalMaterials):C} ({totalLabor:C} labor + {totalMaterials:C} materials).");
        }

        /// <summary>
        /// Best-effort GL account resolver. Walks the per-company config and
        /// falls back to the industry default. Returns null if no account
        /// can be resolved — the seeder bails in that case rather than
        /// producing unbalanced or null-account JEs.
        /// </summary>
        private async Task<string?> ResolveGlAccountCodeAsync(int companyId, GlAccountKind kind)
        {
            var cfg = await _db.Set<CompanyGlAccountConfig>()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.AccountKind == kind);
            if (cfg != null && !string.IsNullOrEmpty(cfg.GlAccount))
                return cfg.GlAccount;
            // Industry defaults align with IGlAccountResolver — kept here as
            // a static fallback so the seeder can run even when the cascade
            // resolver service isn't injected.
            return kind switch
            {
                GlAccountKind.MaintenanceMaterials => "6210",
                GlAccountKind.MaintenanceLabor => "6200",
                GlAccountKind.Inventory => "1300",
                GlAccountKind.AccruedLabor => "2210",
                _ => null
            };
        }
    }
}
