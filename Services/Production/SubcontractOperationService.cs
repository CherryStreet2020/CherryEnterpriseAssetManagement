// Sprint 15.1 PR-4 (2026-05-28) — SubcontractOperationService impl.

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

namespace Abs.FixedAssets.Services.Production;

public class SubcontractOperationService : ISubcontractOperationService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SubcontractOperationService> _logger;

    public SubcontractOperationService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<SubcontractOperationService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // CreateSubcontractOperationAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<CreateSubcontractOperationResult>> CreateSubcontractOperationAsync(
        CreateSubcontractOperationRequest request, CancellationToken ct = default)
    {
        var pro = await _db.Set<ProductionOrder>()
            .Where(p => p.Id == request.ProductionOrderId &&
                        _tenantContext.VisibleCompanyIds.Contains(p.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (pro == null)
            return Result.Failure<CreateSubcontractOperationResult>(
                $"PRO {request.ProductionOrderId} not found or not in tenant scope.");

        // Idempotency: same PRO + op seq → return existing
        var existing = await _db.Set<SubcontractOperation>()
            .Where(s => s.ProductionOrderId == request.ProductionOrderId &&
                        s.OperationSequence == request.OperationSequence)
            .FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            return Result.Success(new CreateSubcontractOperationResult(
                existing.Id, existing.ProductionOrderId, existing.OperationSequence,
                "Already exists — idempotent return."));
        }

        var op = new SubcontractOperation
        {
            CompanyId = pro.CompanyId,
            SiteId = null,
            ProductionOrderId = request.ProductionOrderId,
            OperationSequence = request.OperationSequence,
            OperationCode = request.OperationCode,
            OperationDescription = request.OperationDescription,
            SupplierId = request.SupplierId,
            ServiceItemId = request.ServiceItemId,
            ServiceUnitCost = request.ServiceUnitCost,   // Codex P2 fix
            WipItemId = request.WipItemId,               // Codex P2 fix
            QuantityToShip = request.QuantityToShip,
            PriorOperationSequence = request.PriorOperationSequence,
            ReturnOperationSequence = request.ReturnOperationSequence,
            RequiredShipDate = request.RequiredShipDate,
            RequiredBackDate = request.RequiredBackDate,
            ShipWipRequired = request.ShipWipRequired,
            GenerateSubcontractPo = request.GenerateSubcontractPo,
            FixedLeadTimeDays = request.FixedLeadTimeDays ?? 0m,
            VariableLeadTimeDaysPerUnit = request.VariableLeadTimeDaysPerUnit ?? 0m,
            Status = SubcontractOperationStatus.NotReady,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy ?? "system",
            Notes = request.Notes,
        };
        _db.Add(op);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SubcontractOperation: created op #{Id} for PRO {ProId} op seq {Seq} supplier {SupId}",
            op.Id, op.ProductionOrderId, op.OperationSequence, op.SupplierId);

        return Result.Success(new CreateSubcontractOperationResult(
            op.Id, op.ProductionOrderId, op.OperationSequence,
            $"Created subcontract op '{op.OperationCode}' on PRO {pro.OrderNumber}, seq {op.OperationSequence}"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CreateSubcontractDemandAsync — the §9 DUAL DEMAND pattern
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<CreateSubcontractDemandResult>> CreateSubcontractDemandAsync(
        int subcontractOperationId, string? createdBy, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Include(s => s.ProductionOrder)
            .Where(s => s.Id == subcontractOperationId &&
                        _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (op == null)
            return Result.Failure<CreateSubcontractDemandResult>(
                $"SubcontractOperation {subcontractOperationId} not found or not in tenant scope.");

        // Idempotency: existing binding
        var existing = await _db.Set<SubcontractDemand>()
            .Where(d => d.SubcontractOperationId == subcontractOperationId)
            .FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            return Result.Success(new CreateSubcontractDemandResult(
                existing.Id, existing.ServicePurchaseDemandId, existing.WipMovementDemandId,
                "Already created — idempotent return."));
        }

        var now = DateTime.UtcNow;
        var by = createdBy ?? op.CreatedBy ?? "system";

        // ── §9 Demand 1: Service Purchase Demand ───────────────────────
        var serviceDemand = new ProductionSupplyDemand
        {
            CompanyId = op.CompanyId,
            DemandNumber = $"SCS-{op.ProductionOrder?.OrderNumber ?? op.ProductionOrderId.ToString()}-{op.OperationSequence:000}",
            ProductionOrderId = op.ProductionOrderId,
            BomLineId = null,
            OperationSequence = op.OperationSequence,
            ItemId = op.ServiceItemId,
            PartNumber = null,
            Description = op.ServiceDescription ?? $"Subcontract service: {op.OperationCode}",
            Uom = op.ServiceUom ?? "EA",
            RequiredQuantity = op.QuantityToShip,
            RemainingQuantity = op.QuantityToShip,
            RequiredDate = op.RequiredBackDate,
            NeedByDate = op.RequiredBackDate,
            SourceType = DemandSourceType.Subcontract,
            SupplyPolicy = SupplyPolicy.BuyToVendorLocation,
            VendorId = op.SupplierId,
            VendorSiteCode = op.SupplierSiteCode,
            VendorWipLocation = op.VendorWipLocation,
            SourceStatus = DemandSourceStatus.NotDetermined,
            SupplyStatus = DemandSupplyStatus.NotSupplied,
            ShortageStatus = DemandShortageStatus.NoShortage,
            CostStatus = DemandCostStatus.NotCommitted,
            CreatedAt = now,
            CreatedBy = by,
            Notes = $"Service-purchase demand for subcontract op {op.OperationCode}",
        };
        _db.Add(serviceDemand);

        // ── §9 Demand 2: WIP Movement Demand ───────────────────────────
        var wipDemand = new ProductionSupplyDemand
        {
            CompanyId = op.CompanyId,
            DemandNumber = $"SCW-{op.ProductionOrder?.OrderNumber ?? op.ProductionOrderId.ToString()}-{op.OperationSequence:000}",
            ProductionOrderId = op.ProductionOrderId,
            BomLineId = null,
            OperationSequence = op.OperationSequence,
            ItemId = op.WipItemId, // Codex P2 fix: carry WIP item into movement demand
            PartNumber = null,
            Description = $"WIP movement to vendor: {op.OperationCode}",
            Uom = "EA",
            RequiredQuantity = op.QuantityToShip,
            RemainingQuantity = op.QuantityToShip,
            RequiredDate = op.RequiredShipDate,
            NeedByDate = op.RequiredShipDate,
            SourceType = DemandSourceType.Subcontract,
            SupplyPolicy = SupplyPolicy.BuyToVendorLocation,
            VendorId = op.SupplierId,
            VendorSiteCode = op.SupplierSiteCode,
            VendorWipLocation = op.VendorWipLocation,
            SourceStatus = DemandSourceStatus.NotDetermined,
            SupplyStatus = DemandSupplyStatus.NotSupplied,
            ShortageStatus = DemandShortageStatus.NoShortage,
            CostStatus = DemandCostStatus.NotCommitted,
            CreatedAt = now,
            CreatedBy = by,
            Notes = $"WIP-movement demand (physical part outside) for subcontract op {op.OperationCode}",
        };
        _db.Add(wipDemand);

        await _db.SaveChangesAsync(ct);

        // ── Bind both demands via SubcontractDemand ────────────────────
        var binding = new SubcontractDemand
        {
            CompanyId = op.CompanyId,
            SiteId = op.SiteId,
            SubcontractOperationId = op.Id,
            ProductionOrderId = op.ProductionOrderId,
            OperationSequence = op.OperationSequence,
            ServicePurchaseDemandId = serviceDemand.Id,
            ServiceQuantity = op.QuantityToShip,
            ServiceUnitCost = op.ServiceUnitCost,   // Codex P2 fix
            WipMovementDemandId = wipDemand.Id,
            WipItemId = op.WipItemId,               // Codex P2 fix
            WipQuantityToSend = op.QuantityToShip,
            WipQuantityReturned = 0m,
            FromOperationSequence = op.PriorOperationSequence,
            ToOperationSequence = op.ReturnOperationSequence,
            Status = SubcontractDemandStatus.Open,
            RequiredBackDate = op.RequiredBackDate,
            CreatedAt = now,
            CreatedBy = by,
            Notes = $"Dual-demand binding for subcontract op {op.OperationCode}",
        };
        _db.Add(binding);

        // Advance op lifecycle to ReadyToBuy now that demands exist
        if (op.Status == SubcontractOperationStatus.NotReady)
            op.Status = SubcontractOperationStatus.ReadyToBuy;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SubcontractDemand: created dual-demand binding #{Id} for op {OpId}: service demand {SvcId} + WIP demand {WipId}",
            binding.Id, op.Id, serviceDemand.Id, wipDemand.Id);

        return Result.Success(new CreateSubcontractDemandResult(
            binding.Id, serviceDemand.Id, wipDemand.Id,
            $"Created service demand #{serviceDemand.Id} ({serviceDemand.DemandNumber}) and WIP demand #{wipDemand.Id} ({wipDemand.DemandNumber}). Op advanced to ReadyToBuy."));
    }

    // ═══════════════════════════════════════════════════════════════════
    // TransitionStatusAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractOperationStatus>> TransitionStatusAsync(
        int subcontractOperationId, SubcontractOperationStatus newStatus,
        string? reason, string? actor, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (op == null)
            return Result.Failure<SubcontractOperationStatus>(
                $"SubcontractOperation {subcontractOperationId} not found or not in tenant scope.");

        var oldStatus = op.Status;
        op.Status = newStatus;
        op.Notes = string.IsNullOrEmpty(op.Notes)
            ? $"[{DateTime.UtcNow:O}] {oldStatus} → {newStatus} by {actor ?? "system"}: {reason}"
            : $"{op.Notes}\n[{DateTime.UtcNow:O}] {oldStatus} → {newStatus} by {actor ?? "system"}: {reason}";

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SubcontractOperation #{Id}: {Old} → {New} by {Actor}",
            op.Id, oldStatus, newStatus, actor);

        return Result.Success(newStatus);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RecordShipmentAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractStatusSummary>> RecordShipmentAsync(
        int subcontractOperationId, decimal quantityShipped, string? notes,
        CancellationToken ct = default)
    {
        if (quantityShipped <= 0m)
            return Result.Failure<SubcontractStatusSummary>("quantityShipped must be > 0.");

        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (op == null)
            return Result.Failure<SubcontractStatusSummary>(
                $"SubcontractOperation {subcontractOperationId} not found or not in tenant scope.");

        op.QuantityShipped += quantityShipped;
        op.ActualShipDate ??= DateTime.UtcNow;
        op.ShipmentStatus = op.QuantityShipped >= op.QuantityToShip
            ? SubcontractShipmentStatus.Delivered
            : SubcontractShipmentStatus.InTransit;

        // Advance op status
        if (op.Status == SubcontractOperationStatus.ReadyToShip ||
            op.Status == SubcontractOperationStatus.ReadyToBuy ||
            op.Status == SubcontractOperationStatus.PoCreated)
        {
            op.Status = SubcontractOperationStatus.ShippedToVendor;
        }

        // Bind status update on dual-demand
        var binding = await _db.Set<SubcontractDemand>()
            .Where(d => d.SubcontractOperationId == op.Id)
            .FirstOrDefaultAsync(ct);
        if (binding != null && binding.Status == SubcontractDemandStatus.ServiceCommitted)
        {
            binding.Status = SubcontractDemandStatus.WipAtVendor;
            binding.WipAtVendorUtc = DateTime.UtcNow;
        }
        else if (binding != null && binding.Status == SubcontractDemandStatus.Open)
        {
            binding.Status = SubcontractDemandStatus.WipAtVendor;
            binding.WipAtVendorUtc = DateTime.UtcNow;
        }

        if (!string.IsNullOrEmpty(notes))
            op.Notes = string.IsNullOrEmpty(op.Notes)
                ? $"[ship {DateTime.UtcNow:O}] {notes}"
                : $"{op.Notes}\n[ship {DateTime.UtcNow:O}] {notes}";

        await _db.SaveChangesAsync(ct);

        return Result.Success(BuildSummary(op));
    }

    // ═══════════════════════════════════════════════════════════════════
    // RecordReceiptAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractStatusSummary>> RecordReceiptAsync(
        int subcontractOperationId, decimal quantityReceived,
        decimal quantityAccepted, decimal quantityRejected,
        string? notes, CancellationToken ct = default)
    {
        if (quantityReceived <= 0m)
            return Result.Failure<SubcontractStatusSummary>("quantityReceived must be > 0.");
        if (quantityAccepted < 0m || quantityRejected < 0m)
            return Result.Failure<SubcontractStatusSummary>("accept/reject must be >= 0.");
        if (quantityAccepted + quantityRejected > quantityReceived)
            return Result.Failure<SubcontractStatusSummary>(
                "accepted + rejected cannot exceed received.");

        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (op == null)
            return Result.Failure<SubcontractStatusSummary>(
                $"SubcontractOperation {subcontractOperationId} not found or not in tenant scope.");

        op.QuantityReceivedBack += quantityReceived;
        op.QuantityAccepted += quantityAccepted;
        op.QuantityRejected += quantityRejected;
        op.ActualBackDate ??= DateTime.UtcNow;

        op.ReceiptStatus = op.QuantityReceivedBack >= op.QuantityToShip
            ? SubcontractReceiptStatus.FullyReceived
            : SubcontractReceiptStatus.PartiallyReceived;

        // Lifecycle advancement
        if (quantityRejected > 0m && op.QuantityAccepted < op.QuantityToShip)
        {
            op.Status = SubcontractOperationStatus.Rejected;
        }
        else if (op.QuantityReceivedBack >= op.QuantityToShip && op.QuantityAccepted >= op.QuantityToShip)
        {
            op.Status = op.InspectionOnReturn
                ? SubcontractOperationStatus.InInspection
                : SubcontractOperationStatus.Complete;
        }
        else if (op.QuantityReceivedBack > 0m)
        {
            op.Status = SubcontractOperationStatus.PartiallyReceived;
        }

        if (!string.IsNullOrEmpty(notes))
            op.Notes = string.IsNullOrEmpty(op.Notes)
                ? $"[recv {DateTime.UtcNow:O}] {notes}"
                : $"{op.Notes}\n[recv {DateTime.UtcNow:O}] {notes}";

        // Update dual-demand binding
        var binding = await _db.Set<SubcontractDemand>()
            .Where(d => d.SubcontractOperationId == op.Id)
            .FirstOrDefaultAsync(ct);
        if (binding != null)
        {
            binding.WipQuantityReturned = op.QuantityReceivedBack;
            if (op.Status == SubcontractOperationStatus.Complete)
            {
                binding.Status = SubcontractDemandStatus.BothSatisfied;
                binding.BothSatisfiedUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(BuildSummary(op));
    }

    // ═══════════════════════════════════════════════════════════════════
    // MarkCompleteAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractStatusSummary>> MarkCompleteAsync(
        int subcontractOperationId, string? actor, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (op == null)
            return Result.Failure<SubcontractStatusSummary>(
                $"SubcontractOperation {subcontractOperationId} not found or not in tenant scope.");

        if (op.Status == SubcontractOperationStatus.Complete ||
            op.Status == SubcontractOperationStatus.Closed)
        {
            return Result.Success(BuildSummary(op));
        }

        op.Status = SubcontractOperationStatus.Complete;
        op.Notes = string.IsNullOrEmpty(op.Notes)
            ? $"[complete {DateTime.UtcNow:O}] by {actor ?? "system"}"
            : $"{op.Notes}\n[complete {DateTime.UtcNow:O}] by {actor ?? "system"}";

        var binding = await _db.Set<SubcontractDemand>()
            .Where(d => d.SubcontractOperationId == op.Id)
            .FirstOrDefaultAsync(ct);
        if (binding != null)
        {
            binding.Status = SubcontractDemandStatus.BothSatisfied;
            binding.BothSatisfiedUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(BuildSummary(op));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reads
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractStatusSummary>> GetStatusAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (op == null)
            return Result.Failure<SubcontractStatusSummary>(
                $"SubcontractOperation {subcontractOperationId} not found or not in tenant scope.");

        return Result.Success(BuildSummary(op));
    }

    public async Task<IReadOnlyList<SubcontractOperation>> GetSubcontractOperationsForProAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<SubcontractOperation>()
            .Where(s => s.ProductionOrderId == productionOrderId &&
                        _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
            .OrderBy(s => s.OperationSequence)
            .ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static SubcontractStatusSummary BuildSummary(SubcontractOperation op) =>
        new(op.Id, op.ProductionOrderId, op.OperationSequence,
            op.Status, op.PoCreationStatus, op.ShipmentStatus, op.ReceiptStatus,
            op.QuantityToShip, op.QuantityShipped, op.QuantityReceivedBack,
            op.QuantityAccepted, op.QuantityRejected);
}
