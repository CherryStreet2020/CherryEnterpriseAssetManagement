// Sprint 12D PR #2 / ADR-022 — chain-of-custody graph service interface.
//
// THE Q3-SWAP STABILITY POINT. This interface is the contract; the storage
// backend (Postgres recursive CTE today, Apache AGE opencypher in Q3 2026)
// is a swappable detail. PageModels, Razor partials, voice tools, and tests
// all depend on THIS surface, never on the storage.
//
// Used by:
//   - The AI "explain why" voice tool (PR #5)
//   - Receipt detail page (PR #4) — renders the upstream chain via cytoscape.js
//   - ReceivingPostingService + IWorkOrderService + IPurchasingService +
//     ApPostingService + IItemMasterService (PR #3 — they emit RecordEdgeAsync
//     calls at the right moments)

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.ChainOfCustody;

namespace Abs.FixedAssets.Services.ChainOfCustody;

public interface IChainOfCustodyService
{
    // === Read path — the AI tool + Receipt detail page consume this ===

    /// <summary>
    /// Returns the upstream chain (everything that flows INTO this node)
    /// up to <paramref name="maxDepth"/> hops, traversed via recursive CTE.
    /// Cycle-broken by path-array tracking.
    /// </summary>
    Task<Result<ChainOfCustodyGraph>> GetUpstreamChainAsync(
        string nodeType,
        long entityId,
        int maxDepth = 6,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the downstream chain (everything that flows OUT OF this node).
    /// </summary>
    Task<Result<ChainOfCustodyGraph>> GetDownstreamChainAsync(
        string nodeType,
        long entityId,
        int maxDepth = 6,
        CancellationToken ct = default);

    // === Write path — typed services emit edges via these methods ===

    /// <summary>
    /// Get-or-create a node for (NodeType, EntityId). Idempotent — if the
    /// node already exists in the current tenant scope, returns it instead
    /// of duplicating. Used by all five Sprint 12.9 services that emit
    /// chain edges (PR #3 wires this in).
    /// </summary>
    Task<Result<ChainNode>> EnsureNodeAsync(
        EnsureNodeRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Record a directional edge between two nodes. Idempotent on the
    /// (FromNodeId, ToNodeId, EdgeType) tuple — duplicate edge writes are
    /// no-ops (last metadata wins).
    /// </summary>
    Task<Result<ChainEdge>> RecordEdgeAsync(
        RecordEdgeRequest request,
        CancellationToken ct = default);
}

// === Request/response records ===

/// <summary>Identifies a node by its polymorphic key.</summary>
public sealed record EnsureNodeRequest(
    string NodeType,
    long EntityId,
    string Label,
    JsonDocument? Metadata = null);

/// <summary>
/// Identifies the two nodes by polymorphic key (the service resolves to
/// ChainNode.Id internally, calling EnsureNodeAsync as needed).
/// </summary>
public sealed record RecordEdgeRequest(
    string FromNodeType,
    long FromEntityId,
    string FromLabel,
    string ToNodeType,
    long ToEntityId,
    string ToLabel,
    string EdgeType,
    JsonDocument? Metadata = null);

/// <summary>
/// A traversed chain — shape feeds cytoscape.js directly. Per-hop depth
/// allows the front-end to color by distance from the start.
/// </summary>
public sealed record ChainOfCustodyGraph(
    long StartNodeId,
    IReadOnlyList<ChainHop> Hops);

public sealed record ChainHop(
    long NodeId,
    string NodeType,
    long EntityId,
    string Label,
    int Depth,
    long? IncomingEdgeId,
    string? IncomingEdgeType,
    JsonDocument? NodeMetadata,
    JsonDocument? EdgeMetadata);
