// Sprint 15.3 PR-10 — admin probe for IPurchasingControlCenterService.
//
// Exercises the full surface: KPI band, all 13 queue types, exception lane,
// lifecycle state read, lifecycle transition write.
//
// Per feedback_lock16_corollary_probes_exercise_writes.md every admin probe
// must ship INSERT/UPDATE buttons (read-only probes hid the xmin bug). This
// page ships 8 write buttons: refresh-KPI (no-op write but exercises auth),
// load-queue (13-way dispatch), assign, start-work, send-to-vendor,
// mark-resolved, close, block, unblock, cancel, reopen — 11 transitions
// behind 8 buttons.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe. AppDbContext used for tenant-scoped reads of " +
    "ProductionSupplyDemand. All writes flow through IPurchasingControlCenterService.")]
public sealed class PurchasingControlCenterProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPurchasingControlCenterService _svc;
    private readonly ILogger<PurchasingControlCenterProbeModel> _log;

    public PurchasingControlCenterProbeModel(
        AppDbContext db,
        ITenantContext tenant,
        IPurchasingControlCenterService svc,
        ILogger<PurchasingControlCenterProbeModel> log)
    {
        _db = db;
        _tenant = tenant;
        _svc = svc;
        _log = log;
    }

    // ── Bind props ──────────────────────────────────────────────────────
    [BindProperty(SupportsGet = true)] public int DemandId { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public PurchasingQueueType QueueType { get; set; } = PurchasingQueueType.SupplyDemand;
    [BindProperty(SupportsGet = true)] public int? SiteId { get; set; }
    [BindProperty(SupportsGet = true)] public string? TransitionNotes { get; set; }

    // ── View-model state ────────────────────────────────────────────────
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public PurchasingKpiBand? Kpi { get; private set; }
    public PurchasingQueuePage? Queue { get; private set; }
    public PurchasingExceptionLane? Exceptions { get; private set; }
    public PurchasingLifecycleState? Lifecycle { get; private set; }
    public SubcontractTabPage? SubcontractTab { get; private set; }
    public VendorWipTabPage? VendorWipTab { get; private set; }
    public ReceiptsTabPage? ReceiptsTab { get; private set; }
    public InspectionHoldsTabPage? InspectionHoldsTab { get; private set; }
    public PosTabPage? PosTab { get; private set; }
    public CostExceptionsTabPage? CostExceptionsTab { get; private set; }
    public PurchasingQueuePage? ExpediteQueue { get; private set; }
    public PurchasingQueuePage? ApprovalQueue { get; private set; }
    public int DemandTotalInTenant { get; private set; }
    public IReadOnlyList<ProductionSupplyDemand> RecentDemands { get; private set; } = Array.Empty<ProductionSupplyDemand>();

    public IEnumerable<PurchasingQueueType> AllQueueTypes
        => Enum.GetValues<PurchasingQueueType>();
    public IEnumerable<BuyerActionTransition> AllTransitions
        => Enum.GetValues<BuyerActionTransition>();

    private void Set(bool ok, string? msg) { OutcomeIsError = !ok; Outcome = msg; }

    // ── Handlers ────────────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadKpiAsync(CancellationToken ct)
    {
        var r = await _svc.GetKpiBandAsync(SiteId, ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Kpi = r.Value;
            Set(true,
                $"KPI band loaded. Open demand={Kpi.OpenDemandCount} (committed supply ${Kpi.CommittedSupplyValueUsd:N2}); " +
                $"open POs={Kpi.OpenPoCount} (${Kpi.OpenPoTotalValueUsd:N2}); " +
                $"vendor WIP=${Kpi.VendorWipTotalValueUsd:N2}; late POs={Kpi.LatePoCount}; " +
                $"missing supply={Kpi.MissingSupplyDemandCount}.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadQueueAsync(CancellationToken ct)
    {
        var r = await _svc.GetSupplyDemandQueueAsync(
            QueueType,
            new PurchasingQueueFilter(SiteId: SiteId, Take: 25),
            ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Queue = r.Value;
            Set(true,
                $"Queue {QueueType}: {Queue.TotalCount} matching demand(s); showing {Queue.Rows.Count}.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadExceptionsAsync(CancellationToken ct)
    {
        var r = await _svc.GetExceptionLaneAsync(new PurchasingQueueFilter(Take: 50), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Exceptions = r.Value;
            Set(true, $"Exception lane loaded. {Exceptions.TotalCount} exception(s) surfaced.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadLifecycleAsync(CancellationToken ct)
    {
        var r = await _svc.GetLifecycleStateAsync(DemandId, ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Lifecycle = r.Value;
            Set(true,
                $"Lifecycle for demand #{DemandId}: state={Lifecycle.StateLabel}; " +
                $"{Lifecycle.AllowedTransitions.Count} transition(s) allowed; " +
                $"hint: {Lifecycle.NextActionHint}");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public Task<IActionResult> OnPostAssignAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.Assign, ct);
    public Task<IActionResult> OnPostStartWorkAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.StartWork, ct);
    public Task<IActionResult> OnPostSendToVendorAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.SendToVendor, ct);
    public Task<IActionResult> OnPostRequestApprovalAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.RequestApproval, ct);
    public Task<IActionResult> OnPostApprovalGrantedAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.ApprovalGranted, ct);
    public Task<IActionResult> OnPostApprovalDeniedAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.ApprovalDenied, ct);
    public Task<IActionResult> OnPostMarkResolvedAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.MarkResolved, ct);
    public Task<IActionResult> OnPostCloseAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.Close, ct);
    public Task<IActionResult> OnPostBlockAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.Block, ct);
    public Task<IActionResult> OnPostUnblockAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.Unblock, ct);
    public Task<IActionResult> OnPostCancelAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.Cancel, ct);
    public Task<IActionResult> OnPostReopenAsync(CancellationToken ct)
        => RunTransitionAsync(BuyerActionTransition.Reopen, ct);

    // ── PR-12 tab probes ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostLoadSubcontractTabAsync(CancellationToken ct)
    {
        var r = await _svc.GetSubcontractTabAsync(
            new PurchasingQueueFilter(SiteId: SiteId, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            SubcontractTab = r.Value;
            var first = SubcontractTab.Rows.FirstOrDefault();
            Set(true,
                $"Subcontract tab: {SubcontractTab.TotalCount} active op(s); showing {SubcontractTab.Rows.Count}. " +
                (first is null
                    ? "No active subcontract operations in scope."
                    : $"Top row: op #{first.SubcontractOperationId} ({first.OperationCode}) " +
                      $"status={first.OpStatus}, supplier={first.SupplierName ?? "?"} " +
                      $"days late={first.DaysLateBack?.ToString() ?? "-"}."));
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadVendorWipTabAsync(CancellationToken ct)
    {
        var r = await _svc.GetVendorWipTabAsync(
            new PurchasingQueueFilter(SiteId: SiteId, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            VendorWipTab = r.Value;
            Set(true,
                $"Vendor WIP tab: {VendorWipTab.TotalCount} balance(s) at supplier; " +
                $"${VendorWipTab.TotalValueAtVendorUsd:N2} total value; " +
                $"{VendorWipTab.OverdueReturnCount} overdue return(s); " +
                $"showing {VendorWipTab.Rows.Count}.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadReceiptsTabAsync(CancellationToken ct)
    {
        var r = await _svc.GetReceiptsTabAsync(
            new PurchasingQueueFilter(SiteId: SiteId, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            ReceiptsTab = r.Value;
            Set(true,
                $"Receipts tab: {ReceiptsTab.TotalCount} active receipt(s); " +
                $"{ReceiptsTab.OpenDraftCount} draft(s); " +
                $"{ReceiptsTab.PendingApprovalCount} pending approval; " +
                $"showing {ReceiptsTab.Rows.Count}.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadInspectionHoldsTabAsync(CancellationToken ct)
    {
        var r = await _svc.GetInspectionHoldsTabAsync(
            new PurchasingQueueFilter(SiteId: SiteId, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            InspectionHoldsTab = r.Value;
            Set(true,
                $"Inspection Holds tab: {InspectionHoldsTab.TotalCount} hold(s); " +
                $"{InspectionHoldsTab.OldHoldsCount} aged 7+ days; " +
                $"showing {InspectionHoldsTab.Rows.Count}.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    // ── PR-13 tab probes ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostLoadPosTabAsync(CancellationToken ct)
    {
        var r = await _svc.GetPosTabAsync(
            new PurchasingQueueFilter(SiteId: SiteId, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            PosTab = r.Value;
            var first = PosTab.Rows.FirstOrDefault();
            Set(true,
                $"POs tab: {PosTab.TotalCount} active; " +
                $"{PosTab.OpenTotalValue:N2} open value (currency-agnostic); " +
                $"{PosTab.PendingApprovalCount} pending approval; " +
                $"{PosTab.LateCount} late; showing {PosTab.Rows.Count}. " +
                (first is null
                    ? "(empty)"
                    : $"Top: {first.PoNumber} status={first.Status} vendor={first.VendorName ?? "?"} total=${first.Total:N2}."));
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadCostExceptionsTabAsync(CancellationToken ct)
    {
        var r = await _svc.GetCostExceptionsTabAsync(
            new PurchasingQueueFilter(SiteId: SiteId, Take: 50), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            CostExceptionsTab = r.Value;
            Set(true,
                $"Cost Exceptions tab: {CostExceptionsTab.TotalCount} total; " +
                $"{CostExceptionsTab.HighSeverityCount} high, " +
                $"{CostExceptionsTab.MediumSeverityCount} medium, " +
                $"{CostExceptionsTab.LowSeverityCount} low; showing {CostExceptionsTab.Rows.Count}.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadExpediteQueueAsync(CancellationToken ct)
    {
        var r = await _svc.GetSupplyDemandQueueAsync(
            PurchasingQueueType.ExpediteRequired,
            new PurchasingQueueFilter(SiteId: SiteId, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            ExpediteQueue = r.Value;
            Set(true, $"Expedite queue: {ExpediteQueue.TotalCount} demand row(s); showing {ExpediteQueue.Rows.Count}.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadApprovalQueueAsync(CancellationToken ct)
    {
        var r = await _svc.GetSupplyDemandQueueAsync(
            PurchasingQueueType.ApprovalRequired,
            new PurchasingQueueFilter(SiteId: SiteId, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            ApprovalQueue = r.Value;
            Set(true, $"Approval queue: {ApprovalQueue.TotalCount} demand row(s); showing {ApprovalQueue.Rows.Count}.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    private async Task<IActionResult> RunTransitionAsync(BuyerActionTransition action, CancellationToken ct)
    {
        var req = new TransitionLifecycleRequest(
            DemandId: DemandId,
            Action: action,
            Notes: TransitionNotes,
            UserId: null,
            UserName: User?.Identity?.Name ?? "Admin");
        var r = await _svc.TransitionLifecycleAsync(req, ct);
        if (r.IsSuccess && r.Value is not null)
        {
            var v = r.Value;
            Set(true,
                $"Transition {action} on demand #{v.DemandId}: " +
                $"{v.PreviousState} → {v.NewState} at {v.TransitionedAtUtc:O}.");
        }
        else
        {
            Set(false, r.Error);
        }
        await LoadCommonAsync(ct);
        return Page();
    }

    private async Task LoadCommonAsync(CancellationToken ct)
    {
        DemandTotalInTenant = await _db.Set<ProductionSupplyDemand>()
            .Where(d => _tenant.VisibleCompanyIds.Contains(d.CompanyId))
            .CountAsync(ct);
        RecentDemands = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Where(d => _tenant.VisibleCompanyIds.Contains(d.CompanyId))
            .OrderByDescending(d => d.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
    }
}
