// Sprint 13.5 PR #5d — Operator Workbench PageModel.
//
// The shop-floor execution surface. Operators land here to:
//   1. See the ops queued at their work center
//   2. Clock in / out against an operation
//   3. Record qty completed + scrap qty + rework qty (with reason codes)
//   4. Move an op through its 8-state status machine
//
// COMPOSITION (per Dean lock 2026-05-23 — feedback_reuse_cockpit_primitives):
//   - _CockpitPageHeader  — title + scope + LIVE + Voice
//   - _CockpitKpiBand     — My Active Op / Setup mins today / Run mins today / Qty today
//   - _CockpitTabShell    — Operations | My Time Today | Activity
//   - _CockpitShell       — queue (left) + preview (right) with action verb tray
//
// QUEUE: ProductionOperations with Status in (Released, InSetup, Running, Paused),
// ordered by PlannedStart ASC, NULLS LAST. Tenant-scoped via
// ProductionOperation.CompanyIdSnapshot ∩ ITenantContext.VisibleCompanyIds.
//
// PREVIEW: Action verb tray:
//   - Clock In       → POST OnPostClockIn
//   - Clock Out      → POST OnPostClockOut
//   - Log Scrap qty  → POST OnPostLogScrap (qty + reason code)
//   - Log Rework qty → POST OnPostLogRework (qty + reason code)
//   - Complete       → POST OnPostComplete

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared.ControlCenter;
using Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Navigation.Cockpit;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Production;

[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "AppDbContext used only for read-side queue projections (ops queue, " +
    "user lookup, reason-code dropdowns, KPI band aggregates). All mutations " +
    "flow through ILaborService (clock in/out) and IProductionOperationService " +
    "(scrap/rework qty + reason code via RecordActualsAsync). ADR-025 §B.")]
public class WorkbenchModel : ControlCenterPageModel
{
    public const string TabOperations = "operations";
    public const string TabMyTime = "mytime";
    public const string TabActivity = "activity";

    // ControlCenterPageModel abstract overrides — identify this CC in the
    // ControlCenterRegistry (ADR-017 Control-Center-First IA).
    public override string ControlCenterCode => "production.workbench";
    public override string ControlCenterTitle => "Operator Workbench";

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILaborService _laborService;
    private readonly IProductionOperationService _opService;
    private readonly ILogger<WorkbenchModel> _logger;

