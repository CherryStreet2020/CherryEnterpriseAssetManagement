// Sprint 12D PR #2 / ADR-022 — IChainOfCustodyService tests.
//
// Coverage:
//   - EnsureNodeAsync idempotency (same key returns same row, no duplicate)
//   - RecordEdgeAsync creates both nodes + edge in one call
//   - RecordEdgeAsync idempotency (same edge tuple is a no-op metadata-update)
//   - Cross-tenant leakage (tenant A's edge is invisible to tenant B)
//
// Pattern matches CrossTenantLeakageTests from Sprint 12.9 PR #7.
//
// NOTE: The recursive-CTE traversal in GetUpstream/DownstreamChainAsync is
// Postgres-specific (uses WITH RECURSIVE + ARRAY[] + dropping to raw
// DbConnection). Those traversal paths are covered by integration tests on
// the Postgres deployment in Sprint 12D PR #4 + #6 — out of scope for the
// EF-InMemory unit tests here.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.ChainOfCustody;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.ChainOfCustody;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

public class ChainOfCustodyServiceTests
{
    private const int TenantACompanyId = 100;
    private const int TenantBCompanyId = 200;

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
            .UseInMemoryDatabase($"chain-{dbName}-{Guid.NewGuid()}")
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

    private static ChainOfCustodyService NewService(AppDbContext db, int tenantId)
    {
        var tenant = new StubTenantContext { TenantId = tenantId };
        return new ChainOfCustodyService(db, tenant, NullLogger<ChainOfCustodyService>.Instance);
    }

    // ========================================================================
    // EnsureNodeAsync
    // ========================================================================

