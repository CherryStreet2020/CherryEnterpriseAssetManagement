using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 — RegulatoryProfile admin service.
//
// Per ADR-014 D2 (Result<T> pattern), every method returns Result<T>
// so the Razor page handler AND the future voice-AI MCP tool call
// the same surface and consume the same shape.
//
// Per ADR-014 D4 (Stripe idempotency), every mutation accepts a
// nullable IdempotencyKey. Empty/null = no idempotency. The mediator
// dedupes by (userId, key, request-hash) so the voice layer can retry
// safely.
//
// Per ADR-014 D5 (AuditLog AI columns), every mutation writes an
// AuditLog row including ActorKind / OnBehalfOfUserId / AiSessionId
// when the caller is the voice layer.
public interface IRegulatoryProfileService
{
    Task<Result<IReadOnlyList<RegulatoryProfile>>> ListAsync(CancellationToken ct);

    Task<Result<RegulatoryProfile>> GetAsync(int id, CancellationToken ct);

    Task<Result<RegulatoryProfile>> CreateAsync(
        CreateRegulatoryProfileRequest request,
        int actorUserId,
        System.Guid? idempotencyKey,
        CancellationToken ct);

    Task<Result<RegulatoryProfile>> UpdateAsync(
        int id,
        UpdateRegulatoryProfileRequest request,
        int actorUserId,
        System.Guid? idempotencyKey,
        CancellationToken ct);

    Task<Result<bool>> SetActiveAsync(
        int id,
        bool isActive,
        int actorUserId,
        System.Guid? idempotencyKey,
        CancellationToken ct);
}

public sealed record CreateRegulatoryProfileRequest(
    string Name,
    RegulatoryRegime Regime,
    string? Description,
    bool IsExternalRegime,
    int? MinimumRetentionYears,
    string? GatesJson);

public sealed record UpdateRegulatoryProfileRequest(
    string Name,
    RegulatoryRegime Regime,
    string? Description,
    bool IsExternalRegime,
    int? MinimumRetentionYears,
    string? GatesJson);
