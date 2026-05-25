using System;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services.Controller;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Controller;

/// <summary>
/// Sprint 12.7 PR #2 — ChainTraceService unit tests.
///
/// Covers the four paths the service supports today:
///   1. ParseEntityRef — accepts loose query grammars.
///   2. Asset arm     — walks Asset → CipCapitalization → CipProject → CipCosts
///                      + recent Depreciation JEs (matched by Source +
///                      account-on-line filter) → lines with chips.
///   3. JE arm        — walks JE header → reverse-resolves CIP origin when
///                      Source="CIP" → JournalLines with segment chips.
///   4. Empty / unparseable / not-found paths return IsResolved=false with
///      a helpful narration.
///
/// Uses the same EF Core InMemory pattern as JournalGeneratorAccountingKeyTests.
/// </summary>
public class ChainTraceServiceTests
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
            .UseInMemoryDatabase($"chain-trace-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    // ==========================================================================
    // Parser
    // ==========================================================================

    // Note: EntityKind/EntityRef are internal (see ChainTraceService.cs).
    // We compare via .ToString() / .Id rather than typed enum values to avoid
    // leaking internal types through xunit Theory parameters (which would
    // otherwise require [InlineData] with internal enum constants and
    // tripping CS0051 "inconsistent accessibility").
    [Theory]
    [InlineData("ASSET-1234", "Asset",         1234)]
    [InlineData("asset:5678", "Asset",         5678)]
    [InlineData("Asset 99",   "Asset",         99)]
    [InlineData("1234",       "Asset",         1234)]
    [InlineData("JE-7",       "JournalEntry",  7)]
    [InlineData("journal:12", "JournalEntry",  12)]
    [InlineData("PO-9000",    "PurchaseOrder", 9000)]
    [InlineData("INV-42",     "Invoice",       42)]
    [InlineData("WO-100",     "WorkOrder",     100)]
    public void ParseEntityRef_recognizes_common_forms(string input, string expectedKindName, int expectedId)
    {
        var parsed = ChainTraceService.ParseEntityRef(input);
        Assert.NotNull(parsed);
        Assert.Equal(expectedKindName, parsed!.Kind.ToString());
        Assert.Equal(expectedId, parsed.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-thing")]
    [InlineData("ASSET-")]
    [InlineData("ASSET--1")]   // negative id
    [InlineData("ASSET-0")]    // zero id
    public void ParseEntityRef_rejects_unparseable_strings(string input)
    {
        var parsed = ChainTraceService.ParseEntityRef(input);
        Assert.Null(parsed);
    }

    // ==========================================================================
    // Empty / unresolved query
    // ==========================================================================

    [Fact]
    public async Task TraceAsync_with_empty_query_returns_help_state()
    {
        using var db = NewDb();
        var svc = new ChainTraceService(db, NullLogger<ChainTraceService>.Instance);

        var r = await svc.TraceAsync("");

        Assert.False(r.IsResolved);
        Assert.Contains("No query", r.Headline);
        Assert.NotEmpty(r.Narration!);
        Assert.Empty(r.Steps);
    }

    [Fact]
    public async Task TraceAsync_with_unparseable_query_returns_help_state()
    {
        using var db = NewDb();
        var svc = new ChainTraceService(db, NullLogger<ChainTraceService>.Instance);

        var r = await svc.TraceAsync("garbage-input-no-id");

        Assert.False(r.IsResolved);
        Assert.Contains("Could not parse", r.Headline);
        Assert.Empty(r.Steps);
    }

    [Fact]
    public async Task TraceAsync_with_missing_asset_returns_not_found()
    {
        using var db = NewDb();
        var svc = new ChainTraceService(db, NullLogger<ChainTraceService>.Instance);

        var r = await svc.TraceAsync("ASSET-999999");

        Assert.False(r.IsResolved);
        Assert.Contains("not found", r.Headline);
        Assert.Empty(r.Steps);
    }

    // ==========================================================================
    // Asset arm
    // ==========================================================================

    [Fact]
    public async Task TraceAsync_asset_arm_walks_capitalization_and_depreciation_chain()
    {
        using var db = NewDb();

        // Arrange — an asset with one CIP capitalization (origin) and two
        // depreciation JEs (one per period).
        var asset = new Asset
        {
            AssetNumber = "ABS-0042",
            Description = "Mazak Integrex i-300ST",
            Model = "i-300ST",
            AcquisitionCost = 1_800_000m,
            AccumulatedDepreciation = 556_500m,
            InServiceDate = new DateTime(2024, 3, 15),
            GLAssetAccount = "1500",
            GLAccumDepAccount = "1510",
            GLDepExpenseAccount = "6500",
            CompanyId = 2,
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var cipProject = new CipProject
        {
            ProjectNumber = "CIP-2024-007",
            Name = "5-axis Cell Stand-up",
            StartDate = new DateTime(2023, 11, 1),
            BudgetAmount = 1_800_000m,
            TotalCosts = 1_800_000m,
        };
        db.CipProjects.Add(cipProject);
        await db.SaveChangesAsync();

        var cipCost = new CipCost
        {
            CipProjectId = cipProject.Id,
            Description = "Mazak machine purchase",
            Amount = 1_500_000m,
            TransactionDate = new DateTime(2024, 1, 20),
            Vendor = "Mazak USA",
            InvoiceNumber = "MZ-2024-0987",
            SourceType = "VendorInvoice",
            SourceDisplayRef = "INV-2024-0987",
        };
        db.CipCosts.Add(cipCost);
        await db.SaveChangesAsync();

        var capJe = new JournalEntry
        {
            Batch = "CIP-CAP-ABS-0042",
            Source = "CIP",
            PostingDate = new DateTime(2024, 3, 15),
            Period = 202403,
            Description = "Capitalization of Mazak Integrex",
        };
        db.JournalEntries.Add(capJe);
        await db.SaveChangesAsync();

        db.CipCapitalizations.Add(new CipCapitalization
        {
            AssetId = asset.Id,
            CipProjectId = cipProject.Id,
            JournalEntryId = capJe.Id,
            CapitalizedAt = new DateTime(2024, 3, 15),
            TotalCapitalized = 1_800_000m,
        });
        await db.SaveChangesAsync();

        // Two depreciation JEs — one Apr 2026, one May 2026. Each carries
        // one Debit on DepExpense + one Credit on AccumDep, both on the
        // asset's GL account strings.
        var depJeApr = new JournalEntry
        {
            Batch = "DEP-202604",
            Source = "Depreciation",
            PostingDate = new DateTime(2026, 4, 30),
            Period = 202604,
            Description = "Monthly depreciation Apr 2026",
        };
        var depJeMay = new JournalEntry
        {
            Batch = "DEP-202605",
            Source = "Depreciation",
            PostingDate = new DateTime(2026, 5, 31),
            Period = 202605,
            Description = "Monthly depreciation May 2026",
        };
        db.JournalEntries.AddRange(depJeApr, depJeMay);
        await db.SaveChangesAsync();

        db.JournalLines.AddRange(
            new JournalLine { JournalEntryId = depJeApr.Id, LineNo = 1, Account = "6500", Debit = 14_167m, Description = "Apr dep — ABS-0042" },
            new JournalLine { JournalEntryId = depJeApr.Id, LineNo = 2, Account = "1510", Credit = 14_167m, Description = "Apr dep — ABS-0042" },
            new JournalLine { JournalEntryId = depJeMay.Id, LineNo = 1, Account = "6500", Debit = 14_167m, Description = "May dep — ABS-0042" },
            new JournalLine { JournalEntryId = depJeMay.Id, LineNo = 2, Account = "1510", Credit = 14_167m, Description = "May dep — ABS-0042" }
        );
        await db.SaveChangesAsync();

        var svc = new ChainTraceService(db, NullLogger<ChainTraceService>.Instance);

        // Act
        var r = await svc.TraceAsync($"ASSET-{asset.Id}");

        // Assert — chain shape
        Assert.True(r.IsResolved);
        Assert.Contains("Mazak Integrex", r.Headline);
        Assert.Contains("ABS-0042", r.Headline);

        // Step ordering:
        //   1× Asset
        //   1× CipProject (capitalization)
        //   1× CipCost (top cost from the project)
        //   2× JournalEntry (depreciation JEs)
        //   2× JournalLine per JE = 4 total
        Assert.Equal("Asset",         r.Steps[0].StepType);
        Assert.Equal("CipProject",    r.Steps[1].StepType);
        Assert.Equal("CipCost",       r.Steps[2].StepType);

        var jeSteps = r.Steps.Where(s => s.StepType == "JournalEntry").ToList();
        Assert.Equal(2, jeSteps.Count);

        var lineSteps = r.Steps.Where(s => s.StepType == "JournalLine").ToList();
        Assert.Equal(4, lineSteps.Count);

        // Asset header carries NBV math
        Assert.Contains("NBV", r.Steps[0].AmountText);
        Assert.Contains("acquisition cost", r.Steps[0].Narration);

        // CIP project step renders project number + amount
        Assert.Contains("CIP-2024-007", r.Steps[1].Headline);

        // CIP cost step picks up vendor + invoice ref
        Assert.Contains("Mazak USA", r.Steps[2].Subtext);

        // Depreciation JEs come back DESC (May before Apr)
        Assert.Contains("202605", jeSteps[0].DateText);

        // Each journal line carries a segment chip (at minimum, the GL account
        // fallback when AccountingKeyId is NULL).
        Assert.All(lineSteps, line => Assert.NotNull(line.SegmentChips));
        Assert.All(lineSteps, line => Assert.NotEmpty(line.SegmentChips!));
    }

    // ==========================================================================
    // JE arm
    // ==========================================================================

    [Fact]
    public async Task TraceAsync_je_arm_walks_lines_and_reverse_resolves_cip_origin()
    {
        using var db = NewDb();

        var asset = new Asset
        {
            AssetNumber = "ABS-0099",
            Description = "Parpas 5-axis Bridge Mill",
            AcquisitionCost = 800_000m,
            AccumulatedDepreciation = 0m,
            InServiceDate = new DateTime(2025, 6, 1),
            CompanyId = 2,
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var cip = new CipProject
        {
            ProjectNumber = "CIP-2025-014",
            Name = "Parpas Install",
            StartDate = new DateTime(2025, 1, 15),
            BudgetAmount = 800_000m,
            TotalCosts = 800_000m,
        };
        db.CipProjects.Add(cip);
        await db.SaveChangesAsync();

        var je = new JournalEntry
        {
            Batch = "CIP-CAP-ABS-0099",
            Source = "CIP",
            PostingDate = new DateTime(2025, 6, 1),
            Period = 202506,
            Description = "Capitalize Parpas",
        };
        db.JournalEntries.Add(je);
        await db.SaveChangesAsync();

        db.CipCapitalizations.Add(new CipCapitalization
        {
            AssetId = asset.Id,
            CipProjectId = cip.Id,
            JournalEntryId = je.Id,
            CapitalizedAt = new DateTime(2025, 6, 1),
            TotalCapitalized = 800_000m,
        });
        db.JournalLines.AddRange(
            new JournalLine { JournalEntryId = je.Id, LineNo = 1, Account = "1500", Debit = 800_000m, Description = "Asset cost" },
            new JournalLine { JournalEntryId = je.Id, LineNo = 2, Account = "1400", Credit = 800_000m, Description = "CIP pending clear" }
        );
        await db.SaveChangesAsync();

        var svc = new ChainTraceService(db, NullLogger<ChainTraceService>.Instance);

        var r = await svc.TraceAsync($"JE-{je.Id}");

        Assert.True(r.IsResolved);
        // Header step
        Assert.Equal("JournalEntry", r.Steps[0].StepType);
        Assert.Contains("CIP", r.Steps[0].Eyebrow);
        // Reverse-walked CIP origin
        Assert.Contains("Asset",      r.Steps.Select(s => s.StepType));
        Assert.Contains("CipProject", r.Steps.Select(s => s.StepType));
        // Both JE lines surface
        Assert.Equal(2, r.Steps.Count(s => s.StepType == "JournalLine"));
    }

    [Fact]
    public async Task TraceAsync_je_arm_with_missing_id_returns_not_found()
    {
        using var db = NewDb();
        var svc = new ChainTraceService(db, NullLogger<ChainTraceService>.Instance);

        var r = await svc.TraceAsync("JE-99999");

        Assert.False(r.IsResolved);
        Assert.Contains("not found", r.Headline);
    }
}