    public WorkbenchModel(
        AppDbContext db,
        ITenantContext tenantContext,
        ILaborService laborService,
        IProductionOperationService opService,
        ILogger<WorkbenchModel> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _laborService = laborService;
        _opService = opService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    public string ActiveTab => string.IsNullOrWhiteSpace(Tab) ? TabOperations : Tab.ToLowerInvariant();

    // View-models for the partials.
    public CockpitPageHeaderViewModel? PageHeader { get; private set; }
    public CockpitKpiBandViewModel? KpiBand { get; private set; }
    public CockpitTabShellModel TabShell { get; private set; } = new();
    public CockpitShellViewModel? QueueShell { get; private set; }

    // Operator context (the user driving the workbench).
    public int OperatorUserId { get; private set; }
    public string OperatorName { get; private set; } = "Operator";
    public LaborEntry? ActiveLaborEntry { get; private set; }

    // For preview blob hydration in the JS.
    public string PreviewBlobJson { get; private set; } = "{}";

    // Active ReasonCodes for the dropdowns (scrap + rework).
    public List<ReasonCodeOption> ScrapReasonCodes { get; private set; } = new();
    public List<ReasonCodeOption> ReworkReasonCodes { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadOperatorContextAsync(ct);
        await LoadActiveLaborEntryAsync(ct);
        await LoadReasonCodesAsync(ct);
        await BuildViewModelsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostClockInAsync(int productionOperationId, int? laborTypeId, string? notes, CancellationToken ct)
    {
        await LoadOperatorContextAsync(ct);
        var result = await _laborService.ClockInAsync(
            new ClockInRequest(productionOperationId, OperatorUserId, laborTypeId, notes, OperatorName), ct);
        if (!result.IsSuccess)
        {
            TempData["WorkbenchError"] = result.Error;
        }
        return RedirectToPage(new { tab = Tab });
    }

    public async Task<IActionResult> OnPostClockOutAsync(int laborEntryId, string? notes, CancellationToken ct)
    {
        await LoadOperatorContextAsync(ct);
        var result = await _laborService.ClockOutAsync(
            new ClockOutRequest(laborEntryId, notes, OperatorName), ct);
        if (!result.IsSuccess)
        {
            TempData["WorkbenchError"] = result.Error;
        }
        return RedirectToPage(new { tab = Tab });
    }

    public async Task<IActionResult> OnPostLogScrapAsync(int productionOperationId, decimal scrapQty, string? reasonCode, string? notes, CancellationToken ct)
    {
        await LoadOperatorContextAsync(ct);
        // RecordActualsAsync owns the delta — pull current values + add the scrap.
        var op = await _db.ProductionOperations.FirstOrDefaultAsync(x => x.Id == productionOperationId, ct);
        if (op is null)
        {
            TempData["WorkbenchError"] = "Operation not found.";
            return RedirectToPage(new { tab = Tab });
        }
        var newScrap = op.ScrappedQty + scrapQty;
        var combinedNotes = string.IsNullOrWhiteSpace(reasonCode)
            ? notes
            : $"[Scrap reason: {reasonCode}] {notes}".Trim();
        var result = await _opService.RecordActualsAsync(
            new RecordOperationActualsRequest(
                ProductionOperationId: productionOperationId,
                CompletedQty: op.CompletedQty,
                ScrappedQty: newScrap,
                ReworkQty: op.ReworkQty,
                ActualSetupMins: op.ActualSetupMins,
                ActualRunMins: op.ActualRunMins,
                ActualStart: op.ActualStart,
                ActualEnd: op.ActualEnd,
                OperatorUserIdsCsv: op.OperatorUserIdsCsv,
                Notes: combinedNotes,
                ModifiedBy: OperatorName),
            ct);
        if (!result.IsSuccess)
        {
            TempData["WorkbenchError"] = result.Error;
        }
        return RedirectToPage(new { tab = Tab });
    }

    public async Task<IActionResult> OnPostLogReworkAsync(int productionOperationId, decimal reworkQty, string? reasonCode, string? notes, CancellationToken ct)
    {
        await LoadOperatorContextAsync(ct);
        var op = await _db.ProductionOperations.FirstOrDefaultAsync(x => x.Id == productionOperationId, ct);
        if (op is null)
        {
            TempData["WorkbenchError"] = "Operation not found.";
            return RedirectToPage(new { tab = Tab });
        }
        var newRework = op.ReworkQty + reworkQty;
        var combinedNotes = string.IsNullOrWhiteSpace(reasonCode)
            ? notes
            : $"[Rework reason: {reasonCode}] {notes}".Trim();
        var result = await _opService.RecordActualsAsync(
            new RecordOperationActualsRequest(
                ProductionOperationId: productionOperationId,
                CompletedQty: op.CompletedQty,
                ScrappedQty: op.ScrappedQty,
                ReworkQty: newRework,
                ActualSetupMins: op.ActualSetupMins,
                ActualRunMins: op.ActualRunMins,
                ActualStart: op.ActualStart,
                ActualEnd: op.ActualEnd,
                OperatorUserIdsCsv: op.OperatorUserIdsCsv,
                Notes: combinedNotes,
                ModifiedBy: OperatorName),
            ct);
        if (!result.IsSuccess)
        {
            TempData["WorkbenchError"] = result.Error;
        }
        return RedirectToPage(new { tab = Tab });
    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------

    private async Task LoadOperatorContextAsync(CancellationToken ct)
    {
        var name = User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var u = await _db.Users
                .Where(x => x.Username == name || x.Email == name)
                .Select(x => new { x.Id, x.Username, x.FullName })
                .FirstOrDefaultAsync(ct);
            if (u is not null)
            {
                OperatorUserId = u.Id;
                OperatorName = !string.IsNullOrEmpty(u.FullName) ? u.FullName : u.Username;
                return;
            }
        }
        // Fallback for unauth/demo (shouldn't hit in prod — login is required).
        var fallback = await _db.Users.OrderBy(x => x.Id).Select(x => new { x.Id, x.Username }).FirstOrDefaultAsync(ct);
        OperatorUserId = fallback?.Id ?? 0;
        OperatorName = fallback?.Username ?? "Operator";
    }

    private async Task LoadActiveLaborEntryAsync(CancellationToken ct)
    {
        if (OperatorUserId <= 0) return;
        var result = await _laborService.GetActiveAsync(OperatorUserId, ct);
        ActiveLaborEntry = result.IsSuccess ? result.Value : null;
    }

    private async Task LoadReasonCodesAsync(CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds.ToHashSet();
        var all = await _db.ReasonCodes
            .Where(rc => rc.IsActive
                      && (rc.CompanyId == null || visible.Contains(rc.CompanyId.Value)))
            .OrderBy(rc => rc.SortOrder).ThenBy(rc => rc.Code)
            .Select(rc => new { rc.Code, rc.Description, rc.Category })
            .ToListAsync(ct);
        ScrapReasonCodes = all
            .Where(rc => rc.Category == ReasonCodeCategory.Scrap)
            .Select(rc => new ReasonCodeOption(rc.Code, $"{rc.Code} — {rc.Description}"))
            .ToList();
        ReworkReasonCodes = all
            .Where(rc => rc.Category == ReasonCodeCategory.Rework)
            .Select(rc => new ReasonCodeOption(rc.Code, $"{rc.Code} — {rc.Description}"))
            .ToList();
    }

    private async Task BuildViewModelsAsync(CancellationToken ct)
    {
        PageHeader = new CockpitPageHeaderViewModel
        {
            Title = "Operator Workbench",
            Scope = "Shop floor",
            Subtitle = ActiveLaborEntry is null
                ? $"Welcome, {OperatorName}. Push-to-talk voice (hold Space)."
                : $"Clocked in on operation #{ActiveLaborEntry.ProductionOperationId} since {ActiveLaborEntry.ClockInAt:HH:mm} UTC.",
            ShowLive = true,
            RefreshedAtText = $"as of {DateTime.UtcNow:HH:mm} UTC",
            ShowVoiceButton = true,
            VoiceButtonLabel = "Voice"
        };

        // KPI band — load today's totals
        var totalsResult = await _laborService.GetTodayTotalsAsync(OperatorUserId, ct);
        var totals = totalsResult.IsSuccess ? totalsResult.Value : new LaborTodayTotals(0, 0, 0, 0);

        KpiBand = new CockpitKpiBandViewModel
        {
            HeroMode = true,
            Tiles = new List<CockpitKpiTileViewModel>
            {
                new()
                {
                    Label = "MY ACTIVE OP",
                    Value = ActiveLaborEntry is not null
                        ? $"#{ActiveLaborEntry.ProductionOperationId}"
                        : "—",
                    Unit = ActiveLaborEntry is not null ? "since " + ActiveLaborEntry.ClockInAt.ToString("HH:mm") : "no clock-in",
                    Tone = ActiveLaborEntry is not null ? "info" : "neutral"
                },
                new()
                {
                    Label = "SETUP MINS TODAY",
                    Value = totals.SetupMins.ToString("0.#"),
                    Unit = "mins",
                    Tone = "neutral"
                },
                new()
                {
                    Label = "RUN MINS TODAY",
                    Value = totals.RunMins.ToString("0.#"),
                    Unit = "mins",
                    Tone = "info"
                },
                new()
                {
                    Label = "QTY COMPLETED TODAY",
                    Value = totals.CompletedQty.ToString("0.#"),
                    Unit = totals.OpsTouched == 1 ? "across 1 op" : $"across {totals.OpsTouched} ops",
                    Tone = totals.CompletedQty > 0 ? "info" : "neutral"
                }
            }
        };

        TabShell = new CockpitTabShellModel
        {
            ActiveTabKey = ActiveTab,
            BaseRoute = "/Production/Workbench",
            Tabs = new List<CockpitTab>
            {
                new(Key: TabOperations, Label: "Operations", IconClass: "fas fa-list-check", IsDefault: true),
                new(Key: TabMyTime, Label: "My Time Today", IconClass: "fas fa-clock"),
                new(Key: TabActivity, Label: "Activity", IconClass: "fas fa-clock-rotate-left")
            }
        };

        // Queue: ops at the operator's preferred location, in workable status,
        // ordered by PlannedStart ASC NULLS LAST.
        var visible = _tenantContext.VisibleCompanyIds.ToHashSet();
        var workableStatuses = new[]
        {
            ProductionOperationStatus.Released,
            ProductionOperationStatus.InSetup,
            ProductionOperationStatus.Running,
            ProductionOperationStatus.Paused
        };

        var ops = await _db.ProductionOperations
            .Where(o => workableStatuses.Contains(o.Status)
                     && visible.Contains(o.CompanyIdSnapshot))
            .OrderBy(o => o.PlannedStart == null)   // nulls last
            .ThenBy(o => o.PlannedStart)
            .ThenBy(o => o.SequenceNumber)
            .Take(50)
            .ToListAsync(ct);

        // Build queue rows + preview blob.
        var rows = new List<ICockpitQueueRow>();
        var previewBlob = new Dictionary<string, object>();
        foreach (var op in ops)
        {
            var orderInfo = await _db.ProductionOrders
                .Where(po => po.Id == op.ProductionOrderId)
                .Select(po => new { po.OrderNumber, po.Title, po.QuantityOrdered })
                .FirstOrDefaultAsync(ct);

            var wcCode = await _db.Set<WorkCenter>()
                .Where(wc => wc.Id == op.WorkCenterId)
                .Select(wc => wc.Code)
                .FirstOrDefaultAsync(ct) ?? "?";

            var tone = op.Status switch
            {
                ProductionOperationStatus.Running => "info",
                ProductionOperationStatus.InSetup => "warning",
                ProductionOperationStatus.Paused => "warning",
                ProductionOperationStatus.Released => "neutral",
                _ => "neutral"
            };

            rows.Add(new WorkbenchQueueRow(
                id: op.Id.ToString(),
                primary: $"OP {op.SequenceNumber:D2} · {wcCode}",
                secondary: orderInfo?.OrderNumber + " — " + (op.Description ?? "no description"),
                requiredAt: op.PlannedStart,
                tone: tone,
                statusLabel: op.Status.ToString().ToUpperInvariant(),
                statusTone: tone,
                meta: new List<MetaTriple>
                {
                    new("Qty", $"{op.CompletedQty:0.#}/{op.PlannedQty:0.#}"),
                    new("Setup", $"{op.PlannedSetupMins:0.#}m"),
                    new("Run", $"{op.PlannedRunMins:0.#}m"),
                }));

            previewBlob[op.Id.ToString()] = new
            {
                opId = op.Id,
                orderNumber = orderInfo?.OrderNumber ?? "(no order)",
                orderTitle = orderInfo?.Title ?? "",
                sequenceNumber = op.SequenceNumber,
                description = op.Description,
                workCenter = wcCode,
                status = op.Status.ToString(),
                plannedQty = op.PlannedQty,
                completedQty = op.CompletedQty,
                scrappedQty = op.ScrappedQty,
                reworkQty = op.ReworkQty,
                plannedSetupMins = op.PlannedSetupMins,
                plannedRunMins = op.PlannedRunMins,
                actualSetupMins = op.ActualSetupMins,
                actualRunMins = op.ActualRunMins,
                instructions = op.Instructions,
                notes = op.Notes
            };
        }

        PreviewBlobJson = System.Text.Json.JsonSerializer.Serialize(previewBlob);

        QueueShell = new CockpitShellViewModel
        {
            Queue = new CockpitQueueViewModel
            {
                TitleHtml = "READY TO RUN",
                TitleIconClass = "fas fa-conveyor-belt",
                CountBadge = ops.Count,
                SearchPlaceholder = "Search op / order / work center...",
                SearchElementId = "workbenchSearch",
                FilterFunctionName = "filterWorkbench",
                SelectFunctionName = "selectWorkbenchOp",
                Groups = ops.Count == 0
                    ? Array.Empty<CockpitQueueGroupViewModel>()
                    : new[]
                    {
                        new CockpitQueueGroupViewModel
                        {
                            Code = "ready",
                            Label = "Ready to run",
                            Tone = "info",
                            IconClass = "fas fa-play",
                            Rows = rows
                        }
                    },
                Empty = ops.Count == 0
                    ? new CockpitEmptyViewModel
                    {
                        IconClass = "fas fa-mug-hot",
                        IconTone = "success",
                        Message = "Queue is clear — no operations waiting to run."
                    }
                    : null
            },
            Welcome = new CockpitWelcomeViewModel
            {
                IconClass = "fas fa-hand-back-fist",
                Title = "Select an operation to begin",
                Subtitle = "Pick an operation from the left to clock in, log progress, and complete it.",
                Stats = new[]
                {
                    new CockpitWelcomeStat("Ready", ops.Count.ToString("0"), "info"),
                    new CockpitWelcomeStat("Setup mins today", totals.SetupMins.ToString("0.#"), "neutral"),
                    new CockpitWelcomeStat("Run mins today", totals.RunMins.ToString("0.#"), "info"),
                    new CockpitWelcomeStat("Qty today", totals.CompletedQty.ToString("0.#"), "info")
                }
            },
            PreviewPartialName = "_WorkbenchPreview",
            PreviewPartialModel = this,
            PreviewBlobJson = PreviewBlobJson,
            PreviewBlobElementId = "__workbenchDetails"
        };
    }

    // --------------------------------------------------------------------
    // Row adapter — implements ICockpitQueueRow for the shared partials.
    // --------------------------------------------------------------------
    private sealed class WorkbenchQueueRow : ICockpitQueueRow
    {
        public string Id { get; }
        public string Primary { get; }
        public string Secondary { get; }
        public DateTime? RequiredAt { get; }
        public string Tone { get; }
        public IReadOnlyList<MetaTriple> Meta { get; }
        public string? StatusLabel { get; }
        public string? StatusTone { get; }

        public WorkbenchQueueRow(
            string id, string primary, string secondary, DateTime? requiredAt,
            string tone, string statusLabel, string statusTone, IReadOnlyList<MetaTriple> meta)
        {
            Id = id; Primary = primary; Secondary = secondary; RequiredAt = requiredAt;
            Tone = tone; StatusLabel = statusLabel; StatusTone = statusTone; Meta = meta;
        }
    }

    public sealed record ReasonCodeOption(string Value, string Label);
}
