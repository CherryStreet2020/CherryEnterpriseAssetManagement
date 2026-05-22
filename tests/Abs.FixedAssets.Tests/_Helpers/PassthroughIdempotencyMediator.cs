// Test helper — passthrough IIdempotencyMediator that executes work without
// caching or deduplication. Used by service-construction sites in test
// classes that exercise the legacy PostApprovalAsync / PostReceiptAsync
// paths (those don't touch the mediator field) but still need the
// constructor to accept a non-null mediator argument.
//
// Sprint 12.9 PR #2 (PR #273) — added when IIdempotencyMediator became a
// required ctor param on ApPostingService and ReceivingPostingService.
//
// New tests that exercise the IPostingService<T>.PostAsync path should
// use a fuller fake (or Moq if it lands in the project later) so they
// can assert idempotency behaviors (cached replay, payload-drift conflict).

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Infrastructure;

namespace Abs.FixedAssets.Tests;

internal sealed class PassthroughIdempotencyMediator : IIdempotencyMediator
{
    public Task<Result<TResponse>> ExecuteAsync<TRequest, TResponse>(
        int userId,
        Guid idempotencyKey,
        TRequest request,
        Func<CancellationToken, Task<Result<TResponse>>> work,
        CancellationToken ct)
    {
        // No caching, no locking — just invoke the work and return its result.
        return work(ct);
    }
}
