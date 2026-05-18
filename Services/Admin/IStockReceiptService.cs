using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 PR #5 — StockReceipt admin service.
//
// StockReceipt is the physical-lot record — one row per sheet of stock
// that arrives from the supplier, carrying the heat number / mill cert
// / source PO trail that downstream Nests and Remnants inherit from.
//
// Same Result<T> + IdempotencyMediator + AuditLog pattern as the rest
// of Wave 1 (PR #216 RegulatoryProfile, PR #217 MaterialMaster, PR #218
// Vendor). The Razor page + the future voice-AI MCP tool layer share
// these methods.
public interface IStockReceiptService
{
    Task<Result<IReadOnlyList<StockReceipt>>> ListAsync(
        StockReceiptStatus? status,
        CancellationToken ct);

    Task<Result<StockReceipt>> GetAsync(int id, CancellationToken ct);

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

public sealed record CreateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    string? HeatNumber,
    string? LotNumber,
    string? MillCertUrl,
    string? Mill,
    string? SourcePoNumber,
    string? SourcePoLineId,
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal? LengthMm,
    decimal? WidthMm,
    decimal? ThicknessMm,
    decimal QuantityReceived,
    string? Uom,
    StockReceiptStatus Status,
    string? Notes);

public sealed record UpdateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    string? HeatNumber,
    string? LotNumber,
    string? MillCertUrl,
    string? Mill,
    string? SourcePoNumber,
    string? SourcePoLineId,
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal? LengthMm,
    decimal? WidthMm,
    decimal? ThicknessMm,
    decimal? UsableLengthMm,
    decimal? UsableWidthMm,
    decimal QuantityReceived,
    decimal QuantityRemaining,
    string? Uom,
    string? Notes);
