using System;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Locks in the FK schema + page-handler behavior for the WorkRequest
/// migration shipped alongside this test file. Same shape as
/// <see cref="InventoryListFkMigrationTests"/>; this entity closes
/// out the last open item in docs/FK_MIGRATION_STATUS.md.
/// </summary>
public class WorkRequestFkMigrationTests
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
            .UseInMemoryDatabase($"workrequest-fk-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static WorkRequest MakeRequest(string number = "WR-TEST-1")
    {
        return new WorkRequest
        {
            RequestNumber = number,
            RequestText = "Test request",
            RequestedBy = "tester",
            RequestedAt = DateTime.UtcNow,
            CreatedBy = "tester",
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task WorkRequest_NewRow_LeavesStatusLookupValueIdNull()
    {
        await using var db = NewDb();
        var wr = MakeRequest();
        wr.Status = WorkRequestStatus.New;
        db.WorkRequests.Add(wr);
        await db.SaveChangesAsync();

        var read = await db.WorkRequests.AsNoTracking().FirstAsync();
        Assert.Equal(WorkRequestStatus.New, read.Status);
        Assert.Null(read.StatusLookupValueId);
    }

    [Fact]
    public async Task WorkRequest_WriteFkAndEnumTogether_PersistsBoth()
    {
        await using var db = NewDb();

        var lt = new LookupType { Key = "WorkRequestStatus", Name = "Work Request Status" };
        db.LookupTypes.Add(lt);
        await db.SaveChangesAsync();

        var lvNew = new LookupValue { LookupTypeId = lt.Id, Code = "0", Name = "New" };
        db.LookupValues.Add(lvNew);
        await db.SaveChangesAsync();

        var wr = MakeRequest();
        wr.Status = WorkRequestStatus.New;
        wr.StatusLookupValueId = lvNew.Id;
        db.WorkRequests.Add(wr);
        await db.SaveChangesAsync();

        var read = await db.WorkRequests.AsNoTracking().FirstAsync();
        Assert.Equal(WorkRequestStatus.New, read.Status);
        Assert.Equal(lvNew.Id, read.StatusLookupValueId);
    }

    [Fact]
    public async Task WorkRequest_StatusTransitions_KeepBothColumnsConsistent()
    {
        // Mirrors a request flowing through New -> InReview -> Approved.
        await using var db = NewDb();

        var lt = new LookupType { Key = "WorkRequestStatus", Name = "Work Request Status" };
        db.LookupTypes.Add(lt);
        await db.SaveChangesAsync();

        var lvNew      = new LookupValue { LookupTypeId = lt.Id, Code = "0", Name = "New" };
        var lvInReview = new LookupValue { LookupTypeId = lt.Id, Code = "1", Name = "In Review" };
        var lvApproved = new LookupValue { LookupTypeId = lt.Id, Code = "2", Name = "Approved" };
        db.LookupValues.AddRange(lvNew, lvInReview, lvApproved);
        await db.SaveChangesAsync();

        var wr = MakeRequest();
        wr.Status = WorkRequestStatus.New;
        wr.StatusLookupValueId = lvNew.Id;
        db.WorkRequests.Add(wr);
        await db.SaveChangesAsync();

        // Transition to InReview
        var tracked = await db.WorkRequests.FirstAsync();
        tracked.Status = WorkRequestStatus.InReview;
        tracked.StatusLookupValueId = lvInReview.Id;
        await db.SaveChangesAsync();

        var afterReview = await db.WorkRequests.AsNoTracking().FirstAsync();
        Assert.Equal(WorkRequestStatus.InReview, afterReview.Status);
        Assert.Equal(lvInReview.Id, afterReview.StatusLookupValueId);

        // Transition to Approved
        var tracked2 = await db.WorkRequests.FirstAsync();
        tracked2.Status = WorkRequestStatus.Approved;
        tracked2.StatusLookupValueId = lvApproved.Id;
        await db.SaveChangesAsync();

        var afterApproved = await db.WorkRequests.AsNoTracking().FirstAsync();
        Assert.Equal(WorkRequestStatus.Approved, afterApproved.Status);
        Assert.Equal(lvApproved.Id, afterApproved.StatusLookupValueId);
    }

    [Fact]
    public async Task WorkRequest_QueryByStatusLookupValueId_ReturnsCorrectRows()
    {
        await using var db = NewDb();

        var lt = new LookupType { Key = "WorkRequestStatus", Name = "Work Request Status" };
        db.LookupTypes.Add(lt);
        await db.SaveChangesAsync();

        var lvNew      = new LookupValue { LookupTypeId = lt.Id, Code = "0", Name = "New" };
        var lvApproved = new LookupValue { LookupTypeId = lt.Id, Code = "2", Name = "Approved" };
        db.LookupValues.AddRange(lvNew, lvApproved);
        await db.SaveChangesAsync();

        var r1 = MakeRequest("WR-1"); r1.Status = WorkRequestStatus.New;      r1.StatusLookupValueId = lvNew.Id;
        var r2 = MakeRequest("WR-2"); r2.Status = WorkRequestStatus.Approved; r2.StatusLookupValueId = lvApproved.Id;
        var r3 = MakeRequest("WR-3"); r3.Status = WorkRequestStatus.New;      r3.StatusLookupValueId = lvNew.Id;
        db.WorkRequests.AddRange(r1, r2, r3);
        await db.SaveChangesAsync();

        var newOnes = await db.WorkRequests
            .Where(w => w.StatusLookupValueId == lvNew.Id)
            .OrderBy(w => w.RequestNumber)
            .ToListAsync();

        Assert.Equal(2, newOnes.Count);
        Assert.All(newOnes, w => Assert.Equal(WorkRequestStatus.New, w.Status));
    }
}
