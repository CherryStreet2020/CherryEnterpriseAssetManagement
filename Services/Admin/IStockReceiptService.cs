using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 PR #5 + ADR-015 Migration PR #3 —
// StockReceipt admin service.
//
// StockReceipt is the physical-lot record — one row per sheet of stock
// that arrives from the supplier, carrying the heat number / mill cert
// / source PO trail that downstream Nests and Remnants inherit from.
//
// Migration PR #3 collapses the 8 steel-specific request fields into
// a profile-driven Attributes Dictionary. Profile is sticky on Update.
public interface IStockReceiptService
{
    Task<Result<IReadOnlyList<StockReceipt>>> ListAsync(
        StockReceiptStatus? status,
        CancellationToken ct);

    Task<Result<StockReceipt>> GetAsync(int id, CancellationToken ct);

    // ADR-015 PR #3 — new helpers consumed by the Edit page:
    //   - GetDefaultProfileForCreateAsync: returns STEEL (the v1 default).
    //   - GetWithProfileAsync: load the receipt + its sticky profile in one round-trip.
    //   - GetProfileForSubmitAsync: server-side profile resolution for POST,
    //     ignoring any user-supplied profileCode when an existing receipt
    //     is being updated (the receipt's profile wins).

    Task<Result<ReceiptProfile>> GetDefaultProfileForCreateAsync(CancellationToken ct);

    Task<Result<(StockReceipt entity, ReceiptProfile profile)>> GetWithProfileAsync(
        int id, CancellationToken ct);

    Task<Result<ReceiptProfile>> GetProfileForSubmitAsync(
        int? id, string profileCode, int itemId, CancellationToken ct);

    Task<Result<StockReceipt>> CreateAsync(
        CreateStockReceiptRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);

    Task<Result<StockReceipt>> UpdateAsync(
        int id,
        UpdateStockReceiptRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);

    Task<Result<StockReceipt>> SetStatusAsync(
        int id,
        StockReceiptStatus newStatus,
        string? quarantineReason,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);
}

// ADR-015 Migration PR #3 — collapsed Create DTO. The 8 steel-specific
// fields (HeatNumber, MillCertUrl, Mill, Length/Width/Thickness,
// UsableLength/Width) are gone; Attributes carries them now.
public sealed record CreateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    string ProfileCode,
    string? LotNumber,
    string? SerialNumber,
    string? SourcePoNumber,
    string? SourcePoLineId,
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal QuantityReceived,
    string? Uom,
    StockReceiptStatus Status,
    string? Notes,
    IReadOnlyDictionary<string, object?> Attributes);

// Update DTO — no ProfileCode (profile is sticky on Update; server
// preserves the receipt's existing ProfileId).
public sealed record UpdateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    string? LotNumber,
    string? SerialNumber,
    string? SourcePoNumber,
    string? SourcePoLineId,
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal QuantityReceived,
    decimal QuantityRemaining,
    string? Uom,
    string? Notes,
    IReadOnlyDictionary<string, object?> Attributes);
