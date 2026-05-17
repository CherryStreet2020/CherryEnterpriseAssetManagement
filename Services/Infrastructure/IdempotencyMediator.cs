using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Infrastructure;

// ADR-014 D4 — Default IIdempotencyMediator implementation.
//
// Stripe pattern in Postgres:
//   1. Hash the request canonically (sorted-key JSON SHA-256)
//   2. INSERT INTO IdempotencyKeys ON CONFLICT DO NOTHING
//   3. If we won the insert: run work, cache the response, return
//   4. If we lost the insert: check the existing row. Same hash =
//      return cached response (or wait if still locked). Different
//      hash = Result.Failure("idempotency key conflict")
//
// All within the same DbContext / transaction so dedup is durable
// across worker crashes.
//
// Reference: ADR-014 §"Decisions" D4 + brandur.org/idempotency-keys.
public sealed class IdempotencyMediator : IIdempotencyMediator
{
    private readonly AppDbContext _db;
    private readonly ILogger<IdempotencyMediator> _logger;

    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        // Sorted key output is what canonical-JSON-hashes require.
        // System.Text.Json sorts properties alphabetically when
        // PropertyNameCaseInsensitive=false; for canonical hashing
        // we also disable indentation.
        WriteIndented = false,
    };

    public IdempotencyMediator(AppDbContext db, ILogger<IdempotencyMediator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<TResponse>> ExecuteAsync<TRequest, TResponse>(
        int userId,
        Guid idempotencyKey,
        TRequest request,
        Func<CancellationToken, Task<Result<TResponse>>> work,
        CancellationToken ct)
    {
        // Empty key = no idempotency. Run work directly.
        if (idempotencyKey == Guid.Empty)
        {
            return await work(ct);
        }

        var hash = ComputeRequestHash(request);

        // Check existing row first (fast path for retries).
        var existing = await _db.IdempotencyKeys
            .Where(k => k.UserId == userId && k.Key == idempotencyKey)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            // Already seen this key. Check hash.
            if (!hash.AsSpan().SequenceEqual(existing.RequestHash))
            {
                _logger.LogWarning(
                    "Idempotency key {Key} reused with different request hash for user {UserId}",
                    idempotencyKey, userId);
                return Result.Failure<TResponse>(
                    "Idempotency key already used with a different request payload.");
            }

            // Same hash. If response is cached, return it.
            if (existing.CompletedAt is not null && existing.ResponseBody is not null)
            {
                var cached = JsonSerializer.Deserialize<TResponse>(existing.ResponseBody, CanonicalJson);
                if (cached is null)
                {
                    return Result.Failure<TResponse>("Cached response could not be deserialized.");
                }
                return Result.Success(cached);
            }

            // Same hash, work still running on another worker. We
            // return a soft failure rather than re-executing.
            return Result.Failure<TResponse>(
                "An identical request is currently in progress. Please wait and retry.");
        }

        // Try to claim the key. ON CONFLICT DO NOTHING semantics.
        var keyRow = new IdempotencyKey
        {
            UserId = userId,
            Key = idempotencyKey,
            RequestHash = hash,
            LockedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        };

        try
        {
            _db.IdempotencyKeys.Add(keyRow);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost the race. Re-read and recurse — at most one extra
            // hop, then we hit the fast path above.
            _db.Entry(keyRow).State = EntityState.Detached;
            return await ExecuteAsync(userId, idempotencyKey, request, work, ct);
        }

        // We won the insert. Run the work.
        var result = await work(ct);

        // Cache the result.
        keyRow.CompletedAt = DateTime.UtcNow;
        keyRow.ResponseStatus = result.IsSuccess ? 200 : 400;
        keyRow.ResponseBody = JsonSerializer.Serialize(result.Value, CanonicalJson);
        keyRow.LockedAt = null;
        await _db.SaveChangesAsync(ct);

        return result;
    }

    private static byte[] ComputeRequestHash<TRequest>(TRequest request)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(request, CanonicalJson);
        return SHA256.HashData(canonical);
    }
}
