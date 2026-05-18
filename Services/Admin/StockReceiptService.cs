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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 PR #5 + ADR-015 Migration PR #3 —
// StockReceipt admin service.
//
// PR #3 deltas:
//   - DTOs collapse 8 steel-specific fields into a single Attributes dict
//   - JsonSchema.Net validation runs server-side before persist
//   - Audit snapshot reads Attributes only (no legacy property references)
//   - Profile is sticky on Update; never overwrite ProfileId on update
public sealed class StockReceiptService : IStockReceiptService
{
    private readonly AppDbContext _db;
    private readonly ILogger<StockReceiptService> _logger;
    private readonly Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator _idempotency;
    private readonly ReceiptAttributesValidator _validator;
    private readonly IMemoryCache _cache;

    public StockReceiptService(
        AppDbContext db,
        ILogger<StockReceiptService> logger,
        Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator idempotency,
        ReceiptAttributesValidator validator,
        IMemoryCache cache)
    {
        _db = db;
        _logger = logger;
        _idempotency = idempotency;
        _validator = validator;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<StockReceipt>>> ListAsync(
        StockReceiptStatus? status,
        CancellationToken ct)
    {
        var query = _db.StockReceipts
            .Include(r => r.Item)
            .Include(r => r.MaterialMaster)
            .Include(r => r.Profile)
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
            .Include(r => r.Profile)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        return row is null
            ? Result.Failure<StockReceipt>($"StockReceipt {id} not found")
            : Result.Success(row);
    }

    public async Task<Result<ReceiptProfile>> GetDefaultProfileForCreateAsync(CancellationToken ct)
    {
        var steel = await _db.ReceiptProfiles
            .Where(p => p.Code == "STEEL" && p.IsActive)
            .FirstOrDefaultAsync(ct);
        return steel is null
            ? Result.Failure<ReceiptProfile>("Default ReceiptProfile 'STEEL' not found")
            : Result.Success(steel);
    }

    public async Task<Result<(StockReceipt entity, ReceiptProfile profile)>> GetWithProfileAsync(
        int id, CancellationToken ct)
    {
        var entity = await _db.StockReceipts
            .Include(r => r.Item)
            .Include(r => r.MaterialMaster)
            .Include(r => r.Location)
            .Include(r => r.ReceivedByUser)
            .Include(r => r.Profile)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity is null)
            return Result.Failure<(StockReceipt, ReceiptProfile)>($"StockReceipt {id} not found");

        if (entity.Profile is null)
            return Result.Failure<(StockReceipt, ReceiptProfile)>(
                $"StockReceipt {id} has no ProfileId — Migration PR #2 backfill should have set it");

        return Result.Success<(StockReceipt, ReceiptProfile)>((entity, entity.Profile));
    }

