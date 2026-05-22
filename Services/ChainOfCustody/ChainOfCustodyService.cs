// Sprint 12D PR #2 / ADR-022 — chain-of-custody graph service.
//
// Postgres-native implementation backed by two recursive CTEs (upstream +
// downstream). Q3 2026 swaps this implementation for an Apache-AGE-backed
// one behind the same IChainOfCustodyService interface.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.ChainOfCustody;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.ChainOfCustody;

public sealed class ChainOfCustodyService : IChainOfCustodyService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ChainOfCustodyService> _logger;

    public ChainOfCustodyService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<ChainOfCustodyService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ========================================================================
    // Write path
    // ========================================================================

    public async Task<Result<ChainNode>> EnsureNodeAsync(
        EnsureNodeRequest request,
        CancellationToken ct = default)
    {
        if (request is null) return Result.Failure<ChainNode>("request is required");
        if (string.IsNullOrWhiteSpace(request.NodeType)) return Result.Failure<ChainNode>("NodeType is required");
        if (string.IsNullOrWhiteSpace(request.Label)) return Result.Failure<ChainNode>("Label is required");

        var tenantId = ResolveTenantId();

        // Local-tracker check first (Sprint 12C PR #282 lesson — within-batch
        // dedup avoids UNIQUE constraint violation on the unique index).
        // Apply label/metadata updates here too — last write wins, mirrors
        // the existing-row path below.
        var local = _db.ChainNodes.Local.FirstOrDefault(n =>
            n.NodeType == request.NodeType &&
            n.EntityId == request.EntityId &&
            n.TenantId == tenantId);
        if (local is not null)
        {
            local.Label = request.Label;
            if (request.Metadata is not null)
                local.Metadata = request.Metadata;
            return Result.Success(local);
        }

        var existing = await _db.ChainNodes
            .FirstOrDefaultAsync(n =>
                n.NodeType == request.NodeType &&
                n.EntityId == request.EntityId &&
                n.TenantId == tenantId, ct);
        if (existing is not null)
        {
            // Update label/metadata if the caller has a fresher payload.
            existing.Label = request.Label;
            if (request.Metadata is not null)
                existing.Metadata = request.Metadata;
            return Result.Success(existing);
        }

        var node = new ChainNode
        {
            NodeType = request.NodeType,
            EntityId = request.EntityId,
            TenantId = tenantId,
            Label = request.Label,
            Metadata = request.Metadata,
        };
        _db.ChainNodes.Add(node);
        await _db.SaveChangesAsync(ct);
        return Result.Success(node);
    }

    public async Task<Result<ChainEdge>> RecordEdgeAsync(
        RecordEdgeRequest request,
        CancellationToken ct = default)
    {
        if (request is null) return Result.Failure<ChainEdge>("request is required");
        if (string.IsNullOrWhiteSpace(request.EdgeType)) return Result.Failure<ChainEdge>("EdgeType is required");

        var tenantId = ResolveTenantId();

        var fromNode = await EnsureNodeAsync(
            new EnsureNodeRequest(request.FromNodeType, request.FromEntityId, request.FromLabel),
            ct);
        if (fromNode.IsFailure) return Result.Failure<ChainEdge>($"from-node ensure failed: {fromNode.Error}");

        var toNode = await EnsureNodeAsync(
            new EnsureNodeRequest(request.ToNodeType, request.ToEntityId, request.ToLabel),
            ct);
        if (toNode.IsFailure) return Result.Failure<ChainEdge>($"to-node ensure failed: {toNode.Error}");

        // Dedup by (FromNodeId, ToNodeId, EdgeType). Both nodes are now in
        // the tracker (Added) or in DB (Unchanged). If their IDs are zero
        // (Add path), the SaveChanges below assigns real IDs; we use the
        // current values for the dedup lookup which will be 0 for Adds.
        // Safe because two new edges in the same batch with the same
        // (From, To, EdgeType) cannot collide on a zero-id key — they get
        // separate Ids on save.
        if (fromNode.Value!.Id > 0 && toNode.Value!.Id > 0)
        {
            var existing = await _db.ChainEdges.FirstOrDefaultAsync(e =>
                e.FromNodeId == fromNode.Value.Id &&
                e.ToNodeId == toNode.Value.Id &&
                e.EdgeType == request.EdgeType &&
                e.TenantId == tenantId, ct);
            if (existing is not null)
            {
                if (request.Metadata is not null)
                    existing.Metadata = request.Metadata;
                return Result.Success(existing);
            }
        }

        var edge = new ChainEdge
        {
            FromNode = fromNode.Value!,
            ToNode = toNode.Value!,
            EdgeType = request.EdgeType,
            TenantId = tenantId,
            Metadata = request.Metadata,
        };
        _db.ChainEdges.Add(edge);
        await _db.SaveChangesAsync(ct);
        return Result.Success(edge);
    }

    // ========================================================================
    // Read path — recursive-CTE traversal
    // ========================================================================

    public Task<Result<ChainOfCustodyGraph>> GetUpstreamChainAsync(
        string nodeType, long entityId, int maxDepth = 6, CancellationToken ct = default) =>
        TraverseAsync(nodeType, entityId, maxDepth, upstream: true, ct);

    public Task<Result<ChainOfCustodyGraph>> GetDownstreamChainAsync(
        string nodeType, long entityId, int maxDepth = 6, CancellationToken ct = default) =>
        TraverseAsync(nodeType, entityId, maxDepth, upstream: false, ct);

    private async Task<Result<ChainOfCustodyGraph>> TraverseAsync(
        string nodeType, long entityId, int maxDepth, bool upstream, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nodeType)) return Result.Failure<ChainOfCustodyGraph>("nodeType is required");
        if (maxDepth < 1 || maxDepth > 12) return Result.Failure<ChainOfCustodyGraph>("maxDepth must be 1..12");

        var tenantId = ResolveTenantId();

        var startNode = await _db.ChainNodes.AsNoTracking()
            .Where(n => n.NodeType == nodeType && n.EntityId == entityId && n.TenantId == tenantId)
            .Select(n => new { n.Id, n.NodeType, n.EntityId, n.Label, n.Metadata })
            .FirstOrDefaultAsync(ct);
        if (startNode is null) return Result.Failure<ChainOfCustodyGraph>("start node not found in tenant scope");

        // EF Core can't model recursive CTEs natively — drop to raw SQL.
        // Direction switch:
        //   upstream  → JOIN ChainEdges e ON e.ToNodeId   = chain.Id, next = e.FromNodeId
        //   downstream→ JOIN ChainEdges e ON e.FromNodeId = chain.Id, next = e.ToNodeId
        var (joinOn, nextSide) = upstream
            ? ("e.\"ToNodeId\"   = chain.\"Id\"", "e.\"FromNodeId\"")
            : ("e.\"FromNodeId\" = chain.\"Id\"", "e.\"ToNodeId\"");

        var sql = $@"
            WITH RECURSIVE chain AS (
                SELECT
                    n.""Id""        AS ""Id"",
                    n.""NodeType""  AS ""NodeType"",
                    n.""EntityId""  AS ""EntityId"",
                    n.""Label""     AS ""Label"",
                    n.""Metadata""  AS ""NodeMetadata"",
                    0               AS ""Depth"",
                    CAST(NULL AS BIGINT)  AS ""IncomingEdgeId"",
                    CAST(NULL AS VARCHAR) AS ""IncomingEdgeType"",
                    CAST(NULL AS JSONB)   AS ""EdgeMetadata"",
                    ARRAY[n.""Id""]       AS path
                FROM ""ChainNodes"" n
                WHERE n.""Id"" = {{0}}
                UNION ALL
                SELECT
                    nn.""Id"",
                    nn.""NodeType"",
                    nn.""EntityId"",
                    nn.""Label"",
                    nn.""Metadata"",
                    chain.""Depth"" + 1,
                    e.""Id""        AS ""IncomingEdgeId"",
                    e.""EdgeType""  AS ""IncomingEdgeType"",
                    e.""Metadata""  AS ""EdgeMetadata"",
                    chain.path || nn.""Id""
                FROM chain
                JOIN ""ChainEdges"" e ON {joinOn}
                JOIN ""ChainNodes"" nn ON nn.""Id"" = {nextSide}
                WHERE nn.""Id"" <> ALL(chain.path)
                  AND chain.""Depth"" < {{1}}
            )
            SELECT ""Id"", ""NodeType"", ""EntityId"", ""Label"", ""Depth"",
                   ""IncomingEdgeId"", ""IncomingEdgeType"",
                   ""NodeMetadata"", ""EdgeMetadata""
            FROM chain
            ORDER BY ""Depth"", ""Id"";
        ";

        // Raw SQL → DTO projection. EF Core can run FromSqlRaw against a
        // DbSet that maps to the result columns, but our shape is anonymous
        // (joins across two tables), so use the Npgsql connection directly
        // via _db.Database.GetDbConnection().
        var hops = new List<ChainHop>();
        var conn = _db.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql.Replace("{0}", "@p0").Replace("{1}", "@p1");
            var p0 = cmd.CreateParameter(); p0.ParameterName = "@p0"; p0.Value = startNode.Id; cmd.Parameters.Add(p0);
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@p1"; p1.Value = maxDepth; cmd.Parameters.Add(p1);

            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                hops.Add(new ChainHop(
                    NodeId:           rdr.GetInt64(0),
                    NodeType:         rdr.GetString(1),
                    EntityId:         rdr.GetInt64(2),
                    Label:            rdr.GetString(3),
                    Depth:            rdr.GetInt32(4),
                    IncomingEdgeId:   rdr.IsDBNull(5) ? null : rdr.GetInt64(5),
                    IncomingEdgeType: rdr.IsDBNull(6) ? null : rdr.GetString(6),
                    NodeMetadata:     rdr.IsDBNull(7) ? null : JsonDocument.Parse(rdr.GetString(7)),
                    EdgeMetadata:     rdr.IsDBNull(8) ? null : JsonDocument.Parse(rdr.GetString(8))));
            }
        }
        finally
        {
            if (wasClosed) await conn.CloseAsync();
        }

        return Result.Success(new ChainOfCustodyGraph(startNode.Id, hops));
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    /// <summary>
    /// Resolves the effective TenantId for the current request. Per ADR-022
    /// §D6 + the Embeddings precedent, system/global rows use TenantId=0.
    /// </summary>
    private int ResolveTenantId() => _tenantContext.TenantId ?? 0;
}
