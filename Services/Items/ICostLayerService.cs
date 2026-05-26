// B6 Foundation Sprint PR-FS-4 (2026-05-26) — ICostLayerService.
//
// Service surface for inventory-valuation cost layers (SAP MM "stock with values"
// equivalent / Oracle Cost Layer / D365 inventTrans cost-stack). Each receipt
// creates one immutable layer; each issue consumes from open layers per the
// configured CostMethod (FIFO/LIFO/Average).
//
// Per Lock 15 — IService surface only, never direct DbContext usage from callers.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;

namespace Abs.FixedAssets.Services.Items;

public interface ICostLayerService
{
    /// <summary>
    /// Record a new cost layer for an inventory receipt. Returns the persisted
    /// layer. Idempotent on <c>(ReceiptType, ReceiptReferenceId, LotNumber)</c>:
    /// if a layer with those values already exists and has matching qty + cost,
    /// the existing row is returned without write. Different qty/cost on same
    /// reference is an error (use ReverseReceiptAsync + new RecordReceiptAsync
    /// to correct).
    /// </summary>
    Task<CostLayer> RecordReceiptAsync(
        int itemId,
        int? siteId,
        CostLayerReceiptType receiptType,
        int? receiptReferenceId,
        string? receiptDocumentNumber,
        decimal quantity,
        decimal unitCost,
        string currencyCode,
        string? lotNumber,
        string? serialNumber,
        string? heatNumber,
        string? vendorLot,
        string? vendorReference,
        string? createdBy,
        CancellationToken ct);

    /// <summary>
    /// Consume <paramref name="quantity"/> from open layers per the cost
    /// method. Returns the per-layer consumption rows describing exactly
    /// which layers were hit and at what unit cost (the cost-engine ingests
    /// this to compute the WIP debit / inventory credit at the correct
    /// per-unit basis).
    ///
    /// If <paramref name="costMethod"/> is <c>Average</c>, all consumption
    /// rows share the single weighted-avg unit cost.
    ///
    /// If insufficient open quantity exists, throws <see cref="InvalidOperationException"/>
    /// — the caller must check <see cref="GetTotalOpenQuantityAsync"/> first or
    /// catch the exception and surface a stock-out error.
    /// </summary>
    Task<IReadOnlyList<CostLayerConsumption>> ConsumeQuantityAsync(
        int itemId,
        int? siteId,
        decimal quantity,
        CostMethod costMethod,
        string? consumedBy,
        string? consumptionReason,
        CancellationToken ct);

    /// <summary>
    /// Get all open layers (RemainingQuantity > 0, Status=Open) for the
    /// (Item, Site) pair, ordered FIFO (oldest first) by default. Useful for
    /// admin probe + cost-engine planning.
    /// </summary>
    Task<IReadOnlyList<CostLayer>> GetOpenLayersAsync(
        int itemId,
        int? siteId,
        CancellationToken ct);

    /// <summary>
    /// Sum of <c>RemainingQuantity</c> across all open layers for the
    /// (Item, Site) pair. Used to gate consumption (prevent over-issue).
    /// </summary>
    Task<decimal> GetTotalOpenQuantityAsync(
        int itemId,
        int? siteId,
        CancellationToken ct);

    /// <summary>
    /// Weighted-average unit cost across all open layers for the (Item, Site)
    /// pair. Returns 0 if no open layers exist. Used for the Average cost
    /// method and for make-vs-buy decision input.
    /// </summary>
    Task<decimal> GetWeightedAverageCostAsync(
        int itemId,
        int? siteId,
        CancellationToken ct);

    /// <summary>
    /// Reverse a previously-recorded receipt. Marks the layer
    /// <c>Status=Reversed</c>, zeroes RemainingQuantity, stamps
    /// <c>ReversedAtUtc</c> + <c>ReversalReason</c>. If consumption has
    /// occurred against this layer (RemainingQuantity != ReceivedQuantity),
    /// throws — the caller must reverse the downstream consumption first.
    /// </summary>
    Task<CostLayer> ReverseReceiptAsync(
        int layerId,
        string reason,
        string? reversedBy,
        CancellationToken ct);
}

/// <summary>
/// One row per layer consumed during a <c>ConsumeQuantityAsync</c> call.
/// Caller pairs the consumption qty × unit cost to compute the WIP debit
/// per layer (preserves traceability — each consumption knows its receipt
/// PO / heat / vendor lot).
/// </summary>
public sealed record CostLayerConsumption(
    int CostLayerId,
    int ItemId,
    int? SiteId,
    decimal QuantityConsumed,
    decimal UnitCost,
    decimal CostConsumed,
    string CurrencyCode,
    string? LotNumber,
    string? SerialNumber,
    string? HeatNumber,
    string? VendorLot,
    int? ReceiptReferenceId,
    CostLayerReceiptType ReceiptType,
    CostMethod CostMethodApplied);