    public async Task<Result<ReceiptProfile>> GetProfileForSubmitAsync(
        int? id, string profileCode, int itemId, CancellationToken ct)
    {
        // Update: profile is sticky. Re-resolve from the existing receipt
        // regardless of what the client sent. Defense against tampering.
        if (id is int existingId && existingId > 0)
        {
            var sticky = await _db.StockReceipts
                .Where(r => r.Id == existingId)
                .Include(r => r.Profile)
                .Select(r => r.Profile)
                .FirstOrDefaultAsync(ct);
            return sticky is null
                ? Result.Failure<ReceiptProfile>($"StockReceipt {existingId} not found")
                : Result.Success(sticky);
        }

        // Create: prefer Item.DefaultReceiptProfileId, fall back to client-
        // supplied profileCode (when valid), then fall back to STEEL.
        int? defaultedFromItem = null;
        if (itemId > 0)
        {
            defaultedFromItem = await _db.Items
                .Where(i => i.Id == itemId)
                .Select(i => i.DefaultReceiptProfileId)
                .FirstOrDefaultAsync(ct);
        }

        if (defaultedFromItem is int pid)
        {
            var byId = await _db.ReceiptProfiles.FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (byId is not null) return Result.Success(byId);
        }

        if (!string.IsNullOrWhiteSpace(profileCode))
        {
            var byCode = await _db.ReceiptProfiles
                .FirstOrDefaultAsync(p => p.Code == profileCode && p.IsActive, ct);
            if (byCode is not null) return Result.Success(byCode);
        }

        return await GetDefaultProfileForCreateAsync(ct);
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

        // ADR-015 PR #3 — server-side profile resolution. Ignores any
        // client-side tampering with the ProfileCode hidden input.
        var profileRes = await GetProfileForSubmitAsync(
            id: null, profileCode: request.ProfileCode, itemId: request.ItemId, ct);
        if (profileRes.IsFailure) return Result.Failure<StockReceipt>(profileRes.Error!);
        var profile = profileRes.Value!;

        // ADR-015 PR #3 — JSON Schema validation against profile.JsonSchema.
        var schemaErrors = _validator.Validate(profile, request.Attributes);
        if (schemaErrors.Count > 0)
        {
            return Result.Failure<StockReceipt>(FormatValidationErrors(schemaErrors));
        }

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var entity = new StockReceipt
                {
                    ProfileId = profile.Id,
                    ReceiptNumber = request.ReceiptNumber.Trim(),
                    ItemId = request.ItemId,
                    MaterialMasterId = request.MaterialMasterId,
                    LotNumber = request.LotNumber?.Trim(),
                    SerialNumber = request.SerialNumber?.Trim(),
                    SourcePoNumber = request.SourcePoNumber?.Trim(),
                    SourcePoLineId = request.SourcePoLineId?.Trim(),
                    ReceivedAt = request.ReceivedAt == default ? DateTime.UtcNow : request.ReceivedAt,
                    ReceivedByUserId = request.ReceivedByUserId,
                    LocationId = request.LocationId,
                    QuantityReceived = request.QuantityReceived,
                    QuantityRemaining = request.QuantityReceived,
                    Uom = request.Uom?.Trim(),
                    Status = request.Status,
                    Notes = request.Notes?.Trim(),
                    // ADR-015 PR #3 — Attributes is the only payload now.
                    // The 8 legacy columns are gone with the same-PR migration.
                    Attributes = JsonSerializer.Serialize(request.Attributes),
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
                    description: $"Created StockReceipt '{entity.ReceiptNumber}' (Item {entity.ItemId}, Profile {profile.Code})",
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
                var entity = await _db.StockReceipts
                    .Include(r => r.Profile)
                    .FirstOrDefaultAsync(r => r.Id == id, innerCt);
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

                if (entity.Profile is null)
                    return Result.Failure<StockReceipt>(
                        $"StockReceipt {id} has no ProfileId — Migration PR #2 backfill should have set it");

                // ADR-015 PR #3 — JSON Schema validation against the sticky profile.
                var schemaErrors = _validator.Validate(entity.Profile, request.Attributes);
                if (schemaErrors.Count > 0)
                {
                    return Result.Failure<StockReceipt>(FormatValidationErrors(schemaErrors));
                }

                var before = SnapshotForAudit(entity);

                entity.ReceiptNumber = request.ReceiptNumber.Trim();
                entity.ItemId = request.ItemId;
                entity.MaterialMasterId = request.MaterialMasterId;
                entity.LotNumber = request.LotNumber?.Trim();
                entity.SerialNumber = request.SerialNumber?.Trim();
                entity.SourcePoNumber = request.SourcePoNumber?.Trim();
                entity.SourcePoLineId = request.SourcePoLineId?.Trim();
                entity.ReceivedAt = request.ReceivedAt;
                entity.ReceivedByUserId = request.ReceivedByUserId;
                entity.LocationId = request.LocationId;
                entity.QuantityReceived = request.QuantityReceived;
                entity.QuantityRemaining = request.QuantityRemaining;
                entity.Uom = request.Uom?.Trim();
                entity.Notes = request.Notes?.Trim();
                entity.Attributes = JsonSerializer.Serialize(request.Attributes);
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

    private static string FormatValidationErrors(IReadOnlyList<ValidationError> errors)
    {
        // The Result<T> Error string carries a single composite message.
        // PageModel-side validation (which has access to ModelState) calls
        // the validator directly for per-field error placement.
        return "Attributes failed JSON Schema validation: " +
               string.Join("; ", errors.Select(e =>
                   $"{(string.IsNullOrEmpty(e.Pointer) ? "(root)" : e.Pointer)} [{e.Keyword}]: {e.Message}"));
    }

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

    // ADR-015 PR #3 — audit snapshot reads Attributes only. The 8 legacy
    // properties were dropped from the entity in this PR's migration.
    private static object SnapshotForAudit(StockReceipt r) => new
    {
        r.ReceiptNumber,
        r.ItemId,
        r.MaterialMasterId,
        r.LotNumber,
        r.SerialNumber,
        r.SourcePoNumber,
        r.SourcePoLineId,
        r.ReceivedAt,
        r.ReceivedByUserId,
        r.LocationId,
        r.QuantityReceived,
        r.QuantityRemaining,
        r.Uom,
        Status = r.Status.ToString(),
        r.QuarantineReason,
        r.Notes,
        r.ProfileId,
        Attributes = r.Attributes,
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
