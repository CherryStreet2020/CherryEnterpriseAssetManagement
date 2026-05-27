// =============================================================================
// B8 PR-PRO-11 — Material Transaction Validators (Rules 1-8)
//
// Guards on IProductionMaterialTransactionService actions.
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Production.Validators;

// ─── 1. Revision Check ──────────────────────────────────────────────────────
/// <summary>Cannot issue wrong revision without override.</summary>
public sealed class RevisionCheckValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "RevisionCheck";
    public IReadOnlySet<string> ApplicableActionTypes { get; } =
        TransactionActions.MaterialIssueActions;

    public RevisionCheckValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.BomLineId is not > 0 || ctx.ItemId is not > 0) return TransactionValidationResult.Valid;

        // Revision check compares the PRO's drawing revision against the item's current revision.
        // ComponentRevision on BOM line is a future field; for now check PRO header.
        var pro = await _db.Set<Models.Production.ProductionOrder>()
            .AsNoTracking()
            .Where(p => p.Id == ctx.ProductionOrderId && p.CompanyId == ctx.CompanyId)
            .Select(p => new { p.DrawingRevision })
            .FirstOrDefaultAsync(ct);
        if (pro?.DrawingRevision == null) return TransactionValidationResult.Valid;

        var itemRev = await _db.Items.AsNoTracking()
            .Where(i => i.Id == ctx.ItemId && i.CompanyId == ctx.CompanyId)
            .Select(i => i.Revision)
            .FirstOrDefaultAsync(ct);

        if (itemRev != null && !string.Equals(pro.DrawingRevision, itemRev, StringComparison.OrdinalIgnoreCase))
            return TransactionValidationResult.Block(Name,
                $"PRO drawing rev {pro.DrawingRevision} differs from item rev {itemRev}. Override required.",
                "SupervisorOverride");

        return TransactionValidationResult.Valid;
    }
}

// ─── 2. Lot/Serial Required ─────────────────────────────────────────────────
/// <summary>Lot/serial required for controlled items.</summary>
public sealed class LotSerialRequiredValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "LotSerialRequired";
    public IReadOnlySet<string> ApplicableActionTypes { get; } =
        TransactionActions.MaterialIssueActions;

    public LotSerialRequiredValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.BomLineId is not > 0) return TransactionValidationResult.Valid;

        var flags = await _db.Set<Models.Production.ProductionMaterialStructure>()
            .AsNoTracking()
            .Where(b => b.Id == ctx.BomLineId && b.CompanyId == ctx.CompanyId)
            .Select(b => new { b.IsLotControlled, b.IsSerialControlled })
            .FirstOrDefaultAsync(ct);
        if (flags == null) return TransactionValidationResult.Valid;

        if (flags.IsLotControlled && string.IsNullOrEmpty(ctx.LotNumber))
            return TransactionValidationResult.Block(Name,
                "Lot number required for lot-controlled component. Provide lot number before issuing.");

        if (flags.IsSerialControlled && string.IsNullOrEmpty(ctx.SerialNumber))
            return TransactionValidationResult.Block(Name,
                "Serial number required for serial-controlled component. Provide serial number before issuing.");

        return TransactionValidationResult.Valid;
    }
}

// ─── 3. Expired Material ────────────────────────────────────────────────────
/// <summary>Block or warn expired material.</summary>
public sealed class ExpiredMaterialValidator : IProductionTransactionValidator
{
    public string Name => "ExpiredMaterial";
    public IReadOnlySet<string> ApplicableActionTypes { get; } =
        TransactionActions.MaterialIssueActions;

    public Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        // Shelf-life/expiration check requires lot-level expiry data
        // which lives in InventoryLot (future Sprint 15+ entity).
        // For now: pass. The validator is wired and ready for data.
        return Task.FromResult(TransactionValidationResult.Valid);
    }
}

// ─── 4. Quality Hold ────────────────────────────────────────────────────────
/// <summary>Quality hold blocks issue/consume unless approved.</summary>
public sealed class QualityHoldValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "QualityHold";
    public IReadOnlySet<string> ApplicableActionTypes { get; } = new HashSet<string>(
        TransactionActions.MaterialIssueActions
            .Concat(new[] { TransactionActions.Return, TransactionActions.Substitute,
                           TransactionActions.RecordCompletion }));

    public QualityHoldValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.OperationId is not > 0) return TransactionValidationResult.Valid;

        var op = await _db.Set<Models.Production.ProductionOperation>()
            .AsNoTracking()
            .Where(o => o.Id == ctx.OperationId && o.CompanyIdSnapshot == ctx.CompanyId)
            .Select(o => new { o.QualityHoldActive, o.QualityHoldReason })
            .FirstOrDefaultAsync(ct);

        if (op?.QualityHoldActive == true)
            return TransactionValidationResult.Block(Name,
                $"Operation is on quality hold: {op.QualityHoldReason ?? "no reason specified"}. Release hold before transacting.",
                "SupervisorOverride");

        return TransactionValidationResult.Valid;
    }
}

