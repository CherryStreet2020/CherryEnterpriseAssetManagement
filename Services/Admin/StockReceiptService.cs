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

        // ADR-015 Migration PR #2 — resolve the default ReceiptProfile id
        // outside the idempotency callback so the lookup itself is cached
        // for the lifetime of the request. The existing PR #219 admin form
        // only emits steel-specific fields, so we resolve to STEEL by
        // default. When Item.DefaultReceiptProfileId is populated (Sprint
        // 7+), the profile flows in from the Item master.
        var profileId = await ResolveProfileIdForCreateAsync(request.ItemId, ct);

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var entity = new StockReceipt
                {
                    ProfileId = profileId,    // ADR-015 D1 — every receipt has a profile
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
                    // ADR-015 Migration PR #2 — dual-write Attributes from
                    // the steel-specific legacy fields. Once Migration PR
                    // #3 ships, the form emits Attributes directly and the
                    // legacy columns are dropped.
                    Attributes = BuildSteelAttributesJson(request),
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
                // ADR-015 Migration PR #2 — dual-write Attributes alongside
                // the legacy columns on every Update. Profile is sticky;
                // we never overwrite ProfileId on update.
                entity.Attributes = BuildSteelAttributesJsonFromUpdate(request);
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

    // ADR-015 Migration PR #2 — Resolve which ReceiptProfile.Id this new
    // receipt belongs to. Resolution order:
    //   1. Item.DefaultReceiptProfileId (Sprint 7+, when Item master is
    //      industry-tagged)
    //   2. Fallback to STEEL profile (the only one the current PR #219
    //      form populates real fields for)
    //
    // The result is cached on the service for the request lifetime to
    // avoid repeated lookups.
    private int? _cachedSteelProfileId;
    private async Task<int> ResolveProfileIdForCreateAsync(int itemId, CancellationToken ct)
    {
        var item = await _db.Items
            .Where(i => i.Id == itemId)
            .Select(i => new { i.DefaultReceiptProfileId })
            .FirstOrDefaultAsync(ct);
        if (item?.DefaultReceiptProfileId is int defaulted) return defaulted;

        if (_cachedSteelProfileId is int cached) return cached;
        var steelId = await _db.ReceiptProfiles
            .Where(p => p.Code == "STEEL")
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "ReceiptProfile 'STEEL' not found. Migration PR #1 must run before any " +
                "receipt is created. See ADR-015 §D9 and migration " +
                "20260518_AddReceiptProfileCatalog.");
        _cachedSteelProfileId = steelId;
        return steelId;
    }

    // ADR-015 Migration PR #2 — Build the STEEL-profile Attributes JSON
    // from the legacy steel-specific fields on a CreateStockReceiptRequest.
    // Migration PR #3 will replace the form with a profile-driven renderer
    // that emits Attributes directly; this dual-write is the transition.
    private static string BuildSteelAttributesJson(CreateStockReceiptRequest r)
    {
        var attrs = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(r.HeatNumber)) attrs["heatNumber"] = r.HeatNumber.Trim();
        if (!string.IsNullOrWhiteSpace(r.Mill)) attrs["mill"] = r.Mill.Trim();
        if (!string.IsNullOrWhiteSpace(r.MillCertUrl)) attrs["millCertUrl"] = r.MillCertUrl.Trim();
        if (r.LengthMm.HasValue) attrs["lengthMm"] = r.LengthMm.Value;
        if (r.WidthMm.HasValue) attrs["widthMm"] = r.WidthMm.Value;
        if (r.ThicknessMm.HasValue) attrs["thicknessMm"] = r.ThicknessMm.Value;
        // UsableLength/Width default to the full dims on first create (matches
        // the entity initialization above). Migration PR #3 will pull these
        // from the form directly.
        if (r.LengthMm.HasValue) attrs["usableLengthMm"] = r.LengthMm.Value;
        if (r.WidthMm.HasValue) attrs["usableWidthMm"] = r.WidthMm.Value;
        return JsonSerializer.Serialize(attrs);
    }

    // ADR-015 Migration PR #2 — Same shape as BuildSteelAttributesJson,
    // but reads from an UpdateStockReceiptRequest. The Update DTO carries
    // both LengthMm AND UsableLengthMm because users can adjust the
    // usable-dims as cuts consume the sheet.
    private static string BuildSteelAttributesJsonFromUpdate(UpdateStockReceiptRequest r)
    {
        var attrs = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(r.HeatNumber)) attrs["heatNumber"] = r.HeatNumber.Trim();
        if (!string.IsNullOrWhiteSpace(r.Mill)) attrs["mill"] = r.Mill.Trim();
        if (!string.IsNullOrWhiteSpace(r.MillCertUrl)) attrs["millCertUrl"] = r.MillCertUrl.Trim();
        if (r.LengthMm.HasValue) attrs["lengthMm"] = r.LengthMm.Value;
        if (r.WidthMm.HasValue) attrs["widthMm"] = r.WidthMm.Value;
        if (r.ThicknessMm.HasValue) attrs["thicknessMm"] = r.ThicknessMm.Value;
        if (r.UsableLengthMm.HasValue) attrs["usableLengthMm"] = r.UsableLengthMm.Value;
        if (r.UsableWidthMm.HasValue) attrs["usableWidthMm"] = r.UsableWidthMm.Value;
        return JsonSerializer.Serialize(attrs);
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
