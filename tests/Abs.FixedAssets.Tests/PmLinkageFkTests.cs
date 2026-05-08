using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for S1-2: explicit PM linkage FKs on MaintenanceEvent
/// replace the brittle CustomField1 = "PMTA:N" string hack.
///
/// Per the 2026-05-08 structural audit (S1-2), the hack conflated
/// PMOccurrence.Id with PMTemplateAsset.Id — different tables, different
/// namespaces — so the WO closeout's "advance the PM cycle" logic
/// silently miss-targeted rows or no-oped. Tests prove that:
///
/// - Closing a PM-generated WO advances the PMOccurrence to Closed.
/// - Closing a PM-generated WO advances PMTemplateAsset.LastCompletedDate
///   and recomputes NextDueDate based on the template's CalendarInterval.
/// - Non-PM WOs (Type != Preventative) do not touch PM state.
/// - WOs with no PM linkage no-op cleanly.
/// </summary>
public class PmLinkageFkTests
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
            .UseInMemoryDatabase($"pm-fk-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public int? TenantId { get; init; } = 1;
        public int? CompanyId { get; init; }
        public int? SiteId { get; init; }
        public int? AssignedCompanyId { get; init; }
        public int? AssignedSiteId { get; init; }
        public List<int> VisibleCompanyIds { get; init; } = new();
        public List<int> VisibleSiteIds { get; init; } = new();
        public bool IsResolved => true;
        public string? ResolutionError => null;
        public void SetContext(int? tenantId, int? companyId, int? siteId) { }
        public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds) { }
        public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds) { }
        public void SetError(string error) { }
    }

    private static async Task<(Asset asset, PMTemplate template, PMTemplateAsset assignment, PMSchedule schedule, PMOccurrence occurrence, MaintenanceEvent wo)>
        SeedPmCycleAsync(AppDbContext db, int companyId)
    {
        db.Companies.Add(new Company { Id = companyId, CompanyCode = $"C-{companyId}", Name = "Co", IsActive = true });

        var asset = new Asset
        {
            AssetNumber = "A-1",
            Description = "PM Asset",
            CompanyId = companyId,
            AcquisitionCost = 1000m,
            UsefulLifeMonths = 60,
            DepreciationMethod = DepreciationMethod.StraightLine,
            CreatedAt = DateTime.UtcNow
        };
        db.Assets.Add(asset);

        var template = new PMTemplate
        {
            Code = "PM-T1",
            Name = "Monthly inspection",
            Type = MaintenanceType.Preventative,
            Priority = PMPriority.Medium,
            CalendarInterval = RecurrenceType.Monthly,
            CalendarIntervalValue = 1,
            CompanyId = companyId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.Set<PMTemplate>().Add(template);
        await db.SaveChangesAsync();

        var assignment = new PMTemplateAsset
        {
            PMTemplateId = template.Id,
            AssetId = asset.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Set<PMTemplateAsset>().Add(assignment);

        var schedule = new PMSchedule
        {
            PMTemplateId = template.Id,
            Name = "Monthly schedule",
            CompanyId = companyId,
            CadenceType = PMCadenceType.IntervalDays,
            IntervalDays = 30,
            NextDueDateUtc = DateTime.UtcNow.AddDays(30),
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<PMSchedule>().Add(schedule);
        await db.SaveChangesAsync();

        var occurrence = new PMOccurrence
        {
            PMScheduleId = schedule.Id,
            PMTemplateId = template.Id,
            CompanyId = companyId,
            DueDateUtc = DateTime.UtcNow,
            Status = PMOccurrenceStatus.Created,
            GeneratedAt = DateTime.UtcNow
        };
        db.Set<PMOccurrence>().Add(occurrence);
        await db.SaveChangesAsync();

        var wo = new MaintenanceEvent
        {
            WorkOrderNumber = "WO-PM",
            AssetId = asset.Id,
            Type = MaintenanceType.Preventative,
            Status = MaintenanceStatus.InProgress,
            Priority = MaintenancePriority.Medium,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            PMOccurrenceId = occurrence.Id,
            PMTemplateAssetId = assignment.Id
        };
        db.MaintenanceEvents.Add(wo);
        await db.SaveChangesAsync();

        return (asset, template, assignment, schedule, occurrence, wo);
    }

    [Fact]
    public async Task CompleteEventAsync_PmGeneratedWo_AdvancesOccurrenceToClosed()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (asset, _, _, _, occurrence, wo) = await SeedPmCycleAsync(db, companyId);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var svc = new MaintenanceService(db, tenant, lookup);

        await svc.CompleteEventAsync(wo.Id, "fixed", actualCost: 0m);

        var occAfter = await db.Set<PMOccurrence>().AsNoTracking().FirstAsync(o => o.Id == occurrence.Id);
        Assert.Equal(PMOccurrenceStatus.Completed, occAfter.Status);
    }

    [Fact]
    public async Task CompleteEventAsync_PmGeneratedWo_StampsAssignmentLastCompletedAndAdvancesNextDue()
    {
        const int companyId = 100;
        await using var db = NewDb();
        var (asset, template, assignment, _, _, wo) = await SeedPmCycleAsync(db, companyId);

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var svc = new MaintenanceService(db, tenant, lookup);

        await svc.CompleteEventAsync(wo.Id, "fixed", actualCost: 0m);

        var assignAfter = await db.Set<PMTemplateAsset>().AsNoTracking().FirstAsync(a => a.Id == assignment.Id);
        Assert.NotNull(assignAfter.LastCompletedDate);
        Assert.NotNull(assignAfter.NextDueDate);

        // Monthly cadence × CalendarIntervalValue=1 → +30 days from completion.
        var expected = DateTime.UtcNow.Date.AddDays(30);
        Assert.True(Math.Abs((assignAfter.NextDueDate!.Value - expected).TotalDays) < 1.5);
    }

    [Fact]
    public async Task CompleteEventAsync_NonPreventativeWo_DoesNotAdvancePmState()
    {
        // Negative test: a Corrective WO with PM FKs (shouldn't happen
        // realistically, but tests the type guard) does not touch PM state.
        const int companyId = 100;
        await using var db = NewDb();
        var (_, _, assignment, _, occurrence, wo) = await SeedPmCycleAsync(db, companyId);

        // Flip the type to Corrective.
        wo.Type = MaintenanceType.Corrective;
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var svc = new MaintenanceService(db, tenant, lookup);

        await svc.CompleteEventAsync(wo.Id, "fixed", actualCost: 0m);

        // Occurrence stays Created; assignment LastCompletedDate stays null.
        var occAfter = await db.Set<PMOccurrence>().AsNoTracking().FirstAsync(o => o.Id == occurrence.Id);
        Assert.Equal(PMOccurrenceStatus.Created, occAfter.Status);
        var assignAfter = await db.Set<PMTemplateAsset>().AsNoTracking().FirstAsync(a => a.Id == assignment.Id);
        Assert.Null(assignAfter.LastCompletedDate);
    }

    [Fact]
    public async Task CompleteEventAsync_PmWoWithNoFks_NoOpsCleanly()
    {
        // Edge case: PM WO with neither PMOccurrenceId nor PMTemplateAssetId
        // (a "manual" PM WO created without linkage). Must not throw.
        const int companyId = 100;
        await using var db = NewDb();
        db.Companies.Add(new Company { Id = companyId, CompanyCode = "C-100", Name = "Co", IsActive = true });
        var asset = new Asset
        {
            AssetNumber = "A-1", Description = "x", CompanyId = companyId,
            AcquisitionCost = 1000m, UsefulLifeMonths = 60, CreatedAt = DateTime.UtcNow,
            DepreciationMethod = DepreciationMethod.StraightLine
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        var wo = new MaintenanceEvent
        {
            WorkOrderNumber = "WO-NOLINK",
            AssetId = asset.Id,
            Type = MaintenanceType.Preventative, // PM type, but no FKs
            Status = MaintenanceStatus.InProgress,
            Priority = MaintenancePriority.Medium,
            ScheduledDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.MaintenanceEvents.Add(wo);
        await db.SaveChangesAsync();

        var tenant = new StubTenantContext { CompanyId = companyId, VisibleCompanyIds = new() { companyId } };
        var lookup = new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);
        var svc = new MaintenanceService(db, tenant, lookup);

        var result = await svc.CompleteEventAsync(wo.Id, "fixed", actualCost: 0m);
        Assert.NotNull(result);
        Assert.Equal(MaintenanceStatus.Completed, result!.Status);
    }
}
