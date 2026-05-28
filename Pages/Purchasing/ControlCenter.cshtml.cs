// Sprint 15.3 PR-11 — /Purchasing/ControlCenter page (Wave 3 page surface opens).
//
// First user-facing render of the §21 Purchasing Command Center. Composes
// shared Cockpit primitives (_CockpitPageHeader + _CockpitKpiBand + _CockpitTabShell)
// over IPurchasingControlCenterService (PR-10).
//
// This PR ships the page shell + the first 2 of 13 §21 tabs:
//   * Supply Demand (§21 tab 1) — all unresolved demand across PROs/projects/MRP
//   * Buy-to-Job   (§21 tab 2) — job-specific BOM demand with no PO
//
// The remaining 11 tabs ship in PR-12 (Subcontract / Vendor WIP / Receipts /
// Inspection Holds) and PR-13 (POs / Expedites / Approvals / Cost Exceptions).
// Their tab buttons are registered with a "(soon)" badge so the §21 IA is
// visible from day one but inactive tabs gracefully fall back to the empty
// state.
//
// Row actions from §7 ship as inline buttons against each row — they POST to
// page handlers that delegate to the real services already in the codebase
// (IPurchasingService for PO creation, ISubcontractOperationService for
// subcontract demand creation, etc.). For PR-11 the buttons currently link to
// the existing detail pages where those actions already exist; PR-14 wires
// the Auto-PO Creation rules and PR-15 wires the Buyer Recommendation Engine.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;             // VendorWipInventoryStatus etc.
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;
using Abs.FixedAssets.Services.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Purchasing;

[Authorize]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Read-only Control Center surface. All data access flows through " +
    "IPurchasingControlCenterService which enforces tenant scoping. No " +
    "AppDbContext usage in this page.")]
public sealed class ControlCenterModel : PageModel
{
    private readonly IPurchasingControlCenterService _svc;
    private readonly ILogger<ControlCenterModel> _logger;