// ─── 5. Customer Ownership ──────────────────────────────────────────────────
/// <summary>Block cross-customer transfer of customer-owned inventory.</summary>
public sealed class CustomerOwnershipValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "CustomerOwnership";
    public IReadOnlySet<string> ApplicableActionTypes { get; } = new HashSet<string>
    {
        TransactionActions.TransferToJob
    };

    public CustomerOwnershipValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.BomLineId is not > 0) return TransactionValidationResult.Valid;

        var isCustomerSupplied = await _db.Set<Models.Production.ProductionMaterialStructure>()
            .AsNoTracking()
            .Where(b => b.Id == ctx.BomLineId && b.CompanyId == ctx.CompanyId)
            .Select(b => b.IsCustomerSupplied)
            .FirstOrDefaultAsync(ct);

        if (isCustomerSupplied)
        {
            // Check target PRO has same customer
            if (ctx.TargetProductionOrderId is > 0)
            {
                var sourceCustomer = await _db.Set<Models.Production.ProductionOrder>()
                    .AsNoTracking()
                    .Where(p => p.Id == ctx.ProductionOrderId && p.CompanyId == ctx.CompanyId)
                    .Select(p => p.CustomerId)
                    .FirstOrDefaultAsync(ct);

                var targetCustomer = await _db.Set<Models.Production.ProductionOrder>()
                    .AsNoTracking()
                    .Where(p => p.Id == ctx.TargetProductionOrderId && p.CompanyId == ctx.CompanyId)
                    .Select(p => p.CustomerId)
                    .FirstOrDefaultAsync(ct);

                if (sourceCustomer != targetCustomer)
                    return TransactionValidationResult.Block(Name,
                        "Cannot transfer customer-supplied material to a job for a different customer.");
            }
        }

        return TransactionValidationResult.Valid;
    }
}

// ─── 6. Consigned Material ──────────────────────────────────────────────────
/// <summary>Preserve consigned material ownership.</summary>
public sealed class ConsignedMaterialValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "ConsignedMaterial";
    public IReadOnlySet<string> ApplicableActionTypes { get; } = new HashSet<string>
    {
        TransactionActions.TransferToJob, TransactionActions.Substitute
    };

    public ConsignedMaterialValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.BomLineId is not > 0) return TransactionValidationResult.Valid;

        var isConsigned = await _db.Set<Models.Production.ProductionMaterialStructure>()
            .AsNoTracking()
            .Where(b => b.Id == ctx.BomLineId && b.CompanyId == ctx.CompanyId)
            .Select(b => b.IsConsigned)
            .FirstOrDefaultAsync(ct);

        if (isConsigned)
            return TransactionValidationResult.Block(Name,
                "Consigned material cannot be transferred or substituted without vendor approval.",
                "SupervisorOverride");

        return TransactionValidationResult.Valid;
    }
}

// ─── 7. Negative Inventory ──────────────────────────────────────────────────
/// <summary>Negative inventory rule-controlled.</summary>
public sealed class NegativeInventoryValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "NegativeInventory";
    public IReadOnlySet<string> ApplicableActionTypes { get; } =
        TransactionActions.MaterialIssueActions;

    public NegativeInventoryValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.BomLineId is not > 0 || ctx.Quantity is not > 0) return TransactionValidationResult.Valid;

        var line = await _db.Set<Models.Production.ProductionMaterialStructure>()
            .AsNoTracking()
            .Where(b => b.Id == ctx.BomLineId && b.CompanyId == ctx.CompanyId)
            .Select(b => new { Remaining = b.QuantityPer - b.IssuedQuantity })
            .FirstOrDefaultAsync(ct);

        if (line != null && ctx.Quantity > line.Remaining + 0.001m)
            return TransactionValidationResult.Warn(Name,
                $"Issue quantity {ctx.Quantity:N2} exceeds remaining {line.Remaining:N2}. This will create a negative position.");

        return TransactionValidationResult.Valid;
    }
}

// ─── 8. Over-Issue Approval ─────────────────────────────────────────────────
/// <summary>Over-issue requires reason and supervisor approval.</summary>
public sealed class OverIssueApprovalValidator : IProductionTransactionValidator
{
    public string Name => "OverIssueApproval";
    public IReadOnlySet<string> ApplicableActionTypes { get; } = new HashSet<string>
    {
        TransactionActions.OverIssue
    };

    public Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ctx.ReasonCode))
            return Task.FromResult(TransactionValidationResult.Block(Name,
                "Over-issue requires a reason code. Provide justification before proceeding."));

        if (!ctx.SupervisorOverride)
            return Task.FromResult(TransactionValidationResult.Block(Name,
                "Over-issue requires supervisor approval. Request override from your supervisor.",
                "SupervisorOverride"));

        return Task.FromResult(TransactionValidationResult.Valid);
    }
}
