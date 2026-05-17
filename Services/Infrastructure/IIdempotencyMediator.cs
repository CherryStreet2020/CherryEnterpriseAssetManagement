using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Infrastructure;

// ADR-014 D4 — Idempotency mediator interface.
//
// Wrap mutating service calls to dedup retries. Same (UserId, Key)
// returns the cached response; new key executes the work.
//
// Usage:
//
//   public async Task<Result<PoResult>> PlacePoAsync(
//       PlacePoCommand cmd,
//       ClaimsPrincipal user,
//       CancellationToken ct)
//   {
//       var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
//       return await _idempotency.ExecuteAsync(
//           userId,
//           cmd.IdempotencyKey,
//           cmd,
//           async token => await ExecutePlacePoInnerAsync(cmd, user, token),
//           ct);
//   }
//
// The voice layer mints the idempotency key client-side (one UUID per
// utterance, NOT per retry). The mediator stores (UserId, Key) as
// unique and dedups against the request hash.
//
// Reference: ADR-014 §"Decisions" D4 + brandur.org/idempotency-keys.
public interface IIdempotencyMediator
{
    /// <summary>
    /// Execute work idempotently. If the (userId, idempotencyKey) row
    /// already exists with the same request hash, returns the cached
    /// response. If different hash, returns a failure. Otherwise locks
    /// the key, runs work, caches result, returns.
    /// </summary>
    /// <param name="userId">The authenticated user. Permission gate runs inside work.</param>
    /// <param name="idempotencyKey">Client-minted UUID per logical operation.</param>
    /// <param name="request">The request DTO — hashed canonically to detect payload drift.</param>
    /// <param name="work">The inner work. Runs inside the locked window.</param>
    Task<Result<TResponse>> ExecuteAsync<TRequest, TResponse>(
        int userId,
        Guid idempotencyKey,
        TRequest request,
        Func<CancellationToken, Task<Result<TResponse>>> work,
        CancellationToken ct);
}