    public ControlCenterModel(
        IPurchasingControlCenterService svc,
        ILogger<ControlCenterModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // ── Query binds ─────────────────────────────────────────────────────

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabKey { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SiteId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? BuyerUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Skip { get; set; }

    public const int PageSize = 50;

    // ── Tab constants — every §21 tab pre-registered ────────────────────

    public const string TabSupplyDemand = "supply-demand";
    public const string TabBuyToJob = "buy-to-job";
    public const string TabSubcontract = "subcontract";          // PR-12
    public const string TabVendorWip = "vendor-wip";             // PR-12
    public const string TabReceipts = "receipts";                // PR-12
    public const string TabInspectionHolds = "inspection-holds"; // PR-12
    public const string TabPos = "pos";                          // PR-13
    public const string TabExpedites = "expedites";              // PR-13
    public const string TabApprovals = "approvals";              // PR-13
    public const string TabCostExceptions = "cost-exceptions";   // PR-13

    // PR-12 ships 4 more live tabs: Subcontract / Vendor WIP / Receipts /
    // Inspection Holds. PR-13 ships the final 4: POs / Expedites / Approvals /
    // Cost Exceptions — completing the §21 10-tab IA.
    private static readonly string[] LiveTabs = {
        TabSupplyDemand, TabBuyToJob,
        TabSubcontract, TabVendorWip, TabReceipts, TabInspectionHolds,
        TabPos, TabExpedites, TabApprovals, TabCostExceptions,
    };
    private static readonly string[] AllTabs = {
        TabSupplyDemand, TabBuyToJob, TabSubcontract, TabVendorWip,
        TabReceipts, TabInspectionHolds, TabPos, TabExpedites, TabApprovals,
        TabCostExceptions,
    };

    public string ActiveTab =>
        !string.IsNullOrEmpty(TabKey) && AllTabs.Contains(TabKey, StringComparer.OrdinalIgnoreCase)
            ? TabKey.ToLowerInvariant()
            : TabSupplyDemand;

    public bool ActiveTabIsLive => LiveTabs.Contains(ActiveTab, StringComparer.OrdinalIgnoreCase);

    // ── View-model state ────────────────────────────────────────────────

    public CockpitPageHeaderViewModel? PageHeader { get; private set; }
    public CockpitKpiBandViewModel? KpiBand { get; private set; }
    public CockpitTabShellModel TabShell { get; private set; } = new();
    public PurchasingQueuePage? Queue { get; private set; }

    // PR-12 tab payloads — only the active tab is loaded.
    public SubcontractTabPage? SubcontractTab { get; private set; }
    public VendorWipTabPage? VendorWipTab { get; private set; }
    public ReceiptsTabPage? ReceiptsTab { get; private set; }
    public InspectionHoldsTabPage? InspectionHoldsTab { get; private set; }

    // PR-13 tab payloads. Expedites + Approvals reuse Queue (demand grid).
    public PosTabPage? PosTab { get; private set; }
    public CostExceptionsTabPage? CostExceptionsTab { get; private set; }

    public string? ErrorMessage { get; private set; }

    // ── Handler ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var kpiResult = await _svc.GetKpiBandAsync(SiteId, ct);
        if (kpiResult.IsSuccess && kpiResult.Value is not null)
        {
            KpiBand = BuildKpiBand(kpiResult.Value);
        }
        else
        {
            ErrorMessage = kpiResult.Error;
        }

        PageHeader = new CockpitPageHeaderViewModel
        {
            Title = "Purchasing",
            Scope = SiteId.HasValue ? $"Site #{SiteId}" : "All sites",
            Subtitle = "Unresolved supply demand across Production Orders, projects, and inventory.",
            ShowLive = true,
            RefreshedAtText = kpiResult.IsSuccess
                ? $"Snapshot {kpiResult.Value!.SnapshotUtc:HH:mm:ss} UTC"
                : null,
            ShowVoiceButton = false, // Voice wires up in Sprint 5; placeholder hidden for now.
        };

        TabShell = BuildTabShell();

        if (ActiveTabIsLive)
        {
            var filter = new PurchasingQueueFilter(
                SiteId: SiteId,
                BuyerUserId: BuyerUserId,
                Skip: Math.Max(0, Skip),
                Take: PageSize);

            // PR-11 + PR-12 + PR-13 dispatch:
            //  * Supply Demand / Buy-to-Job / Expedites / Approvals → demand-grid
            //    via GetSupplyDemandQueueAsync (they all share the same row shape;
            //    only the filter changes).
            //  * Subcontract / VendorWip / Receipts / InspectionHolds (PR-12) and
            //    POs / CostExceptions (PR-13) → tab-specific shapes.
            switch (ActiveTab)
            {
                case TabSupplyDemand:
                case TabBuyToJob:
                case TabExpedites:
                case TabApprovals:
                {
                    var queueType = ActiveTab switch
                    {
                        TabBuyToJob => PurchasingQueueType.BuyToJob,
                        TabExpedites => PurchasingQueueType.ExpediteRequired,
                        TabApprovals => PurchasingQueueType.ApprovalRequired,
                        _ => PurchasingQueueType.SupplyDemand,
                    };
                    var qResult = await _svc.GetSupplyDemandQueueAsync(queueType, filter, ct);
                    if (qResult.IsSuccess && qResult.Value is not null)
                    {
                        Queue = qResult.Value;
                    }
                    else
                    {
                        ErrorMessage = qResult.Error;
                        // P2.6 fix — hydrate empty page so the empty-state branch
                        // still renders the canvas instead of leaving a blank below
                        // the error banner.
                        Queue = new PurchasingQueuePage(queueType, 0, Array.Empty<PurchasingQueueRow>());
                    }
                    break;
                }
                case TabSubcontract:
                {
                    var r = await _svc.GetSubcontractTabAsync(filter, ct);
                    if (r.IsSuccess && r.Value is not null) SubcontractTab = r.Value;
                    else { ErrorMessage = r.Error; SubcontractTab = new SubcontractTabPage(0, Array.Empty<SubcontractTabRow>()); }
                    break;
                }
                case TabVendorWip:
                {
                    var r = await _svc.GetVendorWipTabAsync(filter, ct);
                    if (r.IsSuccess && r.Value is not null) VendorWipTab = r.Value;
                    else { ErrorMessage = r.Error; VendorWipTab = new VendorWipTabPage(0, 0m, 0, Array.Empty<VendorWipTabRow>()); }
                    break;
                }
                case TabReceipts:
                {
                    var r = await _svc.GetReceiptsTabAsync(filter, ct);
                    if (r.IsSuccess && r.Value is not null) ReceiptsTab = r.Value;
                    else { ErrorMessage = r.Error; ReceiptsTab = new ReceiptsTabPage(0, 0, 0, Array.Empty<ReceiptsTabRow>()); }
                    break;
                }
                case TabInspectionHolds:
                {
                    var r = await _svc.GetInspectionHoldsTabAsync(filter, ct);
                    if (r.IsSuccess && r.Value is not null) InspectionHoldsTab = r.Value;
                    else { ErrorMessage = r.Error; InspectionHoldsTab = new InspectionHoldsTabPage(0, 0, Array.Empty<InspectionHoldRow>()); }
                    break;
                }
                case TabPos:
                {
                    var r = await _svc.GetPosTabAsync(filter, ct);
                    if (r.IsSuccess && r.Value is not null) PosTab = r.Value;
                    else { ErrorMessage = r.Error; PosTab = new PosTabPage(0, 0m, 0, 0, Array.Empty<PosTabRow>()); }
                    break;
                }
                case TabCostExceptions:
                {
                    var r = await _svc.GetCostExceptionsTabAsync(filter, ct);
                    if (r.IsSuccess && r.Value is not null) CostExceptionsTab = r.Value;
                    else { ErrorMessage = r.Error; CostExceptionsTab = new CostExceptionsTabPage(0, 0, 0, 0, Array.Empty<PurchasingExceptionRow>()); }
                    break;
                }
            }
        }

        return Page();
    }

    // ── Pagination helpers — tab-aware Has-Next/Prev resolver ─────────────
    //
    // The supply-demand / buy-to-job tabs page through PurchasingQueuePage.
    // PR-12 tabs page through their own *TabPage records. Centralise the
    // "is there more" decision so the Razor doesn't have to switch on tab.

    public int ActiveTabTotalCount => ActiveTab switch
    {
        TabSupplyDemand or TabBuyToJob or TabExpedites or TabApprovals => Queue?.TotalCount ?? 0,
        TabSubcontract => SubcontractTab?.TotalCount ?? 0,
        TabVendorWip => VendorWipTab?.TotalCount ?? 0,
        TabReceipts => ReceiptsTab?.TotalCount ?? 0,
        TabInspectionHolds => InspectionHoldsTab?.TotalCount ?? 0,
        TabPos => PosTab?.TotalCount ?? 0,
        TabCostExceptions => CostExceptionsTab?.TotalCount ?? 0,
        _ => 0,
    };

    public int ActiveTabRowCount => ActiveTab switch
    {
        TabSupplyDemand or TabBuyToJob or TabExpedites or TabApprovals => Queue?.Rows.Count ?? 0,
        TabSubcontract => SubcontractTab?.Rows.Count ?? 0,
        TabVendorWip => VendorWipTab?.Rows.Count ?? 0,
        TabReceipts => ReceiptsTab?.Rows.Count ?? 0,
        TabInspectionHolds => InspectionHoldsTab?.Rows.Count ?? 0,
        TabPos => PosTab?.Rows.Count ?? 0,
        // Cost Exceptions: GetExceptionLaneAsync returns merged rows clipped
        // to Take, no Skip support. HasNext returns false because RowCount
        // equals TotalCount when clipped that way.
        TabCostExceptions => CostExceptionsTab?.Rows.Count ?? 0,
        _ => 0,
    };

    // ── Helpers ─────────────────────────────────────────────────────────

    private static CockpitKpiBandViewModel BuildKpiBand(PurchasingKpiBand k)
    {
        return new CockpitKpiBandViewModel
        {
            Eyebrow = "PURCHASING",
            ShowLiveIndicator = true,
            HeroMode = true,
            RefreshedAtText = $"updated {k.SnapshotUtc:HH:mm} UTC",
            Tiles = new[]
            {
                new CockpitKpiTileViewModel
                {
                    Label = "Open demand",
                    Value = k.OpenDemandCount.ToString("N0"),
                    SubText = $"committed supply ${k.CommittedSupplyValueUsd:N0}",
                    Tone = k.OpenDemandCount > 0 ? "warning" : "neutral",
                    // Codex thread #1 (P2): DrillScroll targets .cockpit__group
                    // on the current tab; since each tab only renders one
                    // group, switch to DrillHref so the tile navigates to the
                    // intended tab even if not currently active.
                    DrillHref = "?tab=" + TabSupplyDemand,
                },
                new CockpitKpiTileViewModel
                {
                    Label = "Open POs",
                    Value = k.OpenPoCount.ToString("N0"),
                    SubText = $"${k.OpenPoTotalValueUsd:N0}",
                    Tone = k.OpenPoCount > 0 ? "info" : "neutral",
                    DrillHref = "?tab=" + TabPos,
                },
                new CockpitKpiTileViewModel
                {
                    Label = "Vendor WIP",
                    Value = $"${k.VendorWipTotalValueUsd:N0}",
                    SubText = "at supplier locations",
                    Tone = k.VendorWipTotalValueUsd > 0 ? "info" : "neutral",
                    DrillHref = "?tab=" + TabVendorWip,
                },
                new CockpitKpiTileViewModel
                {
                    Label = "Late POs",
                    Value = k.LatePoCount.ToString("N0"),
                    SubText = "past required / promise date",
                    Tone = k.LatePoCount > 0 ? "danger" : "success",
                    DrillHref = "?tab=" + TabExpedites,
                },
                new CockpitKpiTileViewModel
                {
                    Label = "Missing supply",
                    Value = k.MissingSupplyDemandCount.ToString("N0"),
                    SubText = "demand with no supply record",
                    Tone = k.MissingSupplyDemandCount > 0 ? "danger" : "success",
                    // Codex thread #1 (P2) — navigate to Buy-to-Job tab
                    // rather than scroll to a group that may not be on the
                    // current tab's rendered DOM.
                    DrillHref = "?tab=" + TabBuyToJob,
                },
            },
        };
    }

    private CockpitTabShellModel BuildTabShell()
    {
        var soon = "neutral";
        return new CockpitTabShellModel
        {
            ActiveTabKey = ActiveTab,
            BaseRoute = "/Purchasing/ControlCenter",
            Tabs = new[]
            {
                new CockpitTab(TabSupplyDemand, "Supply Demand", "fas fa-clipboard-list", IsDefault: true),
                new CockpitTab(TabBuyToJob, "Buy-to-Job", "fas fa-cart-plus"),
                new CockpitTab(TabSubcontract, "Subcontract", "fas fa-handshake", CountBadge: null, BadgeTone: soon),
                new CockpitTab(TabVendorWip, "Vendor WIP", "fas fa-warehouse"),
                new CockpitTab(TabReceipts, "Receipts", "fas fa-box-open"),
                new CockpitTab(TabInspectionHolds, "Inspection Holds", "fas fa-flask"),
                new CockpitTab(TabPos, "POs", "fas fa-file-invoice"),
                new CockpitTab(TabExpedites, "Expedites", "fas fa-bolt"),
                new CockpitTab(TabApprovals, "Approvals", "fas fa-circle-check"),
                new CockpitTab(TabCostExceptions, "Cost Exceptions", "fas fa-triangle-exclamation"),
            },
        };
    }

    public bool TabIsLive(string tabKey) =>
        LiveTabs.Contains(tabKey, StringComparer.OrdinalIgnoreCase);

    // Row actions:
    //  * ViewPro link → existing /Production/Details/{id} page (verified).
    //  * StartWork POST handler → calls IPurchasingControlCenterService.
    //    TransitionLifecycleAsync(Open→Assigned or Assigned→InProgress) so
    //    the row action records buyer intent now; PR-14 (Auto-PO Creation)
    //    and PR-15 (Recommendation Engine) extend with real automated paths.
    //  * The original Create-PO / Reserve buttons were dropped pre-PR per
    //    subagent review #P1.1 — their target pages (/PurchaseOrders/Create
    //    and /Materials/Reserve) do not exist in the codebase, so the
    //    buttons would 404 on every row.
    public static string ActionLink_ViewPro(PurchasingQueueRow r)
        => $"/Production/Details/{r.ProductionOrderId}";

    [BindProperty] public int TransitionDemandId { get; set; }
    [BindProperty] public string? TransitionNotes { get; set; }

    public async Task<IActionResult> OnPostStartWorkAsync(CancellationToken ct)
    {
        var req = new TransitionLifecycleRequest(
            DemandId: TransitionDemandId,
            Action: BuyerActionTransition.StartWork,
            Notes: TransitionNotes,
            UserId: null,
            UserName: User?.Identity?.Name ?? "Admin");
        var r = await _svc.TransitionLifecycleAsync(req, ct);
        TempData["pcc-flash"] = r.IsSuccess
            ? $"Demand #{TransitionDemandId}: {r.Value!.PreviousState} → {r.Value.NewState}."
            : $"Transition failed: {r.Error}";
        return RedirectToPage(new { tab = ActiveTab, SiteId = SiteId, BuyerUserId = BuyerUserId, Skip = Skip });
    }

    public async Task<IActionResult> OnPostAssignSelfAsync(CancellationToken ct)
    {
        var req = new TransitionLifecycleRequest(
            DemandId: TransitionDemandId,
            Action: BuyerActionTransition.Assign,
            Notes: TransitionNotes,
            UserId: null,
            UserName: User?.Identity?.Name ?? "Admin");
        var r = await _svc.TransitionLifecycleAsync(req, ct);
        TempData["pcc-flash"] = r.IsSuccess
            ? $"Demand #{TransitionDemandId}: {r.Value!.PreviousState} → {r.Value.NewState}."
            : $"Transition failed: {r.Error}";
        return RedirectToPage(new { tab = ActiveTab, SiteId = SiteId, BuyerUserId = BuyerUserId, Skip = Skip });
    }

    // ── State-tone mapping for badges (subagent review #P2.7) ───────────

    public static string SupplyStatusTone(DemandSupplyStatus s) => s switch
    {
        DemandSupplyStatus.NotSupplied => "danger",
        DemandSupplyStatus.PartiallyFulfilled => "warning",
        DemandSupplyStatus.FullyFulfilled or DemandSupplyStatus.Closed => "success",
        DemandSupplyStatus.AtVendor or DemandSupplyStatus.InInspection => "info",
        DemandSupplyStatus.Cancelled => "muted",
        _ => "info",
    };

    public static string BuyerActionStateTone(BuyerActionState s) => s switch
    {
        BuyerActionState.Open => "neutral",
        BuyerActionState.Assigned or BuyerActionState.InProgress => "info",
        BuyerActionState.AwaitingVendor or BuyerActionState.AwaitingApproval => "warning",
        BuyerActionState.Resolved => "success",
        BuyerActionState.Closed or BuyerActionState.Cancelled => "muted",
        BuyerActionState.Blocked => "danger",
        _ => "info",
    };

    public static string SubcontractOpStatusTone(SubcontractOperationStatus s) => s switch
    {
        SubcontractOperationStatus.NotReady => "muted",
        SubcontractOperationStatus.ReadyToBuy
            or SubcontractOperationStatus.PoCreated
            or SubcontractOperationStatus.ReadyToShip => "info",
        SubcontractOperationStatus.ShippedToVendor
            or SubcontractOperationStatus.AtVendor
            or SubcontractOperationStatus.PartiallyReceived
            or SubcontractOperationStatus.InInspection => "warning",
        SubcontractOperationStatus.Rejected
            or SubcontractOperationStatus.ReworkAtVendor => "danger",
        SubcontractOperationStatus.Complete
            or SubcontractOperationStatus.Closed => "success",
        _ => "info",
    };

    public static string VendorWipInventoryTone(VendorWipInventoryStatus s) => s switch
    {
        VendorWipInventoryStatus.InTransitToVendor
            or VendorWipInventoryStatus.InTransitFromVendor => "info",
        VendorWipInventoryStatus.AtVendorAvailable
            or VendorWipInventoryStatus.AtVendorAssignedToJob
            or VendorWipInventoryStatus.AtVendorInProcess
            or VendorWipInventoryStatus.AtVendorAwaitingReturn => "warning",
        VendorWipInventoryStatus.AtVendorOnHold => "warning",
        VendorWipInventoryStatus.AtVendorRejected
            or VendorWipInventoryStatus.AtVendorScrap => "danger",
        VendorWipInventoryStatus.ReceivedBack
            or VendorWipInventoryStatus.Closed => "success",
        _ => "info",
    };

    public static string ReceiptLifecycleTone(SubcontractReceiptLifecycle s) => s switch
    {
        SubcontractReceiptLifecycle.Draft => "neutral",
        SubcontractReceiptLifecycle.Posting => "info",
        SubcontractReceiptLifecycle.Posted
            or SubcontractReceiptLifecycle.Approved => "success",
        SubcontractReceiptLifecycle.PendingApproval => "warning",
        SubcontractReceiptLifecycle.Reversed => "danger",
        SubcontractReceiptLifecycle.Closed => "muted",
        _ => "info",
    };

    public static string PoStatusTone(POStatus s) => s switch
    {
        // P2.4 fix — map Draft to "info" so in-flight pre-approval POs are
        // visually distinct from terminal Closed/Cancelled states (both of
        // which fall through to "muted" gray). "neutral" isn't a BadgeStyle
        // case; it would fall through to the same gray, conflating Draft
        // with Closed.
        POStatus.Draft => "info",
        POStatus.PendingApproval => "warning",
        POStatus.Approved => "info",
        POStatus.Sent => "info",
        POStatus.PartiallyReceived => "warning",
        POStatus.Received => "success",
        POStatus.Invoiced => "success",
        POStatus.Closed => "muted",
        POStatus.Cancelled => "muted",
        _ => "info",
    };

    public static string ExceptionSeverityTone(string severity) => severity switch
    {
        "High" => "danger",
        "Medium" => "warning",
        "Low" => "info",
        _ => "muted",
    };

    // Pagination helpers — tab-aware via ActiveTabTotalCount / ActiveTabRowCount.
    public bool HasPrev => Skip > 0;
    public bool HasNext => (Skip + ActiveTabRowCount) < ActiveTabTotalCount;
    public int PrevSkip => Math.Max(0, Skip - PageSize);
    public int NextSkip => Skip + PageSize;
}
