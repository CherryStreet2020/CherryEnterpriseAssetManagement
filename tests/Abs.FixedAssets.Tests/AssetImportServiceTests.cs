using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.AssetImport;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.AssetImport;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// PR #337 — happy-path tests for the bulk-asset Excel importer. Covers
/// (a) parse + validate stages a Validated batch with the expected row counts,
/// (b) commit creates Assets with stamped tenant fields + resolved manufacturer FK,
/// (c) committed batches reject a second commit.
/// </summary>
public class AssetImportServiceTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"asset-import-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static AssetImportService NewService(AppDbContext db)
    {
        var audit = new AuditService(db);
        var log = NullLogger<AssetImportService>.Instance;
        return new AssetImportService(db, audit, log);
    }

    private static byte[] BuildExcel(params (string assetNumber, string description, string? manufacturer, decimal? cost)[] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Assets");
        ws.Cell(1, 1).Value = "AssetNumber";
        ws.Cell(1, 2).Value = "Description";
        ws.Cell(1, 3).Value = "Manufacturer";
        ws.Cell(1, 4).Value = "AcquisitionCost";
        for (int i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            ws.Cell(i + 2, 1).Value = r.assetNumber;
            ws.Cell(i + 2, 2).Value = r.description;
            if (r.manufacturer is not null) ws.Cell(i + 2, 3).Value = r.manufacturer;
            if (r.cost.HasValue) ws.Cell(i + 2, 4).Value = r.cost.Value;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task ParseAndStage_HappyPath_CreatesValidatedBatchWithExpectedCounts()
    {
        using var db = NewDb();
        var svc = NewService(db);
        var bytes = BuildExcel(
            ("AT-001", "5-axis machining center", "MAZAK", 985000m),
            ("AT-002", "Horizontal lathe", "OKUMA", 425000m),
            ("", "Missing asset number", null, null)
        );
        using var stream = new MemoryStream(bytes);

        var batch = await svc.ParseAndStageAsync(
            stream, "test.xlsx", bytes.Length,
            companyId: 1, organizationId: null, siteId: null,
            userId: 1, username: "tester", ct: CancellationToken.None);

        Assert.Equal(AssetImportBatchStatus.Validated, batch.Status);
        Assert.Equal(3, batch.RowCount);
        Assert.Equal(2, batch.ValidRowCount);
        Assert.Equal(1, batch.ErrorRowCount);
    }

    [Fact]
    public async Task Commit_CreatesAssetsAndAutoCreatesManufacturer()
    {
        using var db = NewDb();
        var svc = NewService(db);
        var bytes = BuildExcel(
            ("AT-100", "Mazak 5-axis", "MAZAK CORP", 985000m),
            ("AT-101", "Mazak smaller", "MAZAK CORP", 250000m)
        );
        using var stream = new MemoryStream(bytes);

        var batch = await svc.ParseAndStageAsync(
            stream, "commit.xlsx", bytes.Length,
            companyId: 2, organizationId: null, siteId: null,
            userId: 7, username: "tester", ct: CancellationToken.None);
        Assert.Equal(2, batch.ValidRowCount);

        var committed = await svc.CommitBatchAsync(batch.Id, userId: 7, username: "tester", CancellationToken.None);

        Assert.Equal(AssetImportBatchStatus.Committed, committed.Status);
        Assert.Equal(2, await db.Assets.CountAsync());
        var asset1 = await db.Assets.FirstAsync(a => a.AssetNumber == "AT-100");
        Assert.Equal(2, asset1.CompanyId);
        Assert.NotNull(asset1.ManufacturerId);
        // Both rows reused the SAME new manufacturer.
        Assert.Equal(1, await db.Manufacturers.CountAsync());
        Assert.Equal("MAZAK CORP", (await db.Manufacturers.FirstAsync()).Name);
    }

    [Fact]
    public async Task RecommittingABatch_Throws()
    {
        using var db = NewDb();
        var svc = NewService(db);
        var bytes = BuildExcel(("AT-200", "Sample", null, 1000m));
        using var stream = new MemoryStream(bytes);

        var batch = await svc.ParseAndStageAsync(
            stream, "once.xlsx", bytes.Length,
            companyId: 1, organizationId: null, siteId: null,
            userId: 1, username: "tester", ct: CancellationToken.None);
        await svc.CommitBatchAsync(batch.Id, 1, "tester", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CommitBatchAsync(batch.Id, 1, "tester", CancellationToken.None));
    }
}
