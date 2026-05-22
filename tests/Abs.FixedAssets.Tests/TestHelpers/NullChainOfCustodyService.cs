// Sprint 12D PR #3 — no-op IChainOfCustodyService for test contexts that
// don't exercise the chain-of-custody graph but DO instantiate services
// that take IChainOfCustodyService as a constructor dependency
// (ReceivingPostingService, ApPostingService, IWorkOrderService, etc.).
//
// Every method returns a success result with default values. Real chain
// behavior is exercised in ChainOfCustodyServiceTests + PR #6 integration.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.ChainOfCustody;
using Abs.FixedAssets.Services.ChainOfCustody;

namespace Abs.FixedAssets.Tests.TestHelpers;

public sealed class NullChainOfCustodyService : IChainOfCustodyService
{
    public Task<Result<ChainOfCustodyGraph>> GetUpstreamChainAsync(
        string nodeType, long entityId, int maxDepth = 6, CancellationToken ct = default) =>
        Task.FromResult(Result.Success(new ChainOfCustodyGraph(0, new List<ChainHop>())));

    public Task<Result<ChainOfCustodyGraph>> GetDownstreamChainAsync(
        string nodeType, long entityId, int maxDepth = 6, CancellationToken ct = default) =>
        Task.FromResult(Result.Success(new ChainOfCustodyGraph(0, new List<ChainHop>())));

    public Task<Result<ChainNode>> EnsureNodeAsync(
        EnsureNodeRequest request, CancellationToken ct = default) =>
        Task.FromResult(Result.Success(new ChainNode
        {
            Id = 1,
            NodeType = request.NodeType,
            EntityId = request.EntityId,
            Label = request.Label,
            TenantId = 0,
        }));

    public Task<Result<ChainEdge>> RecordEdgeAsync(
        RecordEdgeRequest request, CancellationToken ct = default) =>
        Task.FromResult(Result.Success(new ChainEdge
        {
            Id = 1,
            EdgeType = request.EdgeType,
            TenantId = 0,
        }));
}
