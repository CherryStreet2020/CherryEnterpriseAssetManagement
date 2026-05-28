// Sprint 15.2 PR-6 — admin probe for ISubcontractShipmentReceiptService.
// 13 write/action buttons per Lock 16 corollary (every service method exercised).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count queries. All writes flow through ISubcontractShipmentReceiptService.")]
public sealed class SubcontractShipmentReceiptProbeModel : PageModel
{
    private readonly ISubcontractShipmentReceiptService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<SubcontractShipmentReceiptProbeModel> _logger;

    public SubcontractShipmentReceiptProbeModel(
        ISubcontractShipmentReceiptService svc,
        AppDbContext db,
        ILogger<SubcontractShipmentReceiptProbeModel> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    // ── Create Shipment ──
    [BindProperty] public int CreateShipmentOpId { get; set; } = 1;
    [BindProperty] public int CreateShipmentSupplierId { get; set; } = 1;
    [BindProperty] public string? CreateShipmentCarrier { get; set; } = "FedEx";
    [BindProperty] public DateTime? CreateShipmentRequiredDate { get; set; } = DateTime.UtcNow.AddDays(2);

    // ── Add Shipment Line ──
    [BindProperty] public int AddShipLineShipmentId { get; set; } = 1;
    [BindProperty] public int AddShipLineItemId { get; set; } = 1;
    [BindProperty] public string? AddShipLinePartNumber { get; set; } = "BRACKET-001";
    [BindProperty] public string? AddShipLineLotNumber { get; set; } = "LOT-2026-001";
    [BindProperty] public decimal AddShipLineQty { get; set; } = 100m;
    [BindProperty] public decimal? AddShipLineUnitCost { get; set; } = 12.50m;

    // ── Status transitions ──
    [BindProperty] public int PickedShipmentId { get; set; } = 1;
    [BindProperty] public int InTransitShipmentId { get; set; } = 1;
    [BindProperty] public int DeliveredShipmentId { get; set; } = 1;
    [BindProperty] public int CancelShipmentId { get; set; } = 1;
    [BindProperty] public string? CancelShipmentReason { get; set; } = "Vendor closed unexpectedly";

    // ── Receipt reversal ──
    [BindProperty] public int ReverseReceiptId { get; set; } = 1;
    [BindProperty] public string? ReverseReceiptReason { get; set; } = "Wrong PO referenced";

    // ── Create Receipt ──
    [BindProperty] public int CreateReceiptOpId { get; set; } = 1;
    [BindProperty] public int CreateReceiptSupplierId { get; set; } = 1;
    [BindProperty] public string? CreateReceiptPackingSlip { get; set; } = "VENDOR-PACK-001";
    [BindProperty] public int? CreateReceiptShipmentId { get; set; }

    // ── Add Receipt Line ──
    [BindProperty] public int AddRecvLineReceiptId { get; set; } = 1;
    [BindProperty] public int AddRecvLineItemId { get; set; } = 1;
    [BindProperty] public string? AddRecvLinePartNumber { get; set; } = "BRACKET-001";
    [BindProperty] public decimal AddRecvLineReceived { get; set; } = 100m;
    [BindProperty] public decimal AddRecvLineAccepted { get; set; } = 100m;
    [BindProperty] public decimal AddRecvLineRejected { get; set; } = 0m;
    [BindProperty] public decimal AddRecvLineScrapped { get; set; } = 0m;
    [BindProperty] public decimal AddRecvLineShort { get; set; } = 0m;
    [BindProperty] public SubcontractReceiptScenario AddRecvLineScenario { get; set; } = SubcontractReceiptScenario.FullGoodReceipt;
    [BindProperty] public SubcontractReceiptDisposition AddRecvLineDisposition { get; set; } = SubcontractReceiptDisposition.ReleaseToNextOp;

    // ── Post Receipt ──
    [BindProperty] public int PostReceiptId { get; set; } = 1;

    // ── Approve Receipt ──
    [BindProperty] public int ApproveReceiptId { get; set; } = 1;

    // ── Loads ──
    [BindProperty] public int LoadShipmentsOpId { get; set; } = 1;
    [BindProperty] public int LoadReceiptsOpId { get; set; } = 1;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalShipments { get; private set; }
    public int ShipmentsInTransit { get; private set; }
    public int ShipmentsDelivered { get; private set; }
    public int TotalReceipts { get; private set; }
    public int ReceiptsPosted { get; private set; }
    public int ReceiptsPendingApproval { get; private set; }
    public int TotalShipmentLines { get; private set; }
    public int TotalReceiptLines { get; private set; }

    public IReadOnlyList<SubcontractShipment> LoadedShipments { get; private set; } = Array.Empty<SubcontractShipment>();
    public IReadOnlyList<SubcontractReceipt> LoadedReceipts { get; private set; } = Array.Empty<SubcontractReceipt>();

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalShipments = await _db.Set<SubcontractShipment>().CountAsync(ct);
        ShipmentsInTransit = await _db.Set<SubcontractShipment>()
            .CountAsync(s => s.Status == SubcontractShipmentLifecycle.InTransit, ct);
        ShipmentsDelivered = await _db.Set<SubcontractShipment>()
            .CountAsync(s => s.Status == SubcontractShipmentLifecycle.DeliveredToVendor ||
                              s.Status == SubcontractShipmentLifecycle.Reconciled, ct);
        TotalReceipts = await _db.Set<SubcontractReceipt>().CountAsync(ct);
        ReceiptsPosted = await _db.Set<SubcontractReceipt>()
            .CountAsync(r => r.Status == SubcontractReceiptLifecycle.Posted ||
                              r.Status == SubcontractReceiptLifecycle.Approved, ct);
        ReceiptsPendingApproval = await _db.Set<SubcontractReceipt>()
            .CountAsync(r => r.Status == SubcontractReceiptLifecycle.PendingApproval, ct);
        TotalShipmentLines = await _db.Set<SubcontractShipmentLine>().CountAsync(ct);
        TotalReceiptLines = await _db.Set<SubcontractReceiptLine>().CountAsync(ct);
    }

