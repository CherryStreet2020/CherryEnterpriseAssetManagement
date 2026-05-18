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

// Sprint 4 Phase F Wave 1 PR #5 — StockReceipt admin service implementation.
//
// All mutations flow through IdempotencyMediator (Stripe pattern, ADR-014
// D3) so the voice-AI MCP layer and the UI dedup on the same key. Audit
// log writes are cycle-safe (DTO snapshots, no live EF entities).
public sealed class StockReceiptService : IStockReceiptService
{
    private readonly AppDbContext _db;
    private readonly ILogger<StockReceiptService> _logger;
    private readonly Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator _idempotency;

    public StockReceiptService(
        AppDbContext db,
        ILogger<StockReceiptService> logger,
        Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator idempotency)
    {
        _db = db;
        _logger = logger;
        _idempotency = idempotency;
    }

    public async Task<Result<IReadOnlyList<StockReceipt>>> ListAsync(
        StockReceiptStatus? status,
        CancellationToken ct)
    {
        var query = _db.StockReceipts
            .Include(r => r.Item)
            .Include(r => r.MaterialMaster)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var rows = await query
            .OrderByDescending(r => r.ReceivedAt)
            .ThenByDescending(r => r.Id)
            .Take(500)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<StockReceipt>>(rows);
    }

    public async Task<Result<StockReceipt>> GetAsync(int id, CancellationToken ct)
    {
        var row = await _db.StockReceipts
            .Include(r => r.Item)
            .Include(r => r.MaterialMaster)
            .Include(r => r.Location)
            .Include(r => r.ReceivedByUser)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        return row is null
            ? Result.Failure<StockReceipt>($"StockReceipt {id} not found")
            : Result.Success(row);
    }

