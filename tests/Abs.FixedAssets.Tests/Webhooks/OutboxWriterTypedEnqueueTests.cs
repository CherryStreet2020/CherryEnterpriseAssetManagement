using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Webhooks;

/// <summary>
/// Lock-in tests for the strongly-typed OutboxWriter.EnqueueAsync&lt;T&gt;
/// path AND the legacy untyped overload's continued behavior. Phase 1
/// of typed-outbox-payloads — see docs/design/OUTBOX_TYPED_PAYLOADS.md.
/// </summary>
public class OutboxWriterTypedEnqueueTests
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
            .UseInMemoryDatabase($"outbox-typed-{dbName}-{Guid.NewGuid()}")
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

    private static OutboxWriter NewWriter(AppDbContext db) =>
        new OutboxWriter(db, new StubTenantContext { CompanyId = 100, VisibleCompanyIds = new() { 100 } },
            NullLogger<OutboxWriter>.Instance);

    [Fact]
    public async Task EnqueueAsync_TypedEvent_PersistsCorrectShape()
    {
        await using var db = NewDb();
        var writer = NewWriter(db);

        var evt = new WorkOrderClosedV1(
            WorkOrderId: 789,
            WorkOrderNumber: "WO-001",
            Status: "Completed",
            AssetId: 42,
            ClosedAt: new DateTime(2026, 5, 7, 18, 30, 0, DateTimeKind.Utc),
            ClosedBy: "alice");

        await writer.EnqueueAsync(companyId: 100, siteId: 5, evt: evt, correlationId: "corr-789");

        var stored = await db.OutboxEvents.SingleAsync();
        Assert.Equal("workorder.closed", stored.EventType);
        Assert.Equal("MaintenanceEvent", stored.EntityType);
        Assert.Equal("789", stored.EntityId);
        Assert.Equal(100, stored.CompanyId);
        Assert.Equal(5, stored.SiteId);
        Assert.Equal("corr-789", stored.CorrelationId);
        Assert.Equal(OutboxEventStatus.Pending, stored.Status);

        // Payload JSON must contain every record property under camelCase keys.
        using var doc = JsonDocument.Parse(stored.PayloadJson);
        var root = doc.RootElement;
        Assert.Equal(789, root.GetProperty("workOrderId").GetInt32());
        Assert.Equal("WO-001", root.GetProperty("workOrderNumber").GetString());
        Assert.Equal("Completed", root.GetProperty("status").GetString());
        Assert.Equal(42, root.GetProperty("assetId").GetInt32());
        Assert.Equal("alice", root.GetProperty("closedBy").GetString());
    }

    [Fact]
    public async Task EnqueueAsync_TypedEvent_StampsPayloadVersion()
    {
        await using var db = NewDb();
        var writer = NewWriter(db);

        await writer.EnqueueAsync(companyId: 100, siteId: null,
            evt: new WorkOrderClosedV1(1, "WO-1", "Completed", null, null, null));

        var stored = await db.OutboxEvents.SingleAsync();
        Assert.Equal(1, stored.PayloadVersion);
    }

    [Fact]
    public async Task EnqueueAsync_TypedEvent_NullEvent_ThrowsArgumentNull()
    {
        await using var db = NewDb();
        var writer = NewWriter(db);
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            writer.EnqueueAsync<WorkOrderClosedV1>(companyId: 100, siteId: null, evt: null!));
    }

    [Fact]
    public async Task EnqueueAsync_TypedEvent_StaticInterfaceTypeT_SerializesConcreteShape()
    {
        // If T binds to IDomainEvent at the call site, naive
        // JsonSerializer.Serialize<T>(value) would emit only the
        // interface members. Our implementation passes evt.GetType()
        // explicitly so concrete record properties round-trip.
        await using var db = NewDb();
        var writer = NewWriter(db);

        IDomainEvent evt = new WorkOrderClosedV1(
            WorkOrderId: 5,
            WorkOrderNumber: "WO-5",
            Status: "Completed",
            AssetId: 99,
            ClosedAt: null,
            ClosedBy: "bob");

        await writer.EnqueueAsync(companyId: 100, siteId: null, evt);

        var stored = await db.OutboxEvents.SingleAsync();
        using var doc = JsonDocument.Parse(stored.PayloadJson);
        Assert.Equal(5, doc.RootElement.GetProperty("workOrderId").GetInt32());
        Assert.Equal("bob", doc.RootElement.GetProperty("closedBy").GetString());
    }

    [Fact]
#pragma warning disable CS0618 // intentionally exercising the obsolete legacy overload
    public async Task EnqueueAsync_LegacyOverload_StampsPayloadVersion1()
    {
        await using var db = NewDb();
        var writer = NewWriter(db);

        await writer.EnqueueAsync(
            companyId: 100,
            siteId: null,
            eventType: "test.event",
            entityType: "TestEntity",
            entityId: "1",
            payload: new { foo = "bar" },
            correlationId: "legacy-1");

        var stored = await db.OutboxEvents.SingleAsync();
        Assert.Equal(1, stored.PayloadVersion);
        Assert.Equal("test.event", stored.EventType);
        Assert.Contains("\"foo\":\"bar\"", stored.PayloadJson);
    }
#pragma warning restore CS0618
}
