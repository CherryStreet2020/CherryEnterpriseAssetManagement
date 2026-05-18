using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 — RegulatoryProfile admin service implementation.
//
// First Wave 1 PR. Sets the pattern that the other three Wave 1
// surfaces (MaterialMaster admin, Vendor edit, StockReceipts) will
// follow:
//   1. Service interface + record DTOs in IXxxService.cs
//   2. Implementation here returns Result<T>
//   3. Every mutation writes an AuditLog row
//   4. Every mutation optionally dedupes via IIdempotencyMediator
//   5. Razor page handler awaits service method, switches on Result
//
// Reference: ADR-014 §"Decisions" D2-D5.
public sealed class RegulatoryProfileService : IRegulatoryProfileService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RegulatoryProfileService> _logger;
    private readonly Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator _idempotency;

    public RegulatoryProfileService(
        AppDbContext db,
        ILogger<RegulatoryProfileService> logger,
        Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator idempotency)
    {
        _db = db;
        _logger = logger;
        _idempotency = idempotency;
    }

    public async Task<Result<IReadOnlyList<RegulatoryProfile>>> ListAsync(CancellationToken ct)
    {
        var rows = await _db.RegulatoryProfiles
            .OrderBy(p => p.Regime)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<RegulatoryProfile>>(rows);
    }

    public async Task<Result<RegulatoryProfile>> GetAsync(int id, CancellationToken ct)
    {
        var row = await _db.RegulatoryProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        return row is null
            ? Result.Failure<RegulatoryProfile>($"RegulatoryProfile {id} not found")
            : Result.Success(row);
    }

    public async Task<Result<RegulatoryProfile>> CreateAsync(
        CreateRegulatoryProfileRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<RegulatoryProfile>("Name is required");

        if (request.MinimumRetentionYears is < 0)
            return Result.Failure<RegulatoryProfile>("MinimumRetentionYears cannot be negative");

        if (!string.IsNullOrWhiteSpace(request.GatesJson) && !IsValidJson(request.GatesJson))
            return Result.Failure<RegulatoryProfile>("Gates must be valid JSON");

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var entity = new RegulatoryProfile
                {
                    Name = request.Name.Trim(),
                    Regime = request.Regime,
                    Description = request.Description?.Trim(),
                    IsExternalRegime = request.IsExternalRegime,
                    MinimumRetentionYears = request.MinimumRetentionYears,
                    Gates = string.IsNullOrWhiteSpace(request.GatesJson) ? null : request.GatesJson,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actorUserId.ToString(),
                };
                _db.RegulatoryProfiles.Add(entity);
                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(
                    entityId: entity.Id,
                    action: "Create",
                    beforeJson: null,
                    afterJson: JsonSerializer.Serialize(new
                    {
                        entity.Id,
                        entity.Name,
                        Regime = entity.Regime.ToString(),
                        entity.MinimumRetentionYears,
                        entity.IsExternalRegime,
                        entity.IsActive,
                    }),
                    actorUserId,
                    description: $"Created RegulatoryProfile '{entity.Name}' ({entity.Regime})",
                    innerCt);

                return Result.Success(entity);
            },
            ct);
    }

    public async Task<Result<RegulatoryProfile>> UpdateAsync(
        int id,
        UpdateRegulatoryProfileRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<RegulatoryProfile>("Name is required");

        if (request.MinimumRetentionYears is < 0)
            return Result.Failure<RegulatoryProfile>("MinimumRetentionYears cannot be negative");

        if (!string.IsNullOrWhiteSpace(request.GatesJson) && !IsValidJson(request.GatesJson))
            return Result.Failure<RegulatoryProfile>("Gates must be valid JSON");

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var entity = await _db.RegulatoryProfiles.FirstOrDefaultAsync(p => p.Id == id, innerCt);
                if (entity is null)
                    return Result.Failure<RegulatoryProfile>($"RegulatoryProfile {id} not found");

                var before = new
                {
                    entity.Name,
                    Regime = entity.Regime.ToString(),
                    entity.Description,
                    entity.IsExternalRegime,
                    entity.MinimumRetentionYears,
                    entity.Gates,
                };

                entity.Name = request.Name.Trim();
                entity.Regime = request.Regime;
                entity.Description = request.Description?.Trim();
                entity.IsExternalRegime = request.IsExternalRegime;
                entity.MinimumRetentionYears = request.MinimumRetentionYears;
                entity.Gates = string.IsNullOrWhiteSpace(request.GatesJson) ? null : request.GatesJson;
                entity.ModifiedAt = DateTime.UtcNow;
                entity.ModifiedBy = actorUserId.ToString();

                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(
                    entityId: entity.Id,
                    action: "Update",
                    beforeJson: JsonSerializer.Serialize(before),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        entity.Name,
                        Regime = entity.Regime.ToString(),
                        entity.Description,
                        entity.IsExternalRegime,
                        entity.MinimumRetentionYears,
                        entity.Gates,
                    }),
                    actorUserId,
                    description: $"Updated RegulatoryProfile '{entity.Name}' ({entity.Regime})",
                    innerCt);

                return Result.Success(entity);
            },
            ct);
    }

    public async Task<Result<bool>> SetActiveAsync(
        int id,
        bool isActive,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            new { id, isActive },
            async innerCt =>
            {
                var entity = await _db.RegulatoryProfiles.FirstOrDefaultAsync(p => p.Id == id, innerCt);
                if (entity is null)
                    return Result.Failure<bool>($"RegulatoryProfile {id} not found");

                if (entity.IsActive == isActive)
                    return Result.Success(true); // no-op

                var previous = entity.IsActive;
                entity.IsActive = isActive;
                entity.ModifiedAt = DateTime.UtcNow;
                entity.ModifiedBy = actorUserId.ToString();

                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(
                    entityId: entity.Id,
                    action: isActive ? "Activate" : "Deactivate",
                    beforeJson: JsonSerializer.Serialize(new { IsActive = previous }),
                    afterJson: JsonSerializer.Serialize(new { entity.IsActive }),
                    actorUserId,
                    description: $"{(isActive ? "Activated" : "Deactivated")} RegulatoryProfile '{entity.Name}'",
                    innerCt);

                return Result.Success(true);
            },
            ct);
    }

    private async Task WriteAuditAsync(
        int entityId,
        string action,
        string? beforeJson,
        string? afterJson,
        int actorUserId,
        string description,
        CancellationToken ct)
    {
        // Cycle-safe pattern (per memory: feedback_audit_log_serialization).
        // Flat anonymous object only — no EF nav graphs.
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(RegulatoryProfile),
                EntityId = entityId,
                Action = action,
                BeforeJson = beforeJson,
                AfterJson = afterJson,
                Username = actorUserId.ToString(),
                Timestamp = DateTime.UtcNow,
                Description = description,
                ActorKind = ActorKind.User, // Wave 1 surfaces are direct-user CRUD; AI mediation comes later
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit failure must NOT roll back the business mutation.
            _logger.LogWarning(ex, "Audit log write failed for RegulatoryProfile {Id} {Action}", entityId, action);
        }
    }

    private static bool IsValidJson(string s)
    {
        try { using var _ = JsonDocument.Parse(s); return true; }
        catch { return false; }
    }
}