    [Fact]
    public async Task EnsureNode_NewKey_CreatesRow()
    {
        await using var db = NewDb();
        var svc = NewService(db, TenantACompanyId);

        var result = await svc.EnsureNodeAsync(
            new EnsureNodeRequest(ChainNodeTypes.Receipt, 42L, "RCPT-42"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChainNodeTypes.Receipt, result.Value!.NodeType);
        Assert.Equal(42L, result.Value.EntityId);
        Assert.Equal(TenantACompanyId, result.Value.TenantId);
        Assert.Equal("RCPT-42", result.Value.Label);
    }

    [Fact]
    public async Task EnsureNode_ExistingKey_ReturnsSameRow_NoDuplicates()
    {
        await using var db = NewDb();
        var svc = NewService(db, TenantACompanyId);

        var first = await svc.EnsureNodeAsync(
            new EnsureNodeRequest(ChainNodeTypes.Receipt, 42, "RCPT-42-original"),
            CancellationToken.None);
        var second = await svc.EnsureNodeAsync(
            new EnsureNodeRequest(ChainNodeTypes.Receipt, 42, "RCPT-42-updated"),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);

        // Only one row in DB
        var count = await db.ChainNodes.CountAsync();
        Assert.Equal(1, count);
        // Label updated to latest write
        Assert.Equal("RCPT-42-updated", second.Value.Label);
    }

    // ========================================================================
    // RecordEdgeAsync
    // ========================================================================

    [Fact]
    public async Task RecordEdge_NewNodes_CreatesBothNodesAndEdge()
    {
        await using var db = NewDb();
        var svc = NewService(db, TenantACompanyId);

        var result = await svc.RecordEdgeAsync(
            new RecordEdgeRequest(
                FromNodeType: ChainNodeTypes.Receipt, FromEntityId: 42, FromLabel: "RCPT-42",
                ToNodeType:   ChainNodeTypes.PurchaseOrder, ToEntityId: 555, ToLabel: "PO-555",
                EdgeType:     ChainEdgeTypes.ReceivedAt),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChainEdgeTypes.ReceivedAt, result.Value!.EdgeType);
        Assert.Equal(2, await db.ChainNodes.CountAsync());
        Assert.Equal(1, await db.ChainEdges.CountAsync());
    }

    [Fact]
    public async Task RecordEdge_DuplicateTuple_NoOpMetadataUpdate()
    {
        await using var db = NewDb();
        var svc = NewService(db, TenantACompanyId);

        await svc.RecordEdgeAsync(
            new RecordEdgeRequest(
                ChainNodeTypes.Receipt, 42, "RCPT-42",
                ChainNodeTypes.PurchaseOrder, 555, "PO-555",
                ChainEdgeTypes.ReceivedAt),
            CancellationToken.None);

        // Second identical call must NOT add another edge.
        var second = await svc.RecordEdgeAsync(
            new RecordEdgeRequest(
                ChainNodeTypes.Receipt, 42, "RCPT-42",
                ChainNodeTypes.PurchaseOrder, 555, "PO-555",
                ChainEdgeTypes.ReceivedAt),
            CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(1, await db.ChainEdges.CountAsync());
    }

    // ========================================================================
    // Cross-tenant isolation
    // ========================================================================

    [Fact]
    public async Task EnsureNode_DifferentTenants_SamePolymorphicKey_KeepsBothRows()
    {
        // Polymorphic key (NodeType, EntityId) can collide across tenants.
        // The unique index includes TenantId, so both rows coexist.
        await using var db = NewDb();
        var svcA = NewService(db, TenantACompanyId);
        var svcB = NewService(db, TenantBCompanyId);

        var a = await svcA.EnsureNodeAsync(
            new EnsureNodeRequest(ChainNodeTypes.Receipt, 42, "RCPT-42 (A)"),
            CancellationToken.None);
        var b = await svcB.EnsureNodeAsync(
            new EnsureNodeRequest(ChainNodeTypes.Receipt, 42, "RCPT-42 (B)"),
            CancellationToken.None);

        Assert.True(a.IsSuccess);
        Assert.True(b.IsSuccess);
        Assert.NotEqual(a.Value!.Id, b.Value!.Id);
        Assert.Equal(TenantACompanyId, a.Value.TenantId);
        Assert.Equal(TenantBCompanyId, b.Value.TenantId);
        Assert.Equal(2, await db.ChainNodes.CountAsync());
    }

    [Fact]
    public async Task RecordEdge_TenantBCannotSeeTenantAEdges()
    {
        // Tenant A records an edge between two nodes.
        // Tenant B tries to record the "same" edge (same polymorphic keys,
        // same EdgeType) — at the EF-InMemory level the tenant column is
        // not RLS-enforced, but the SERVICE LAYER scopes everything to
        // _tenantContext.TenantId, so Tenant B gets a fresh node pair +
        // fresh edge (in its own tenant scope). Tenant A's data is untouched.
        await using var db = NewDb();
        var svcA = NewService(db, TenantACompanyId);
        var svcB = NewService(db, TenantBCompanyId);

        await svcA.RecordEdgeAsync(
            new RecordEdgeRequest(
                ChainNodeTypes.Receipt, 42, "RCPT-42",
                ChainNodeTypes.PurchaseOrder, 555, "PO-555",
                ChainEdgeTypes.ReceivedAt),
            CancellationToken.None);

        // Tenant B's identical call lands in tenant B scope, not tenant A's.
        await svcB.RecordEdgeAsync(
            new RecordEdgeRequest(
                ChainNodeTypes.Receipt, 42, "RCPT-42",
                ChainNodeTypes.PurchaseOrder, 555, "PO-555",
                ChainEdgeTypes.ReceivedAt),
            CancellationToken.None);

        // 4 nodes total (2 per tenant), 2 edges total (1 per tenant)
        Assert.Equal(4, await db.ChainNodes.CountAsync());
        Assert.Equal(2, await db.ChainEdges.CountAsync());

        var aNodes = await db.ChainNodes.Where(n => n.TenantId == TenantACompanyId).CountAsync();
        var bNodes = await db.ChainNodes.Where(n => n.TenantId == TenantBCompanyId).CountAsync();
        Assert.Equal(2, aNodes);
        Assert.Equal(2, bNodes);
    }
}