    public async Task<Result<StockReceipt>> CreateAsync(
        CreateStockReceiptRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        var validation = ValidateCreate(request);
        if (validation is not null) return Result.Failure<StockReceipt>(validation);

        // Receipt number uniqueness — pre-insert for a clean error.
        var dup = await _db.StockReceipts.AnyAsync(r => r.ReceiptNumber == request.ReceiptNumber, ct);
        if (dup)
            return Result.Failure<StockReceipt>($"Receipt # '{request.ReceiptNumber}' already exists");

        var itemExists = await _db.Items.AnyAsync(i => i.Id == request.ItemId, ct);
        if (!itemExists)
            return Result.Failure<StockReceipt>($"Item {request.ItemId} not found");

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var entity = new StockReceipt
                {
                    ReceiptNumber = request.ReceiptNumber.Trim(),
                    ItemId = request.ItemId,
                    MaterialMasterId = request.MaterialMasterId,
                    HeatNumber = request.HeatNumber?.Trim(),
                    LotNumber = request.LotNumber?.Trim(),
                    MillCertUrl = request.MillCertUrl?.Trim(),
                    Mill = request.Mill?.Trim(),
                    SourcePoNumber = request.SourcePoNumber?.Trim(),
                    SourcePoLineId = request.SourcePoLineId?.Trim(),
                    ReceivedAt = request.ReceivedAt == default ? DateTime.UtcNow : request.ReceivedAt,
                    ReceivedByUserId = request.ReceivedByUserId,
                    LocationId = request.LocationId,
                    LengthMm = request.LengthMm,
                    WidthMm = request.WidthMm,
                    ThicknessMm = request.ThicknessMm,
                    UsableLengthMm = request.LengthMm,    // start equal to full dims
                    UsableWidthMm = request.WidthMm,
                    QuantityReceived = request.QuantityReceived,
                    QuantityRemaining = request.QuantityReceived,  // nothing consumed yet
                    Uom = request.Uom?.Trim(),
                    Status = request.Status,
                    Notes = request.Notes?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actorUserId.ToString(),
                };

                _db.StockReceipts.Add(entity);
                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(
                    entityId: entity.Id,
                    action: "Create",
                    beforeJson: null,
                    afterJson: JsonSerializer.Serialize(SnapshotForAudit(entity)),
                    actorUserId,
                    description: $"Created StockReceipt '{entity.ReceiptNumber}' (Item {entity.ItemId}, Heat {entity.HeatNumber ?? "—"})",
                    innerCt);

                return Result.Success(entity);
            },
            ct);
    }

    public async Task<Result<StockReceipt>> UpdateAsync(
        int id,
        UpdateStockReceiptRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        var validation = ValidateUpdate(request);
        if (validation is not null) return Result.Failure<StockReceipt>(validation);

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var entity = await _db.StockReceipts.FirstOrDefaultAsync(r => r.Id == id, innerCt);
                if (entity is null) return Result.Failure<StockReceipt>($"StockReceipt {id} not found");

                // ReceiptNumber uniqueness if renamed.
                if (!string.Equals(entity.ReceiptNumber, request.ReceiptNumber, StringComparison.Ordinal))
                {
                    var dup = await _db.StockReceipts.AnyAsync(
                        r => r.Id != id && r.ReceiptNumber == request.ReceiptNumber, innerCt);
                    if (dup)
                        return Result.Failure<StockReceipt>($"Receipt # '{request.ReceiptNumber}' already exists");
                }

                if (entity.ItemId != request.ItemId)
                {
                    var itemExists = await _db.Items.AnyAsync(i => i.Id == request.ItemId, innerCt);
                    if (!itemExists)
                        return Result.Failure<StockReceipt>($"Item {request.ItemId} not found");
                }

                var before = SnapshotForAudit(entity);

                entity.ReceiptNumber = request.ReceiptNumber.Trim();
                entity.ItemId = request.ItemId;
                entity.MaterialMasterId = request.MaterialMasterId;
                entity.HeatNumber = request.HeatNumber?.Trim();
                entity.LotNumber = request.LotNumber?.Trim();
                entity.MillCertUrl = request.MillCertUrl?.Trim();
                entity.Mill = request.Mill?.Trim();
                entity.SourcePoNumber = request.SourcePoNumber?.Trim();
                entity.SourcePoLineId = request.SourcePoLineId?.Trim();
                entity.ReceivedAt = request.ReceivedAt;
                entity.ReceivedByUserId = request.ReceivedByUserId;
                entity.LocationId = request.LocationId;
                entity.LengthMm = request.LengthMm;
                entity.WidthMm = request.WidthMm;
                entity.ThicknessMm = request.ThicknessMm;
                entity.UsableLengthMm = request.UsableLengthMm;
                entity.UsableWidthMm = request.UsableWidthMm;
                entity.QuantityReceived = request.QuantityReceived;
                entity.QuantityRemaining = request.QuantityRemaining;
                entity.Uom = request.Uom?.Trim();
                entity.Notes = request.Notes?.Trim();
                entity.ModifiedAt = DateTime.UtcNow;
                entity.ModifiedBy = actorUserId.ToString();

                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(
                    entityId: entity.Id,
                    action: "Update",
                    beforeJson: JsonSerializer.Serialize(before),
                    afterJson: JsonSerializer.Serialize(SnapshotForAudit(entity)),
                    actorUserId,
                    description: $"Updated StockReceipt '{entity.ReceiptNumber}'",
                    innerCt);

                return Result.Success(entity);
            },
            ct);
    }

    public async Task<Result<StockReceipt>> SetStatusAsync(
        int id,
        StockReceiptStatus newStatus,
        string? quarantineReason,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        if (newStatus == StockReceiptStatus.Quarantined && string.IsNullOrWhiteSpace(quarantineReason))
            return Result.Failure<StockReceipt>("Quarantine reason is required when status = Quarantined");

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            new { id, newStatus, quarantineReason },
            async innerCt =>
            {
                var entity = await _db.StockReceipts.FirstOrDefaultAsync(r => r.Id == id, innerCt);
                if (entity is null) return Result.Failure<StockReceipt>($"StockReceipt {id} not found");

                var prevStatus = entity.Status;
                var prevReason = entity.QuarantineReason;

                entity.Status = newStatus;
                entity.QuarantineReason = newStatus == StockReceiptStatus.Quarantined
                    ? quarantineReason?.Trim()
                    : null;
                entity.ModifiedAt = DateTime.UtcNow;
                entity.ModifiedBy = actorUserId.ToString();

                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(
                    entityId: entity.Id,
                    action: "SetStatus",
                    beforeJson: JsonSerializer.Serialize(new
                    {
                        Status = prevStatus.ToString(),
                        QuarantineReason = prevReason
                    }),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        Status = entity.Status.ToString(),
                        entity.QuarantineReason
                    }),
                    actorUserId,
                    description: $"StockReceipt '{entity.ReceiptNumber}' status {prevStatus} → {newStatus}",
                    innerCt);

                return Result.Success(entity);
            },
            ct);
    }

    // ---- helpers ----

    private static string? ValidateCreate(CreateStockReceiptRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ReceiptNumber)) return "Receipt # is required";
        if (r.ItemId <= 0) return "Item is required";
        if (r.QuantityReceived < 0) return "Quantity Received cannot be negative";
        return null;
    }

    private static string? ValidateUpdate(UpdateStockReceiptRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ReceiptNumber)) return "Receipt # is required";
        if (r.ItemId <= 0) return "Item is required";
        if (r.QuantityReceived < 0) return "Quantity Received cannot be negative";
        if (r.QuantityRemaining < 0) return "Quantity Remaining cannot be negative";
        if (r.QuantityRemaining > r.QuantityReceived)
            return "Quantity Remaining cannot exceed Quantity Received";
        return null;
    }

    private static object SnapshotForAudit(StockReceipt r) => new
    {
        r.ReceiptNumber,
        r.ItemId,
        r.MaterialMasterId,
        r.HeatNumber,
        r.LotNumber,
        r.MillCertUrl,
        r.Mill,
        r.SourcePoNumber,
        r.SourcePoLineId,
        r.ReceivedAt,
        r.ReceivedByUserId,
        r.LocationId,
        r.LengthMm,
        r.WidthMm,
        r.ThicknessMm,
        r.UsableLengthMm,
        r.UsableWidthMm,
        r.QuantityReceived,
        r.QuantityRemaining,
        r.Uom,
        Status = r.Status.ToString(),
        r.QuarantineReason,
        r.Notes,
    };

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
                EntityType = nameof(StockReceipt),
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
            _logger.LogWarning(ex, "Audit log write failed for StockReceipt {Id} {Action}", entityId, action);
        }
    }
}