    // 1) CREATE SHIPMENT
    public async Task<IActionResult> OnPostCreateShipmentAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CreateShipmentAsync(new CreateSubcontractShipmentRequest(
            SubcontractOperationId: CreateShipmentOpId,
            SubcontractDemandId: null,
            SupplierId: CreateShipmentSupplierId,
            VendorLocationId: null,
            ShipFromLocationId: null,
            VendorWipLocationCode: "VENDOR-WIP-A1",
            Carrier: CreateShipmentCarrier,
            ShippingMethod: "Ground",
            TrackingNumber: null,
            FreightCost: 45.00m,
            FreightCurrency: "USD",
            RequiredShipDate: CreateShipmentRequiredDate,
            ExpectedDeliveryDate: CreateShipmentRequiredDate?.AddDays(3),
            CertRequired: false,
            PackingInstructions: "Crate per AS9100 §8.5.6 — admin probe",
            CreatedBy: by,
            Notes: "Admin probe — outbound WIP shipment"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created shipment #{r.Value!.SubcontractShipmentId} ({r.Value.ShipmentNumber}). {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) ADD SHIPMENT LINE
    public async Task<IActionResult> OnPostAddShipLineAsync(CancellationToken ct)
    {
        var r = await _svc.AddShipmentLineAsync(new AddShipmentLineRequest(
            SubcontractShipmentId: AddShipLineShipmentId,
            ItemId: AddShipLineItemId,
            PartNumber: AddShipLinePartNumber,
            Description: "Outbound WIP — admin probe",
            DrawingRevision: "RevA",
            LotNumber: AddShipLineLotNumber,
            SerialNumber: null,
            QuantityShipped: AddShipLineQty,
            Uom: "EA",
            UnitCostSnapshot: AddShipLineUnitCost,
            Notes: "Admin probe line"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Added line {r.Value!.LineNumber} (id #{r.Value.SubcontractShipmentLineId}) to shipment #{AddShipLineShipmentId}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3a) MARK PICKED (locks lines, pick complete)
    public async Task<IActionResult> OnPostMarkPickedAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.MarkShipmentPickedAsync(PickedShipmentId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Shipment #{PickedShipmentId} → Picked. Lines={r.Value!.LineCount}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3b) MARK IN-TRANSIT (ship to vendor — triggers VendorWipTransaction.ShipToVendor)
    public async Task<IActionResult> OnPostMarkInTransitAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.MarkShipmentInTransitAsync(InTransitShipmentId, DateTime.UtcNow, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Shipment #{InTransitShipmentId} → InTransit. Lines={r.Value!.LineCount}, qty={r.Value.TotalQuantityShipped:N4}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) MARK DELIVERED
    public async Task<IActionResult> OnPostMarkDeliveredAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.MarkShipmentDeliveredAsync(DeliveredShipmentId, DateTime.UtcNow, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Shipment #{DeliveredShipmentId} → DeliveredToVendor."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4b) CANCEL SHIPMENT (Draft/Picked/Staged/InTransit only — refuses Delivered/Reconciled)
    public async Task<IActionResult> OnPostCancelShipmentAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CancelShipmentAsync(CancelShipmentId, CancelShipmentReason, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Shipment #{CancelShipmentId} → Cancelled. Reason: {CancelShipmentReason}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5) CREATE RECEIPT
    public async Task<IActionResult> OnPostCreateReceiptAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CreateReceiptAsync(new CreateSubcontractReceiptRequest(
            SubcontractOperationId: CreateReceiptOpId,
            SubcontractShipmentId: CreateReceiptShipmentId,
            SupplierId: CreateReceiptSupplierId,
            VendorLocationId: null,
            ReceivingLocationId: null,
            VendorPackingSlip: CreateReceiptPackingSlip,
            Carrier: "FedEx",
            TrackingNumber: null,
            ReceiptDate: DateTime.UtcNow,
            CertReceived: true,
            CertReference: "CERT-2026-001",
            InspectionRequired: false,
            CreatedBy: by,
            Notes: "Admin probe — return receipt"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created receipt #{r.Value!.SubcontractReceiptId} ({r.Value.ReceiptNumber}). {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 6) ADD RECEIPT LINE (with scenario + disposition)
    public async Task<IActionResult> OnPostAddRecvLineAsync(CancellationToken ct)
    {
        var r = await _svc.AddReceiptLineAsync(new AddReceiptLineRequest(
            SubcontractReceiptId: AddRecvLineReceiptId,
            SubcontractShipmentLineId: null,
            ItemId: AddRecvLineItemId,
            PartNumber: AddRecvLinePartNumber,
            Description: "Return WIP — admin probe",
            DrawingRevision: "RevA",
            LotNumber: "LOT-2026-001",
            SerialNumber: null,
            QuantityReceived: AddRecvLineReceived,
            QuantityAccepted: AddRecvLineAccepted,
            QuantityRejected: AddRecvLineRejected,
            QuantityScrappedAtVendor: AddRecvLineScrapped,
            QuantityShort: AddRecvLineShort,
            Uom: "EA",
            Scenario: AddRecvLineScenario,
            Disposition: AddRecvLineDisposition,
            RejectReason: AddRecvLineRejected > 0m ? "OutOfTolerance" : null,
            NcrReference: AddRecvLineRejected > 0m ? "NCR-2026-001" : null,
            Notes: "Admin probe line"), ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Added line {r.Value!.LineNumber} (id #{r.Value.SubcontractReceiptLineId}) — scenario {r.Value.Scenario}, disposition {r.Value.Disposition}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 7) POST RECEIPT (atomically updates vendor WIP balance + rolls into subcontract op)
    public async Task<IActionResult> OnPostPostReceiptAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.PostReceiptAsync(PostReceiptId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Posted receipt #{PostReceiptId}. Status={r.Value!.Status}. Lines={r.Value.LinesPosted}, accepted={r.Value.TotalAccepted:N4}, rejected={r.Value.TotalRejected:N4}, scrapped={r.Value.TotalScrapped:N4}. RequiresApproval={r.Value.RequiresApproval}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 8) APPROVE RECEIPT (clear PendingApproval gate)
    public async Task<IActionResult> OnPostApproveReceiptAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ApproveReceiptAsync(ApproveReceiptId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Approved receipt #{ApproveReceiptId}. Status={r.Value!.Status} by {r.Value.ApprovedBy}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 8b) REVERSE RECEIPT (PendingApproval only in PR-6 — Posted/Approved reversal lands in PR-8)
    public async Task<IActionResult> OnPostReverseReceiptAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReverseReceiptAsync(ReverseReceiptId, ReverseReceiptReason, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Reversed receipt #{ReverseReceiptId}. Reason: {ReverseReceiptReason}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 9) LOAD SHIPMENTS for an op
    public async Task<IActionResult> OnPostLoadShipmentsAsync(CancellationToken ct)
    {
        LoadedShipments = await _svc.GetShipmentsForOpAsync(LoadShipmentsOpId, ct);
        Set(true, $"Loaded {LoadedShipments.Count} shipments for op #{LoadShipmentsOpId}");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 10) LOAD RECEIPTS for an op (extra read)
    public async Task<IActionResult> OnPostLoadReceiptsAsync(CancellationToken ct)
    {
        LoadedReceipts = await _svc.GetReceiptsForOpAsync(LoadReceiptsOpId, ct);
        Set(true, $"Loaded {LoadedReceipts.Count} receipts for op #{LoadReceiptsOpId}");
        await LoadStatsAsync(ct);
        return Page();
    }
}
