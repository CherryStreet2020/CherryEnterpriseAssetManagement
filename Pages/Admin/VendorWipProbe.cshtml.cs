using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 15.1 PR-5 — admin probe for IVendorWipService.
// 8 write/action buttons.
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for read-only count/list queries. All writes flow through IVendorWipService.")]
public sealed class VendorWipProbeModel : PageModel
{
    private readonly IVendorWipService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<VendorWipProbeModel> _logger;

    public VendorWipProbeModel(
        IVendorWipService svc,
        AppDbContext db,
        ILogger<VendorWipProbeModel> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    [BindProperty] public int RegLocSupplierId { get; set; } = 1;
    [BindProperty] public string RegLocCode { get; set; } = "MAIN-PLANT";
    [BindProperty] public string? RegLocAddress { get; set; } = "123 Vendor Way, Cleveland OH";
    [BindProperty] public bool RegLocWipAllowed { get; set; } = true;
    [BindProperty] public int RegLocTransitDays { get; set; } = 3;

    [BindProperty] public int ShipProId { get; set; } = 10;
    [BindProperty] public int ShipOpSeq { get; set; } = 40;
    [BindProperty] public int ShipSupplierId { get; set; } = 1;
    [BindProperty] public int? ShipVendorLocationId { get; set; }
    [BindProperty] public string? ShipPartNumber { get; set; } = "BRG-6207-2RS";
    [BindProperty] public string? ShipRevision { get; set; } = "B";
    [BindProperty] public string? ShipLotNumber { get; set; } = "LOT-2026-A";
    [BindProperty] public decimal ShipQty { get; set; } = 100m;
    [BindProperty] public decimal ShipUnitValue { get; set; } = 18.90m;
    [BindProperty] public int? ShipSubcontractOpId { get; set; }

    [BindProperty] public int RecvBalanceId { get; set; } = 1;
    [BindProperty] public decimal RecvQty { get; set; } = 100m;
    [BindProperty] public decimal RecvAccepted { get; set; } = 95m;
    [BindProperty] public decimal RecvRejected { get; set; } = 5m;

    [BindProperty] public int ScrapBalanceId { get; set; } = 1;
    [BindProperty] public decimal ScrapQty { get; set; } = 2m;
    [BindProperty] public string? ScrapReason { get; set; } = "Process error at vendor";

    [BindProperty] public int LoadBalanceId { get; set; } = 1;
    [BindProperty] public int LoadByProId { get; set; } = 10;
    [BindProperty] public int LoadBySupplierId { get; set; } = 1;
    [BindProperty] public int LoadTxnsBalanceId { get; set; } = 1;

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalLocations { get; private set; }
    public int TotalBalances { get; private set; }
    public int BalancesAtVendor { get; private set; }
    public int BalancesReceivedBack { get; private set; }
    public int TotalTransactions { get; private set; }
    public decimal TotalValueAtVendor { get; private set; }

    public VendorWipBalanceSummary? LoadedBalance { get; private set; }
    public IReadOnlyList<VendorWipBalance> LoadedBalances { get; private set; } = Array.Empty<VendorWipBalance>();
    public IReadOnlyList<VendorWipTransaction> LoadedTransactions { get; private set; } = Array.Empty<VendorWipTransaction>();

    private void Set(bool ok, string? msg) { OutcomeIsError = !ok; Outcome = msg; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalLocations = await _db.Set<VendorLocation>().CountAsync(ct);
        TotalBalances = await _db.Set<VendorWipBalance>().CountAsync(ct);
        BalancesAtVendor = await _db.Set<VendorWipBalance>().CountAsync(b => b.QuantityAtVendor > 0m, ct);
        BalancesReceivedBack = await _db.Set<VendorWipBalance>().CountAsync(b => b.InventoryStatus == VendorWipInventoryStatus.ReceivedBack, ct);
        TotalTransactions = await _db.Set<VendorWipTransaction>().CountAsync(ct);
        TotalValueAtVendor = await _db.Set<VendorWipBalance>().SumAsync(b => b.TotalValueAtVendor, ct);
    }

    public async Task<IActionResult> OnPostRegisterLocationAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RegisterVendorLocationAsync(
            new RegisterVendorLocationRequest(
                RegLocSupplierId, RegLocCode, null, RegLocAddress,
                null, null, VendorLocationType.ProcessingPlant,
                VendorManaged: true,
                CustomerOwnedMaterialAllowed: false,
                ConsignedMaterialAllowed: false,
                WipAllowed: RegLocWipAllowed,
                InspectionRequiredOnReturn: true,
                DefaultShippingMethod: "Ground",
                DefaultTransitDays: RegLocTransitDays,
                DefaultReceivingLocationId: null,
                DefaultReturnToOperationSequence: null,
                Notes: "Admin probe vendor location",
                CreatedBy: by),
            ct);
        Set(r.IsSuccess, r.IsSuccess ? $"Registered VendorLocation #{r.Value} for supplier {RegLocSupplierId} code '{RegLocCode}'." : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostShipAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ShipToVendorAsync(
            new ShipToVendorRequest(
                ShipProId, ShipOpSeq, ShipSupplierId, ShipVendorLocationId,
                ItemId: null, PartNumber: ShipPartNumber, Revision: ShipRevision,
                LotNumber: ShipLotNumber, SerialNumber: null,
                Quantity: ShipQty, UnitValue: ShipUnitValue, Uom: "EA",
                ShipmentDocument: $"BOL-{DateTime.UtcNow:yyyyMMddHHmmss}",
                FromLocationDescription: "Internal WIP Op " + (ShipOpSeq - 10).ToString("000"),
                ToLocationDescription: "Vendor processing plant",
                SubcontractOperationId: ShipSubcontractOpId,
                RequiredReturnDate: DateTime.UtcNow.AddDays(7),
                Notes: "Admin probe ship-to-vendor",
                CreatedBy: by),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Ship-to-vendor: Txn #{r.Value!.TransactionId} on Balance #{r.Value.VendorWipBalanceId}. At vendor: {r.Value.QuantityAtVendor:N4}. Status={r.Value.InventoryStatus}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReceiveAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReceiveFromVendorAsync(
            new ReceiveFromVendorRequest(
                RecvBalanceId, RecvQty, RecvAccepted, RecvRejected,
                ReceiptDocument: $"GRN-VENDOR-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Notes: "Admin probe receive-from-vendor",
                CreatedBy: by),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Receive: Txn #{r.Value!.TransactionId}. At vendor remaining: {r.Value.QuantityAtVendor:N4}. Status={r.Value.InventoryStatus}. {r.Value.Message}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostScrapAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RecordScrapAtVendorAsync(
            new RecordScrapAtVendorRequest(
                ScrapBalanceId, ScrapQty, ScrapReason,
                Notes: "Admin probe scrap-at-vendor",
                CreatedBy: by),
            ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Scrap: Txn #{r.Value!.TransactionId}. At vendor remaining: {r.Value.QuantityAtVendor:N4}. Status={r.Value.InventoryStatus}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadBalanceAsync(CancellationToken ct)
    {
        var r = await _svc.GetBalanceAsync(LoadBalanceId, ct);
        if (r.IsSuccess) LoadedBalance = r.Value;
        Set(r.IsSuccess, r.IsSuccess ? $"Loaded balance #{r.Value!.BalanceId}" : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadByProAsync(CancellationToken ct)
    {
        LoadedBalances = await _svc.GetBalancesForProAsync(LoadByProId, ct);
        Set(true, $"Loaded {LoadedBalances.Count} balances for PRO {LoadByProId}");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadBySupplierAsync(CancellationToken ct)
    {
        LoadedBalances = await _svc.GetBalancesForSupplierAsync(LoadBySupplierId, ct);
        Set(true, $"Loaded {LoadedBalances.Count} balances for supplier {LoadBySupplierId}");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadTransactionsAsync(CancellationToken ct)
    {
        LoadedTransactions = await _svc.GetTransactionsForBalanceAsync(LoadTxnsBalanceId, ct);
        Set(true, $"Loaded {LoadedTransactions.Count} transactions for balance #{LoadTxnsBalanceId}");
        await LoadStatsAsync(ct);
        return Page();
    }
}
