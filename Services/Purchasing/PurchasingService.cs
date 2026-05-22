// ADR-025 D5 — PurchasingService implementation (Sprint 12.9 PR #4).
//
// Centralizes all 12 write paths off Pages/Purchasing/Details.cshtml.cs.
// Each method preserves the EXACT legacy logic — same Draft-state guards,
// same lookup resolution, same release-quantity rules, same SoD outcome
// handling via IApprovalService.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Approvals;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Purchasing;

public sealed class PurchasingService : IPurchasingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILookupService _lookupService;
    private readonly IApprovalService _approvals;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<PurchasingService> _logger;

    public PurchasingService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILookupService lookupService,
        IApprovalService approvals,
        IOutboxWriter outbox,
        ILogger<PurchasingService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lookupService = lookupService;
        _approvals = approvals;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<Result<PurchaseOrder>> UpdateHeaderAsync(
        UpdatePoHeaderRequest request,
        CancellationToken ct)
    {
        var po = await _db.PurchaseOrders
            .Where(p => p.Id == request.PurchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (po == null)
        {
            return Result.Failure<PurchaseOrder>($"Purchase order {request.PurchaseOrderId} not found or not visible.");
        }

        if (po.Status != POStatus.Draft)
        {
            return Result.Failure<PurchaseOrder>("Only Draft purchase orders can be edited.");
        }

        po.VendorId = request.VendorId;

        if (request.POTypeLookupValueId > 0)
        {
            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, request.POTypeLookupValueId);
            if (lv != null)
            {
                po.POTypeLookupValueId = lv.Id;
                if (int.TryParse(lv.Code, out var enumVal))
                    po.POType = (POType)enumVal;
            }
        }

        po.OrderDate = request.OrderDate;
        po.RequiredDate = request.RequiredDate;
        po.Notes = request.Notes;

        // S2-11: tenant-scope the CIP project link.
        if (request.CipProjectId.HasValue && request.CipProjectId.Value > 0)
        {
            var validProject = await _db.CipProjects
                .AnyAsync(p => p.Id == request.CipProjectId.Value
                    && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0), ct);
            po.CipProjectId = validProject ? request.CipProjectId : null;
        }
        else
        {
            po.CipProjectId = null;
        }

        po.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(po);
    }

    public async Task<Result<PurchaseOrderLine>> AddLineAsync(
        AddPoLineRequest request,
        CancellationToken ct)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Lines)
            .Where(p => p.Id == request.PurchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (po == null)
        {
            return Result.Failure<PurchaseOrderLine>($"Purchase order {request.PurchaseOrderId} not found or not visible.");
        }

        if (po.Status != POStatus.Draft)
        {
            return Result.Failure<PurchaseOrderLine>("Only Draft purchase orders can be modified.");
        }

        var lineNumber = po.Lines.Any() ? po.Lines.Max(l => l.LineNumber) + 1 : 1;
        var lineTotal = request.Quantity * request.UnitPrice;

        var line = new PurchaseOrderLine
        {
            PurchaseOrderId = request.PurchaseOrderId,
            LineNumber = lineNumber,
            ItemId = request.ItemId,
            IsNonItemMaster = request.ItemId == null,
            Description = request.Description,
            PartNumber = request.PartNumber,
            ManufacturerPartNumber = request.MfrPartNumber,
            VendorPartNumber = request.VendorPartNumber,
            Revision = request.Revision,
            UOM = request.Uom,
            QuantityOrdered = request.Quantity,
            UnitPrice = request.UnitPrice,
            LineTotal = lineTotal,
            GlAccountId = request.GlAccountId,
            Notes = request.Notes
        };

        po.Lines.Add(line);

        po.Subtotal = po.Lines.Sum(l => l.LineTotal);
        po.Total = po.Subtotal + po.TaxAmount + po.ShippingAmount;
        po.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(line);
    }

    public async Task<Result<PurchaseOrderLine>> UpdateLineAsync(
        UpdatePoLineRequest request,
        CancellationToken ct)
    {
        var line = await _db.PurchaseOrderLines
            .Include(l => l.PurchaseOrder)
            .Where(l => l.Id == request.LineId
                && _tenantContext.VisibleCompanyIds.Contains(l.PurchaseOrder.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (line == null)
        {
            return Result.Failure<PurchaseOrderLine>($"PO line {request.LineId} not found or not visible.");
        }

        if (line.PurchaseOrder.Status != POStatus.Draft)
        {
            return Result.Failure<PurchaseOrderLine>("Only Draft purchase orders can be modified.");
        }

        line.QuantityOrdered = request.Quantity;
        line.UnitPrice = request.UnitPrice;
        line.LineTotal = request.Quantity * request.UnitPrice;

        var po = await _db.PurchaseOrders.Include(p => p.Lines)
            .Where(p => p.Id == request.PurchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (po != null)
        {
            po.Subtotal = po.Lines.Sum(l => l.LineTotal);
            po.Total = po.Subtotal + po.TaxAmount + po.ShippingAmount;
            po.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(line);
    }

    public async Task<Result<int>> DeleteLineAsync(
        DeletePoLineRequest request,
        CancellationToken ct)
    {
        var line = await _db.PurchaseOrderLines
            .Include(l => l.Releases)
            .Include(l => l.PurchaseOrder)
            .Where(l => l.Id == request.LineId
                && _tenantContext.VisibleCompanyIds.Contains(l.PurchaseOrder.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (line == null)
        {
            return Result.Failure<int>($"PO line {request.LineId} not found or not visible.");
        }

        if (line.PurchaseOrder.Status != POStatus.Draft)
        {
            return Result.Failure<int>("Only Draft purchase orders can be modified.");
        }

        _db.PurchaseOrderReleases.RemoveRange(line.Releases);
        _db.PurchaseOrderLines.Remove(line);

        var po = await _db.PurchaseOrders.Include(p => p.Lines)
            .Where(p => p.Id == request.PurchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (po != null)
        {
            po.Subtotal = po.Lines.Where(l => l.Id != request.LineId).Sum(l => l.LineTotal);
            po.Total = po.Subtotal + po.TaxAmount + po.ShippingAmount;
            po.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(request.PurchaseOrderId);
    }

    public async Task<Result<PurchaseOrderRelease>> AddReleaseAsync(
        AddPoReleaseRequest request,
        CancellationToken ct)
    {
        var line = await _db.PurchaseOrderLines
            .Include(l => l.Releases)
            .Include(l => l.PurchaseOrder)
            .Where(l => l.Id == request.LineId
                && _tenantContext.VisibleCompanyIds.Contains(l.PurchaseOrder.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (line == null)
        {
            return Result.Failure<PurchaseOrderRelease>($"PO line {request.LineId} not found or not visible.");
        }

        if (line.PurchaseOrder.Status != POStatus.Draft)
        {
            return Result.Failure<PurchaseOrderRelease>("Only Draft purchase orders can be modified.");
        }

        var currentReleaseQty = line.Releases.Sum(r => r.Quantity);
        if (currentReleaseQty + request.Quantity > line.QuantityOrdered)
        {
            return Result.Failure<PurchaseOrderRelease>(
                $"Release quantity ({request.Quantity:N0}) exceeds remaining ({line.QuantityOrdered - currentReleaseQty:N0})");
        }

        var releaseNumber = line.Releases.Any() ? line.Releases.Max(r => r.ReleaseNumber) + 1 : 1;

        var release = new PurchaseOrderRelease
        {
            PurchaseOrderLineId = request.LineId,
            ReleaseNumber = releaseNumber,
            Quantity = request.Quantity,
            ShipToLocationId = request.ShipToLocationId,
            DueDate = request.DueDate,
            Notes = request.Notes
        };

        _db.PurchaseOrderReleases.Add(release);
        await _db.SaveChangesAsync(ct);
        return Result.Success(release);
    }

    public async Task<Result<int>> DeleteReleaseAsync(
        DeletePoReleaseRequest request,
        CancellationToken ct)
    {
        var release = await _db.PurchaseOrderReleases
            .Include(r => r.PurchaseOrderLine)
                .ThenInclude(l => l.PurchaseOrder)
            .Where(r => r.Id == request.ReleaseId
                && _tenantContext.VisibleCompanyIds.Contains(r.PurchaseOrderLine.PurchaseOrder.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);

        if (release == null)
        {
            // Legacy behavior: silently redirect — no NotFound.
            return Result.Success(request.PurchaseOrderId);
        }

        if (release.PurchaseOrderLine.PurchaseOrder.Status != POStatus.Draft)
        {
            return Result.Failure<int>("Only Draft purchase orders can be modified.");
        }

        _db.PurchaseOrderReleases.Remove(release);
        await _db.SaveChangesAsync(ct);
        return Result.Success(request.PurchaseOrderId);
    }

    public async Task<Result<PurchaseOrder>> SubmitForApprovalAsync(
        int purchaseOrderId,
        CancellationToken ct)
    {
        var po = await _db.PurchaseOrders
            .Where(p => p.Id == purchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (po == null)
        {
            return Result.Failure<PurchaseOrder>($"Purchase order {purchaseOrderId} not found or not visible.");
        }

        if (po.Status == POStatus.Draft)
        {
            await SyncStatusFkAsync(po, POStatus.PendingApproval);
            po.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(po);
    }

    public async Task<Result<ApprovePoOutcome>> ApproveAsync(
        ApprovePoRequest request,
        CancellationToken ct)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.RequestedBy)
            .Where(p => p.Id == request.PurchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (po == null)
        {
            return Result.Failure<ApprovePoOutcome>($"Purchase order {request.PurchaseOrderId} not found or not visible.");
        }

        if (po.Status != POStatus.PendingApproval)
        {
            return Result.Failure<ApprovePoOutcome>(
                $"PO is in status {po.Status}; only PendingApproval can be approved.");
        }

        var result = await _approvals.RecordDecisionAsync(
            targetEntityType: "PurchaseOrder",
            targetEntityId: po.Id,
            workflowType: WorkflowType.PurchaseOrder,
            amount: po.Total,
            decision: ApprovalDecision.Approved,
            approverUserId: request.ApproverUsername,
            approverUsername: request.ApproverUsername,
            approverRoles: request.ApproverRoles.ToList(),
            creatorUserId: po.RequestedBy?.Username,
            comment: request.Comment,
            companyId: po.CompanyId);

        if (result.Outcome == ApprovalOutcome.SodViolation
            || result.Outcome == ApprovalOutcome.DuplicateApprover
            || result.Outcome == ApprovalOutcome.InsufficientRole)
        {
            return Result.Success(new ApprovePoOutcome(
                PurchaseOrderId: po.Id,
                PoNumber: po.PONumber,
                Status: ApprovePoStatus.Rejected,
                ApprovalsRecorded: result.ApprovalsRecorded,
                ApprovalsRequired: result.ApprovalsRequired,
                Message: result.ErrorMessage));
        }

        if (result.Outcome == ApprovalOutcome.FullyApproved
            || result.Outcome == ApprovalOutcome.NoWorkflowApplicable)
        {
            await SyncStatusFkAsync(po, POStatus.Approved);
            po.ApprovedAt = DateTime.UtcNow;
            po.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await _outbox.EnqueueAsync(
                po.CompanyId ?? 0,
                siteId: po.ShipToSiteId,
                new PoApprovedV1(
                    PurchaseOrderId: po.Id,
                    PoNumber: po.PONumber,
                    VendorId: po.VendorId,
                    CompanyId: po.CompanyId,
                    CipProjectId: po.CipProjectId,
                    Total: po.Total,
                    OrderDate: po.OrderDate,
                    RequiredDate: po.RequiredDate,
                    ApprovedAt: po.ApprovedAt!.Value,
                    ApproverUsername: request.ApproverUsername),
                correlationId: $"po-approve-{po.Id}"
            );

            return Result.Success(new ApprovePoOutcome(
                PurchaseOrderId: po.Id,
                PoNumber: po.PONumber,
                Status: ApprovePoStatus.Approved,
                ApprovalsRecorded: result.ApprovalsRecorded,
                ApprovalsRequired: result.ApprovalsRequired,
                Message: $"PO {po.PONumber} fully approved ({result.ApprovalsRecorded} of {result.ApprovalsRequired})."));
        }

        // Partial approval — record decision but PO stays PendingApproval.
        return Result.Success(new ApprovePoOutcome(
            PurchaseOrderId: po.Id,
            PoNumber: po.PONumber,
            Status: ApprovePoStatus.PartiallyApproved,
            ApprovalsRecorded: result.ApprovalsRecorded,
            ApprovalsRequired: result.ApprovalsRequired,
            Message: $"Approval recorded ({result.ApprovalsRecorded} of {result.ApprovalsRequired}). Awaiting additional approver(s)."));
    }

    public async Task<Result<RejectPoOutcome>> RejectAsync(
        RejectPoRequest request,
        CancellationToken ct)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.RequestedBy)
            .Where(p => p.Id == request.PurchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (po == null)
        {
            return Result.Failure<RejectPoOutcome>($"Purchase order {request.PurchaseOrderId} not found or not visible.");
        }

        if (po.Status != POStatus.PendingApproval)
        {
            return Result.Failure<RejectPoOutcome>("Only PendingApproval POs can be rejected.");
        }

        var result = await _approvals.RecordDecisionAsync(
            targetEntityType: "PurchaseOrder",
            targetEntityId: po.Id,
            workflowType: WorkflowType.PurchaseOrder,
            amount: po.Total,
            decision: ApprovalDecision.Rejected,
            approverUserId: request.ApproverUsername,
            approverUsername: request.ApproverUsername,
            approverRoles: request.ApproverRoles.ToList(),
            creatorUserId: po.RequestedBy?.Username,
            comment: request.Comment,
            companyId: po.CompanyId);

        if (result.Outcome == ApprovalOutcome.SodViolation
            || result.Outcome == ApprovalOutcome.InsufficientRole)
        {
            return Result.Success(new RejectPoOutcome(
                PurchaseOrderId: po.Id,
                PoNumber: po.PONumber,
                Status: RejectPoStatus.Blocked,
                Message: result.ErrorMessage));
        }

        // Reject lands the PO back in Draft so the requester can revise + resubmit.
        await SyncStatusFkAsync(po, POStatus.Draft);
        po.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result.Success(new RejectPoOutcome(
            PurchaseOrderId: po.Id,
            PoNumber: po.PONumber,
            Status: RejectPoStatus.Rejected,
            Message: $"PO {po.PONumber} rejected and returned to Draft."));
    }

    public async Task<Result<PurchaseOrder>> DuplicatePoAsync(
        int sourcePurchaseOrderId,
        CancellationToken ct)
    {
        var source = await _db.PurchaseOrders
            .Include(p => p.Lines)
            .Where(p => p.Id == sourcePurchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (source == null)
        {
            return Result.Failure<PurchaseOrder>($"Purchase order {sourcePurchaseOrderId} not found or not visible.");
        }

        // Same yy-NNNNN PONumber sequence the legacy page used.
        var lastPO = await _db.PurchaseOrders
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        var nextNum = 1;
        if (lastPO != null && lastPO.PONumber.Contains("-"))
        {
            var parts = lastPO.PONumber.Split('-');
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var num))
                nextNum = num + 1;
        }
        var newPONumber = $"PO-{DateTime.Now:yy}-{nextNum:D5}";

        var draftLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", ((int)POStatus.Draft).ToString());

        var newPO = new PurchaseOrder
        {
            PONumber = newPONumber,
            POType = source.POType,
            POTypeLookupValueId = source.POTypeLookupValueId,
            Status = POStatus.Draft,
            StatusLookupValueId = draftLv?.Id,
            VendorId = source.VendorId,
            OrderDate = DateTime.Today,
            RequiredDate = source.RequiredDate,
            Currency = source.Currency,
            Notes = $"Duplicated from {source.PONumber}",
            CompanyId = source.CompanyId
        };

        _db.PurchaseOrders.Add(newPO);
        await _db.SaveChangesAsync(ct);

        foreach (var srcLine in source.Lines)
        {
            var newLine = new PurchaseOrderLine
            {
                PurchaseOrderId = newPO.Id,
                LineNumber = srcLine.LineNumber,
                ItemId = srcLine.ItemId,
                IsNonItemMaster = srcLine.IsNonItemMaster,
                Description = srcLine.Description,
                PartNumber = srcLine.PartNumber,
                ManufacturerPartNumber = srcLine.ManufacturerPartNumber,
                VendorPartNumber = srcLine.VendorPartNumber,
                Revision = srcLine.Revision,
                UOM = srcLine.UOM,
                QuantityOrdered = srcLine.QuantityOrdered,
                UnitPrice = srcLine.UnitPrice,
                LineTotal = srcLine.LineTotal,
                GlAccountId = srcLine.GlAccountId,
                CostCenterId = srcLine.CostCenterId
            };
            _db.PurchaseOrderLines.Add(newLine);
        }

        newPO.Subtotal = source.Subtotal;
        newPO.Total = source.Total;
        await _db.SaveChangesAsync(ct);

        return Result.Success(newPO);
    }

    public async Task<Result<bool>> DeletePoAsync(
        int purchaseOrderId,
        CancellationToken ct)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.Lines)
                .ThenInclude(l => l.Releases)
            .Where(p => p.Id == purchaseOrderId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (po == null)
        {
            return Result.Failure<bool>($"Purchase order {purchaseOrderId} not found or not visible.");
        }

        if (po.Status != POStatus.Draft)
        {
            // Legacy behavior: silently no-op (return false) instead of failing.
            return Result.Success(false);
        }

        foreach (var line in po.Lines)
        {
            _db.PurchaseOrderReleases.RemoveRange(line.Releases);
        }
        _db.PurchaseOrderLines.RemoveRange(po.Lines);
        _db.PurchaseOrders.Remove(po);
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    // === Private helper moved from PageModel ===

    private async Task SyncStatusFkAsync(PurchaseOrder po, POStatus status)
    {
        po.Status = status;
        var lv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", ((int)status).ToString());
        if (lv != null)
            po.StatusLookupValueId = lv.Id;
    }
}
