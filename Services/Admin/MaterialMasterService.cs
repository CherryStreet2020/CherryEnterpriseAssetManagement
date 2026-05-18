using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 PR #2 — MaterialMaster admin service.
// Same Result<T> + audit + idempotency pattern as PR #216.
public sealed class MaterialMasterService : IMaterialMasterService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MaterialMasterService> _logger;
    private readonly Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator _idempotency;

    public MaterialMasterService(
        AppDbContext db,
        ILogger<MaterialMasterService> logger,
        Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator idempotency)
    {
        _db = db;
        _logger = logger;
        _idempotency = idempotency;
    }

    public async Task<Result<IReadOnlyList<MaterialMaster>>> ListAsync(CancellationToken ct)
    {
        var rows = await _db.MaterialMasters
            .OrderBy(m => m.Form)
            .ThenBy(m => m.ShopCode)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<MaterialMaster>>(rows);
    }

    public async Task<Result<MaterialMaster>> GetAsync(int id, CancellationToken ct)
    {
        var row = await _db.MaterialMasters.FirstOrDefaultAsync(m => m.Id == id, ct);
        return row is null
            ? Result.Failure<MaterialMaster>($"MaterialMaster {id} not found")
            : Result.Success(row);
    }

    public async Task<Result<MaterialMaster>> CreateAsync(
        CreateMaterialMasterRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ShopCode))
            return Result.Failure<MaterialMaster>("ShopCode is required");

        if (request.DensityKgPerM3 is < 0)
            return Result.Failure<MaterialMaster>("DensityKgPerM3 cannot be negative");

        // Enforce ShopCode uniqueness pre-insert for a clean error
        var dup = await _db.MaterialMasters.AnyAsync(m => m.ShopCode == request.ShopCode, ct);
        if (dup)
            return Result.Failure<MaterialMaster>($"ShopCode '{request.ShopCode}' already exists");

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var entity = new MaterialMaster
                {
                    ShopCode = request.ShopCode.Trim(),
                    AstmDesignation = request.AstmDesignation?.Trim(),
                    Description = request.Description?.Trim(),
                    Form = request.Form,
                    DensityKgPerM3 = request.DensityKgPerM3,
                    IsAnisotropic = request.IsAnisotropic,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actorUserId.ToString(),
                };
                _db.MaterialMasters.Add(entity);
                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(
                    entityId: entity.Id,
                    action: "Create",
                    beforeJson: null,
                    afterJson: JsonSerializer.Serialize(new
                    {
                        entity.Id,
                        entity.ShopCode,
                        entity.AstmDesignation,
                        Form = entity.Form.ToString(),
                        entity.DensityKgPerM3,
                        entity.IsAnisotropic,
                    }),
                    actorUserId,
                    description: $"Created MaterialMaster '{entity.ShopCode}' ({entity.Form})",
                    innerCt);

                return Result.Success(entity);
            },
            ct);
    }

    public async Task<Result<MaterialMaster>> UpdateAsync(
        int id,
        UpdateMaterialMasterRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ShopCode))
            return Result.Failure<MaterialMaster>("ShopCode is required");

        if (request.DensityKgPerM3 is < 0)
            return Result.Failure<MaterialMaster>("DensityKgPerM3 cannot be negative");

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var entity = await _db.MaterialMasters.FirstOrDefaultAsync(m => m.Id == id, innerCt);
                if (entity is null)
                    return Result.Failure<MaterialMaster>($"MaterialMaster {id} not found");

                // ShopCode uniqueness if renamed
                if (!string.Equals(entity.ShopCode, request.ShopCode, StringComparison.Ordinal))
                {
                    var dup = await _db.MaterialMasters.AnyAsync(
                        m => m.Id != id && m.ShopCode == request.ShopCode, innerCt);
                    if (dup)
                        return Result.Failure<MaterialMaster>($"ShopCode '{request.ShopCode}' already exists");
                }

                var before = new
                {
                    entity.ShopCode,
                    entity.AstmDesignation,
                    entity.Description,
                    Form = entity.Form.ToString(),
                    entity.DensityKgPerM3,
                    entity.IsAnisotropic,
                };

                entity.ShopCode = request.ShopCode.Trim();
                entity.AstmDesignation = request.AstmDesignation?.Trim();
                entity.Description = request.Description?.Trim();
                entity.Form = request.Form;
                entity.DensityKgPerM3 = request.DensityKgPerM3;
                entity.IsAnisotropic = request.IsAnisotropic;
                entity.ModifiedAt = DateTime.UtcNow;
                entity.ModifiedBy = actorUserId.ToString();

                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(
                    entityId: entity.Id,
                    action: "Update",
                    beforeJson: JsonSerializer.Serialize(before),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        entity.ShopCode,
                        entity.AstmDesignation,
                        entity.Description,
                        Form = entity.Form.ToString(),
                        entity.DensityKgPerM3,
                        entity.IsAnisotropic,
                    }),
                    actorUserId,
                    description: $"Updated MaterialMaster '{entity.ShopCode}' ({entity.Form})",
                    innerCt);

                return Result.Success(entity);
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
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(MaterialMaster),
                EntityId = entityId,
                Action = action,
                BeforeJson = beforeJson,
                AfterJson = afterJson,
                Username = actorUserId.ToString(),
                Timestamp = DateTime.UtcNow,
                Description = description,
                ActorKind = ActorKind.User,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log write failed for MaterialMaster {Id} {Action}", entityId, action);
        }
    }
}
