using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests
{
    public class CcaBackfillServiceTests
    {
        // InMemory provider can't map JsonDocument (LookupValue.Metadata is jsonb).
        private sealed class TestAppDbContext : AppDbContext
        {
            public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
            protected override void OnModelCreating(ModelBuilder mb)
            {
                base.OnModelCreating(mb);
                mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            }
        }

        private static AppDbContext NewDb(string dbName)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new TestAppDbContext(opts);
        }

        // Mirror the production CcaClass seed (subset is enough for tests, but seeding all 25
        // ensures CcaClassSuggester always finds its target class).
        private static void SeedCcaClasses(AppDbContext db)
        {
            if (db.CcaClasses.Any()) return;
            db.CcaClasses.AddRange(
                new CcaClass { ClassNumber = 1, Rate = 0.04m, Description = "Buildings", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new CcaClass { ClassNumber = 8, Rate = 0.20m, Description = "General machinery", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new CcaClass { ClassNumber = 10, Rate = 0.30m, Description = "Automotive", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new CcaClass { ClassNumber = 12, Rate = 1.00m, Description = "Tools <$500 / software", IsDecliningBalance = true, HalfYearRuleApplies = false },
                new CcaClass { ClassNumber = 17, Rate = 0.08m, Description = "Land improvements", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new CcaClass { ClassNumber = 29, Rate = 0.50m, Description = "Pre-2016 manufacturing", IsDecliningBalance = false, HalfYearRuleApplies = true },
                new CcaClass { ClassNumber = 39, Rate = 0.25m, Description = "Post-2015 generic M&P", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new CcaClass { ClassNumber = 50, Rate = 0.55m, Description = "Computers post-2011", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new CcaClass { ClassNumber = 53, Rate = 0.50m, Description = "Manufacturing post-2015", IsDecliningBalance = true, HalfYearRuleApplies = true, IsAcceleratedInvestmentIncentive = true }
            );
            db.SaveChanges();
        }

        private static Company SeedCanadianCompany(AppDbContext db, int id = 99)
        {
            var co = new Company
            {
                Id = id,
                Name = "TEST CANADIAN MFG",
                CompanyCode = "TST-CAN",
                Country = "CANADA",
                IsActive = true
            };
            db.Companies.Add(co);
            db.SaveChanges();
            return co;
        }

        private static Asset AddAsset(AppDbContext db, int companyId, string number, string type, decimal cost, DateTime inService)
        {
            var asset = new Asset
            {
                AssetNumber = number,
                Description = $"{type} asset {number}",
                AssetType = type,
                AcquisitionCost = cost,
                InServiceDate = inService,
                UsefulLifeMonths = 60,
                CompanyId = companyId,
                Active = true,
                Status = AssetStatus.Active
            };
            db.Assets.Add(asset);
            db.SaveChanges();
            return asset;
        }

        private static CcaBackfillService MakeService(AppDbContext db)
        {
            var tenantOverride = new TenantContextOverride();
            var tenantContext = new TenantContext(tenantOverride);
            tenantContext.SetContext(1, 99, null);
            var ccaService = new CcaService(db, tenantContext);
            var audit = new AuditService(db);
            return new CcaBackfillService(
                db, ccaService, tenantContext, tenantOverride, audit,
                NullLogger<CcaBackfillService>.Instance);
        }

        // ─────────────────────────────────────────────────────────────────
        // CcaClassSuggester — pure logic
        // ─────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("CNC MACHINE", 2024, CcaClassSuggester.Class53_ManufacturingPost2015)]
        [InlineData("GRINDER", 2020, CcaClassSuggester.Class53_ManufacturingPost2015)]
        [InlineData("CNC MACHINE", 2010, CcaClassSuggester.Class29_PreManufacturing2016)]
        [InlineData("FORKLIFT", 2024, CcaClassSuggester.Class10_AutomotiveAndOldComputers)]
        [InlineData("COMPUTER", 2024, CcaClassSuggester.Class50_GeneralComputers)]
        [InlineData("SOFTWARE", 2024, CcaClassSuggester.Class12_FullWriteoff)]
        [InlineData("BUILDING", 2024, CcaClassSuggester.Class1_BuildingsPost1987)]
        [InlineData("OFFICE FURNITURE", 2024, CcaClassSuggester.Class8_GeneralMachinery)]
        [InlineData("PARKING LOT", 2024, CcaClassSuggester.Class17_LandImprovements)]
        [InlineData("UNSPECIFIED", 2024, CcaClassSuggester.Class8_GeneralMachinery)]
        [InlineData("", 2024, CcaClassSuggester.Class8_GeneralMachinery)]
        public void Suggester_KnownTypes_ReturnsExpectedClass(string assetType, int year, int expectedClass)
        {
            var asset = new Asset
            {
                AssetNumber = "T-001",
                Description = "Test",
                AssetType = assetType,
                AcquisitionCost = 50000m,
                InServiceDate = new DateTime(year, 6, 1)
            };
            Assert.Equal(expectedClass, CcaClassSuggester.Suggest(asset));
        }

        [Fact]
        public void Suggester_CheapTool_GoesToClass12()
        {
            var asset = new Asset
            {
                AssetNumber = "T-002",
                Description = "Cheap die",
                AssetType = "DIE",
                AcquisitionCost = 350m,
                InServiceDate = new DateTime(2024, 1, 1)
            };
            Assert.Equal(CcaClassSuggester.Class12_FullWriteoff, CcaClassSuggester.Suggest(asset));
        }

        [Fact]
        public void Suggester_ExpensiveTool_FallsBackToClass8()
        {
            var asset = new Asset
            {
                AssetNumber = "T-003",
                Description = "Expensive jig",
                AssetType = "JIG",
                AcquisitionCost = 5000m,
                InServiceDate = new DateTime(2024, 1, 1)
            };
            Assert.Equal(CcaClassSuggester.Class8_GeneralMachinery, CcaClassSuggester.Suggest(asset));
        }

        // ─────────────────────────────────────────────────────────────────
        // CcaBackfillService — end-to-end
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task FirstRun_CreatesAssetTaxSettingsAndOpeningTransaction()
        {
            using var db = NewDb(nameof(FirstRun_CreatesAssetTaxSettingsAndOpeningTransaction));
            SeedCcaClasses(db);
            var co = SeedCanadianCompany(db);
            AddAsset(db, co.Id, "A-1", "CNC MACHINE", 100_000m, new DateTime(2024, 1, 1));
            AddAsset(db, co.Id, "A-2", "FORKLIFT", 30_000m, new DateTime(2024, 6, 15));
            AddAsset(db, co.Id, "A-3", "COMPUTER", 5_000m, new DateTime(2023, 1, 1));

            var svc = MakeService(db);
            var report = await svc.RunAsync(co.Id, throughFiscalYear: 2024, computeBalances: false);

            Assert.Equal(3, report.AssetsScanned);
            Assert.Equal(3, report.AssetsMapped);
            Assert.Equal(0, report.AssetsAlreadyMapped);
            Assert.Empty(report.Errors);

            Assert.Equal(3, await db.AssetTaxSettings.CountAsync());
            Assert.Equal(3, await db.CcaTransactions.CountAsync(t => t.TransactionType == CcaTransactionType.Addition));

            var class53Id = await db.CcaClasses.Where(c => c.ClassNumber == 53).Select(c => c.Id).FirstAsync();
            var cncSettings = await db.AssetTaxSettings.FirstAsync(t => t.Asset!.AssetNumber == "A-1");
            Assert.Equal(class53Id, cncSettings.CcaClassId);
            Assert.True(cncSettings.EligibleForAcceleratedIncentive);
            Assert.Equal(100_000m, cncSettings.CapitalCost);
        }

        [Fact]
        public async Task SecondRun_IsIdempotent_CreatesZeroNewSettings()
        {
            using var db = NewDb(nameof(SecondRun_IsIdempotent_CreatesZeroNewSettings));
            SeedCcaClasses(db);
            var co = SeedCanadianCompany(db);
            AddAsset(db, co.Id, "A-1", "CNC MACHINE", 100_000m, new DateTime(2024, 1, 1));
            AddAsset(db, co.Id, "A-2", "GRINDER", 50_000m, new DateTime(2023, 6, 1));

            var svc = MakeService(db);
            var firstReport = await svc.RunAsync(co.Id, throughFiscalYear: 2024);
            Assert.Equal(2, firstReport.AssetsMapped);

            var firstSettingsCount = await db.AssetTaxSettings.CountAsync();
            var firstTransactionsCount = await db.CcaTransactions.CountAsync();
            var firstBalancesCount = await db.CcaClassBalances.CountAsync();

            var secondReport = await svc.RunAsync(co.Id, throughFiscalYear: 2024);
            Assert.Equal(0, secondReport.AssetsMapped);
            Assert.Equal(2, secondReport.AssetsAlreadyMapped);

            Assert.Equal(firstSettingsCount, await db.AssetTaxSettings.CountAsync());
            Assert.Equal(firstTransactionsCount, await db.CcaTransactions.CountAsync());
            // Balances row count is the same — recompute updates rows in place rather than insert.
            Assert.Equal(firstBalancesCount, await db.CcaClassBalances.CountAsync());
        }

        [Fact]
        public async Task Class53_FirstYear_AppliesAIIWithoutHalfYearAdjustment()
        {
            using var db = NewDb(nameof(Class53_FirstYear_AppliesAIIWithoutHalfYearAdjustment));
            SeedCcaClasses(db);
            var co = SeedCanadianCompany(db);
            AddAsset(db, co.Id, "A-1", "CNC MACHINE", 100_000m, new DateTime(2024, 3, 15));

            var svc = MakeService(db);
            var report = await svc.RunAsync(co.Id, throughFiscalYear: 2024);
            Assert.Empty(report.Errors);

            var class53Id = await db.CcaClasses.Where(c => c.ClassNumber == 53).Select(c => c.Id).FirstAsync();
            var balance = await db.CcaClassBalances.SingleAsync(b => b.CcaClassId == class53Id && b.FiscalYear == 2024);

            // AII drops half-year adjustment to 0; first-year CCA = 100,000 × 50% = 50,000.
            Assert.Equal(0m, balance.OpeningUcc);
            Assert.Equal(100_000m, balance.Additions);
            Assert.Equal(0m, balance.HalfYearAdjustment);
            Assert.Equal(100_000m, balance.BaseForCca);
            Assert.Equal(50_000m, balance.CcaClaimed);
            Assert.Equal(50_000m, balance.ClosingUcc);
        }

        [Fact]
        public async Task RepeatComputeBalances_ProducesSameClosingUcc()
        {
            using var db = NewDb(nameof(RepeatComputeBalances_ProducesSameClosingUcc));
            SeedCcaClasses(db);
            var co = SeedCanadianCompany(db);
            AddAsset(db, co.Id, "A-1", "CNC MACHINE", 100_000m, new DateTime(2023, 1, 1));

            var svc = MakeService(db);
            var first = await svc.RunAsync(co.Id, throughFiscalYear: 2025);
            Assert.Empty(first.Errors);

            var class53Id = await db.CcaClasses.Where(c => c.ClassNumber == 53).Select(c => c.Id).FirstAsync();
            var snapshotBefore = await db.CcaClassBalances
                .Where(b => b.CcaClassId == class53Id)
                .OrderBy(b => b.FiscalYear)
                .Select(b => new { b.FiscalYear, b.ClosingUcc, b.CcaClaimed })
                .ToListAsync();
            Assert.Equal(3, snapshotBefore.Count);
            Assert.True(snapshotBefore[0].CcaClaimed > 0);

            // Second run on the same data must yield identical closing UCC + CCA claimed
            // for every (class, year), even though balances are recomputed in place.
            var second = await svc.RunAsync(co.Id, throughFiscalYear: 2025);
            Assert.Empty(second.Errors);

            var snapshotAfter = await db.CcaClassBalances
                .Where(b => b.CcaClassId == class53Id)
                .OrderBy(b => b.FiscalYear)
                .Select(b => new { b.FiscalYear, b.ClosingUcc, b.CcaClaimed })
                .ToListAsync();

            Assert.Equal(snapshotBefore.Count, snapshotAfter.Count);
            for (int i = 0; i < snapshotBefore.Count; i++)
            {
                Assert.Equal(snapshotBefore[i].FiscalYear, snapshotAfter[i].FiscalYear);
                Assert.Equal(snapshotBefore[i].ClosingUcc, snapshotAfter[i].ClosingUcc);
                Assert.Equal(snapshotBefore[i].CcaClaimed, snapshotAfter[i].CcaClaimed);
            }
        }

        [Fact]
        public async Task AdminOverride_BeatsSuggestion()
        {
            using var db = NewDb(nameof(AdminOverride_BeatsSuggestion));
            SeedCcaClasses(db);
            var co = SeedCanadianCompany(db);
            var asset = AddAsset(db, co.Id, "A-1", "CNC MACHINE", 100_000m, new DateTime(2024, 1, 1));

            var class8Id = await db.CcaClasses.Where(c => c.ClassNumber == 8).Select(c => c.Id).FirstAsync();
            var overrides = new Dictionary<int, int> { { asset.Id, class8Id } };

            var svc = MakeService(db);
            var report = await svc.RunAsync(co.Id, throughFiscalYear: 2024,
                overrideClassByAssetId: overrides, computeBalances: false);

            Assert.Equal(1, report.AssetsMapped);
            var settings = await db.AssetTaxSettings.SingleAsync();
            Assert.Equal(class8Id, settings.CcaClassId);
        }

        [Fact]
        public async Task TwoCanadianCompanies_BalancesAreIsolatedPerCompany()
        {
            using var db = NewDb(nameof(TwoCanadianCompanies_BalancesAreIsolatedPerCompany));
            SeedCcaClasses(db);
            var coA = SeedCanadianCompany(db, id: 101);
            var coB = SeedCanadianCompany(db, id: 102);
            AddAsset(db, coA.Id, "A-1", "CNC MACHINE", 100_000m, new DateTime(2024, 1, 1));
            AddAsset(db, coB.Id, "B-1", "CNC MACHINE", 250_000m, new DateTime(2024, 1, 1));

            // Company A
            var tenantOverrideA = new TenantContextOverride();
            var tenantContextA = new TenantContext(tenantOverrideA);
            tenantContextA.SetContext(1, coA.Id, null);
            var ccaServiceA = new CcaService(db, tenantContextA);
            var auditA = new AuditService(db);
            var svcA = new CcaBackfillService(db, ccaServiceA, tenantContextA, tenantOverrideA, auditA, NullLogger<CcaBackfillService>.Instance);
            var reportA = await svcA.RunAsync(coA.Id, throughFiscalYear: 2024);
            Assert.Empty(reportA.Errors);

            // Company B — must NOT be blocked by Company A's existing mappings.
            var tenantOverrideB = new TenantContextOverride();
            var tenantContextB = new TenantContext(tenantOverrideB);
            tenantContextB.SetContext(1, coB.Id, null);
            var ccaServiceB = new CcaService(db, tenantContextB);
            var auditB = new AuditService(db);
            var svcB = new CcaBackfillService(db, ccaServiceB, tenantContextB, tenantOverrideB, auditB, NullLogger<CcaBackfillService>.Instance);
            var reportB = await svcB.RunAsync(coB.Id, throughFiscalYear: 2024);
            Assert.Empty(reportB.Errors);

            var class53Id = await db.CcaClasses.Where(c => c.ClassNumber == 53).Select(c => c.Id).FirstAsync();
            var balanceA = await db.CcaClassBalances.SingleAsync(b => b.CompanyId == coA.Id && b.CcaClassId == class53Id && b.FiscalYear == 2024);
            var balanceB = await db.CcaClassBalances.SingleAsync(b => b.CompanyId == coB.Id && b.CcaClassId == class53Id && b.FiscalYear == 2024);

            // Each subsidiary keeps its own UCC roll-forward — totals are NOT merged.
            Assert.Equal(100_000m, balanceA.Additions);
            Assert.Equal(50_000m, balanceA.CcaClaimed);
            Assert.Equal(250_000m, balanceB.Additions);
            Assert.Equal(125_000m, balanceB.CcaClaimed);
        }

        [Fact]
        public async Task ZeroCostAsset_IsSkippedWithWarning()
        {
            using var db = NewDb(nameof(ZeroCostAsset_IsSkippedWithWarning));
            SeedCcaClasses(db);
            var co = SeedCanadianCompany(db);
            AddAsset(db, co.Id, "A-1", "CNC MACHINE", 0m, new DateTime(2024, 1, 1));

            var svc = MakeService(db);
            var report = await svc.RunAsync(co.Id, throughFiscalYear: 2024, computeBalances: false);

            Assert.Equal(0, report.AssetsMapped);
            Assert.Equal(1, report.AssetsSkippedNoCost);
            Assert.Single(report.Warnings);
            Assert.Equal(0, await db.AssetTaxSettings.CountAsync());
        }
    }
}
