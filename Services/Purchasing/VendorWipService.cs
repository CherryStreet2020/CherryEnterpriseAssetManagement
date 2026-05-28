// Sprint 15.1 PR-5 (2026-05-28) — VendorWipService implementation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Purchasing;

public class VendorWipService : IVendorWipService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<VendorWipService> _logger;

    public VendorWipService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<VendorWipService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // RegisterVendorLocation
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<int>> RegisterVendorLocationAsync(
        RegisterVendorLocationRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.LocationCode))
            return Result.Failure<int>("LocationCode required.");

        var supplier = await _db.Set<Vendor>()
            .Where(v => v.Id == req.SupplierId)
            .FirstOrDefaultAsync(ct);
        if (supplier == null)
            return Result.Failure<int>($"Supplier {req.SupplierId} not found.");

        var companyId = _tenantContext.CompanyId ?? supplier.CompanyId ?? 0;
        if (companyId == 0 || !_tenantContext.VisibleCompanyIds.Contains(companyId))
            return Result.Failure<int>("No resolvable company / supplier out of tenant scope.");

        // Idempotency: same company + supplier + code → return existing
        var existing = await _db.Set<VendorLocation>()
            .Where(l => l.CompanyId == companyId &&
                        l.SupplierId == req.SupplierId &&
                        l.LocationCode == req.LocationCode)
            .FirstOrDefaultAsync(ct);
        if (existing != null)
            return Result.Success(existing.Id);

        var loc = new VendorLocation
        {
            CompanyId = companyId,
            SupplierId = req.SupplierId,
            LocationCode = req.LocationCode,
            SupplierSiteCode = req.SupplierSiteCode,
            Address = req.Address,
            LinkedWarehouseId = req.LinkedWarehouseId,
            LinkedBinLocationId = req.LinkedBinLocationId,
            LocationType = req.LocationType,
            VendorManaged = req.VendorManaged,
            CustomerOwnedMaterialAllowed = req.CustomerOwnedMaterialAllowed,
            ConsignedMaterialAllowed = req.ConsignedMaterialAllowed,
            WipAllowed = req.WipAllowed,
            InspectionRequiredOnReturn = req.InspectionRequiredOnReturn,
            DefaultShippingMethod = req.DefaultShippingMethod,
            DefaultTransitDays = req.DefaultTransitDays,
            DefaultReceivingLocationId = req.DefaultReceivingLocationId,
            DefaultReturnToOperationSequence = req.DefaultReturnToOperationSequence,
            IsActive = true,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = req.CreatedBy ?? "system",
        };
        _db.Add(loc);
        await _db.SaveChangesAsync(ct);
        return Result.Success(loc.Id);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ShipToVendorAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<VendorWipMovementResult>> ShipToVendorAsync(
        ShipToVendorRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0m)
            return Result.Failure<VendorWipMovementResult>("Quantity must be > 0.");

        var pro = await _db.Set<ProductionOrder>()
            .Where(p => p.Id == req.ProductionOrderId &&
                        _tenantContext.VisibleCompanyIds.Contains(p.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (pro == null)
            return Result.Failure<VendorWipMovementResult>(
                $"PRO {req.ProductionOrderId} not found or not in tenant scope.");

        // Find or create the balance row for this (PRO, op, supplier, part, revision, lot, serial)
        var balance = await _db.Set<VendorWipBalance>()
            .Where(b => b.ProductionOrderId == req.ProductionOrderId &&
                        b.OperationSequence == req.OperationSequence &&
                        b.SupplierId == req.SupplierId &&
                        b.PartNumber == req.PartNumber &&
                        b.Revision == req.Revision &&
                        b.LotNumber == req.LotNumber &&
                        b.SerialNumber == req.SerialNumber)
            .FirstOrDefaultAsync(ct);

        var nowUtc = DateTime.UtcNow;
        if (balance == null)
        {
            balance = new VendorWipBalance
            {
                CompanyId = pro.CompanyId,
                ProductionOrderId = req.ProductionOrderId,
                OperationSequence = req.OperationSequence,
                SupplierId = req.SupplierId,
                VendorLocationId = req.VendorLocationId,
                ItemId = req.ItemId,
                PartNumber = req.PartNumber,
                Revision = req.Revision,
                LotNumber = req.LotNumber,
                SerialNumber = req.SerialNumber,
                SubcontractOperationId = req.SubcontractOperationId,
                InventoryStatus = VendorWipInventoryStatus.InTransitToVendor,
                Ownership = VendorWipOwnership.Us,
                ValuationStatus = VendorWipValuationStatus.Valued,
                QualityStatus = VendorWipQualityStatus.Unknown,
                UnitValue = req.UnitValue,
                RequiredReturnDate = req.RequiredReturnDate,
                FirstShippedUtc = nowUtc,
                LastTransactionUtc = nowUtc,
                CreatedAt = nowUtc,
                CreatedBy = req.CreatedBy ?? "system",
            };
            _db.Add(balance);
            await _db.SaveChangesAsync(ct);
        }

        balance.QuantityShipped += req.Quantity;
        balance.QuantityAtVendor += req.Quantity;
        balance.InventoryStatus = VendorWipInventoryStatus.InTransitToVendor;
        balance.TotalValueAtVendor = balance.QuantityAtVendor * balance.UnitValue;
        balance.LastTransactionUtc = nowUtc;

        var txn = new VendorWipTransaction
        {
            CompanyId = pro.CompanyId,
            // Codex P2 fix: bound under 48 chars. Was previously using
            // OrderNumber (≤32 chars) + 14-char timestamp + 3-char op seq +
            // 4-char "VWT-" + separators → up to 56 chars, blowing past the
            // VendorWipTransaction.TransactionNumber varchar(48). Compact
            // format: VWT-{ProId}-{op:000}-{epochSeconds:x} ≤ ~32 chars.
            TransactionNumber = $"VWT-{pro.Id}-{req.OperationSequence:000}-{((DateTimeOffset)nowUtc).ToUnixTimeSeconds():x}",
            TransactionType = VendorWipTransactionType.ShipToVendor,
            ProductionOrderId = pro.Id,
            OperationSequence = req.OperationSequence,
            SubcontractOperationId = req.SubcontractOperationId,
            VendorWipBalanceId = balance.Id,
            SupplierId = req.SupplierId,
            VendorLocationId = req.VendorLocationId,
            ItemId = req.ItemId,
            PartNumber = req.PartNumber,
            Revision = req.Revision,
            LotNumber = req.LotNumber,
            SerialNumber = req.SerialNumber,
            Quantity = req.Quantity,
            UnitValue = req.UnitValue,
            ExtendedValue = req.Quantity * req.UnitValue,
            Uom = req.Uom,
            FromLocationDescription = req.FromLocationDescription,
            ToLocationDescription = req.ToLocationDescription,
            ShipmentDocument = req.ShipmentDocument,
            TransactionUtc = nowUtc,
            RequiredReturnDate = req.RequiredReturnDate,
            Notes = req.Notes,
            CreatedAt = nowUtc,
            CreatedBy = req.CreatedBy ?? "system",
        };
        _db.Add(txn);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "VendorWip: shipped {Qty} of {Part} rev {Rev} to supplier {Sup} for PRO {Pro} op {Op}. Balance #{BalId}, Txn #{TxnId}",
            req.Quantity, req.PartNumber, req.Revision, req.SupplierId, pro.Id, req.OperationSequence, balance.Id, txn.Id);

        return Result.Success(new VendorWipMovementResult(
            txn.Id, balance.Id, balance.QuantityAtVendor, balance.InventoryStatus,
            $"Shipped {req.Quantity:N4} to supplier {req.SupplierId} for PRO {pro.OrderNumber}, op {req.OperationSequence}. Total at vendor: {balance.QuantityAtVendor:N4}"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ReceiveFromVendorAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<VendorWipMovementResult>> ReceiveFromVendorAsync(
        ReceiveFromVendorRequest req, CancellationToken ct = default)
    {
        if (req.QuantityReceived <= 0m)
            return Result.Failure<VendorWipMovementResult>("QuantityReceived must be > 0.");
        // Codex P2 fix: validate each inspection qty individually so a caller
        // can't pass -1 and trigger negative accept/reject buckets on the balance.
        if (req.QuantityAccepted < 0m)
            return Result.Failure<VendorWipMovementResult>("QuantityAccepted must be >= 0.");
        if (req.QuantityRejected < 0m)
            return Result.Failure<VendorWipMovementResult>("QuantityRejected must be >= 0.");
        if (req.QuantityAccepted + req.QuantityRejected > req.QuantityReceived)
            return Result.Failure<VendorWipMovementResult>(
                "Accepted + rejected cannot exceed received.");

        var balance = await _db.Set<VendorWipBalance>()
            .Where(b => b.Id == req.VendorWipBalanceId &&
                        _tenantContext.VisibleCompanyIds.Contains(b.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (balance == null)
            return Result.Failure<VendorWipMovementResult>(
                $"Balance {req.VendorWipBalanceId} not found or not in tenant scope.");

        if (req.QuantityReceived > balance.QuantityAtVendor)
            return Result.Failure<VendorWipMovementResult>(
                $"QuantityReceived {req.QuantityReceived:N4} > QuantityAtVendor {balance.QuantityAtVendor:N4}.");

        var nowUtc = DateTime.UtcNow;

        balance.QuantityAtVendor -= req.QuantityReceived;
        balance.QuantityReceivedBack += req.QuantityReceived;
        balance.QuantityAccepted += req.QuantityAccepted;
        balance.QuantityRejected += req.QuantityRejected;
        balance.LastTransactionUtc = nowUtc;
        balance.TotalValueAtVendor = balance.QuantityAtVendor * balance.UnitValue;
        balance.QualityStatus = req.QuantityRejected > 0
            ? VendorWipQualityStatus.Rejected
            : (req.QuantityAccepted > 0 ? VendorWipQualityStatus.Accepted : balance.QualityStatus);

        balance.InventoryStatus = balance.QuantityAtVendor == 0m
            ? VendorWipInventoryStatus.ReceivedBack
            : VendorWipInventoryStatus.InTransitFromVendor;

        // Compact + bounded (≤48). PRO Id is int, op is :000.
        var txnNumber = $"VWT-RCV-{balance.ProductionOrderId}-{balance.OperationSequence:000}-{((DateTimeOffset)nowUtc).ToUnixTimeSeconds():x}";

        // Main receipt transaction
        var receiptTxn = new VendorWipTransaction
        {
            CompanyId = balance.CompanyId,
            TransactionNumber = txnNumber,
            TransactionType = VendorWipTransactionType.ReceivedBack,
            ProductionOrderId = balance.ProductionOrderId,
            OperationSequence = balance.OperationSequence,
            SubcontractOperationId = balance.SubcontractOperationId,
            VendorWipBalanceId = balance.Id,
            SupplierId = balance.SupplierId,
            VendorLocationId = balance.VendorLocationId,
            ItemId = balance.ItemId,
            PartNumber = balance.PartNumber,
            Revision = balance.Revision,
            LotNumber = balance.LotNumber,
            SerialNumber = balance.SerialNumber,
            Quantity = req.QuantityReceived,
            UnitValue = balance.UnitValue,
            ExtendedValue = req.QuantityReceived * balance.UnitValue,
            ReceiptDocument = req.ReceiptDocument,
            TransactionUtc = nowUtc,
            Notes = req.Notes,
            CreatedAt = nowUtc,
            CreatedBy = req.CreatedBy ?? "system",
        };
        _db.Add(receiptTxn);

        // Optional accept/reject sub-transactions (auditable detail)
        if (req.QuantityAccepted > 0m)
        {
            _db.Add(new VendorWipTransaction
            {
                CompanyId = balance.CompanyId,
                TransactionNumber = $"{txnNumber}-ACC",
                TransactionType = VendorWipTransactionType.InspectionAccepted,
                ProductionOrderId = balance.ProductionOrderId,
                OperationSequence = balance.OperationSequence,
                SubcontractOperationId = balance.SubcontractOperationId,
                VendorWipBalanceId = balance.Id,
                SupplierId = balance.SupplierId,
                ItemId = balance.ItemId,
                PartNumber = balance.PartNumber,
                Revision = balance.Revision,
                Quantity = req.QuantityAccepted,
                UnitValue = balance.UnitValue,
                ExtendedValue = req.QuantityAccepted * balance.UnitValue,
                TransactionUtc = nowUtc,
                CreatedAt = nowUtc,
                CreatedBy = req.CreatedBy ?? "system",
            });
        }
        if (req.QuantityRejected > 0m)
        {
            _db.Add(new VendorWipTransaction
            {
                CompanyId = balance.CompanyId,
                TransactionNumber = $"{txnNumber}-REJ",
                TransactionType = VendorWipTransactionType.InspectionRejected,
                ProductionOrderId = balance.ProductionOrderId,
                OperationSequence = balance.OperationSequence,
                SubcontractOperationId = balance.SubcontractOperationId,
                VendorWipBalanceId = balance.Id,
                SupplierId = balance.SupplierId,
                ItemId = balance.ItemId,
                PartNumber = balance.PartNumber,
                Revision = balance.Revision,
                Quantity = req.QuantityRejected,
                UnitValue = balance.UnitValue,
                ExtendedValue = req.QuantityRejected * balance.UnitValue,
                TransactionUtc = nowUtc,
                CreatedAt = nowUtc,
                CreatedBy = req.CreatedBy ?? "system",
            });
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(new VendorWipMovementResult(
            receiptTxn.Id, balance.Id, balance.QuantityAtVendor, balance.InventoryStatus,
            $"Received {req.QuantityReceived:N4} from vendor (accepted {req.QuantityAccepted:N4}, rejected {req.QuantityRejected:N4}). Remaining at vendor: {balance.QuantityAtVendor:N4}"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // RecordScrapAtVendorAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<VendorWipMovementResult>> RecordScrapAtVendorAsync(
        RecordScrapAtVendorRequest req, CancellationToken ct = default)
    {
        if (req.QuantityScrapped <= 0m)
            return Result.Failure<VendorWipMovementResult>("QuantityScrapped must be > 0.");

        var balance = await _db.Set<VendorWipBalance>()
            .Where(b => b.Id == req.VendorWipBalanceId &&
                        _tenantContext.VisibleCompanyIds.Contains(b.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (balance == null)
            return Result.Failure<VendorWipMovementResult>(
                $"Balance {req.VendorWipBalanceId} not found or not in tenant scope.");

        if (req.QuantityScrapped > balance.QuantityAtVendor)
            return Result.Failure<VendorWipMovementResult>(
                $"QuantityScrapped {req.QuantityScrapped:N4} > QuantityAtVendor {balance.QuantityAtVendor:N4}.");

        var nowUtc = DateTime.UtcNow;

        balance.QuantityAtVendor -= req.QuantityScrapped;
        balance.QuantityScrappedAtVendor += req.QuantityScrapped;
        balance.LastTransactionUtc = nowUtc;
        balance.TotalValueAtVendor = balance.QuantityAtVendor * balance.UnitValue;
        if (balance.QuantityAtVendor == 0m)
            balance.InventoryStatus = VendorWipInventoryStatus.AtVendorScrap;

        var txn = new VendorWipTransaction
        {
            CompanyId = balance.CompanyId,
            TransactionNumber = $"VWT-SCR-{balance.ProductionOrderId}-{((DateTimeOffset)nowUtc).ToUnixTimeSeconds():x}",
            TransactionType = VendorWipTransactionType.ScrappedAtVendor,
            ProductionOrderId = balance.ProductionOrderId,
            OperationSequence = balance.OperationSequence,
            SubcontractOperationId = balance.SubcontractOperationId,
            VendorWipBalanceId = balance.Id,
            SupplierId = balance.SupplierId,
            ItemId = balance.ItemId,
            PartNumber = balance.PartNumber,
            Revision = balance.Revision,
            LotNumber = balance.LotNumber,
            Quantity = req.QuantityScrapped,
            UnitValue = balance.UnitValue,
            ExtendedValue = req.QuantityScrapped * balance.UnitValue,
            TransactionUtc = nowUtc,
            ReasonCode = req.ReasonCode,
            Notes = req.Notes,
            CreatedAt = nowUtc,
            CreatedBy = req.CreatedBy ?? "system",
        };
        _db.Add(txn);
        await _db.SaveChangesAsync(ct);

        return Result.Success(new VendorWipMovementResult(
            txn.Id, balance.Id, balance.QuantityAtVendor, balance.InventoryStatus,
            $"Scrapped {req.QuantityScrapped:N4} at vendor. Remaining at vendor: {balance.QuantityAtVendor:N4}"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reads
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<VendorWipBalanceSummary>> GetBalanceAsync(
        int vendorWipBalanceId, CancellationToken ct = default)
    {
        var b = await _db.Set<VendorWipBalance>()
            .Where(x => x.Id == vendorWipBalanceId &&
                        _tenantContext.VisibleCompanyIds.Contains(x.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (b == null)
            return Result.Failure<VendorWipBalanceSummary>(
                $"Balance {vendorWipBalanceId} not found or not in tenant scope.");

        var aging = b.FirstShippedUtc.HasValue
            ? (int)(DateTime.UtcNow - b.FirstShippedUtc.Value).TotalDays
            : 0;

        return Result.Success(new VendorWipBalanceSummary(
            b.Id, b.ProductionOrderId, b.OperationSequence, b.SupplierId,
            b.PartNumber, b.QuantityShipped, b.QuantityAtVendor, b.QuantityReceivedBack,
            b.QuantityAccepted, b.QuantityRejected, b.QuantityScrappedAtVendor, b.QuantityLost,
            b.InventoryStatus, aging));
    }

    public async Task<IReadOnlyList<VendorWipBalance>> GetBalancesForProAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<VendorWipBalance>()
            .Where(b => b.ProductionOrderId == productionOrderId &&
                        _tenantContext.VisibleCompanyIds.Contains(b.CompanyId))
            .OrderBy(b => b.OperationSequence)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VendorWipBalance>> GetBalancesForSupplierAsync(
        int supplierId, CancellationToken ct = default)
    {
        return await _db.Set<VendorWipBalance>()
            .Where(b => b.SupplierId == supplierId &&
                        _tenantContext.VisibleCompanyIds.Contains(b.CompanyId))
            .OrderByDescending(b => b.LastTransactionUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VendorWipTransaction>> GetTransactionsForBalanceAsync(
        int vendorWipBalanceId, CancellationToken ct = default)
    {
        return await _db.Set<VendorWipTransaction>()
            .Where(t => t.VendorWipBalanceId == vendorWipBalanceId &&
                        _tenantContext.VisibleCompanyIds.Contains(t.CompanyId))
            .OrderBy(t => t.TransactionUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VendorLocation>> GetVendorLocationsForSupplierAsync(
        int supplierId, CancellationToken ct = default)
    {
        return await _db.Set<VendorLocation>()
            .Where(l => l.SupplierId == supplierId &&
                        _tenantContext.VisibleCompanyIds.Contains(l.CompanyId))
            .OrderBy(l => l.LocationCode)
            .ToListAsync(ct);
    }
}
