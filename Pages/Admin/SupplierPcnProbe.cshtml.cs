using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Services.Engineering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 14.3 PR-5 (2026-05-27) — admin probe for ISupplierNotificationService.
//
// NINE WRITE BUTTONS per the Lock 16 corollary from PR #365:
//   1. Create Supplier PCN
//   2. Mark Pending (Draft → Pending)
//   3. Send PCN (Pending → SentToSupplier, fires outbox event)
//   4. Record Acknowledgement (SentToSupplier → SupplierAcknowledged)
//   5. Record Impact Assessment (SupplierAcknowledged → ImpactAssessmentReceived)
//   6. Approve (ImpactAssessmentReceived → Approved)
//   7. Reject (ImpactAssessmentReceived → Rejected)
//   8. Close PCN (Approved → Closed)
//   9. Get PCN (read form)
[Authorize(Roles = "Admin")]
public sealed class SupplierPcnProbeModel : PageModel
{
    private readonly ISupplierNotificationService _svc;
    private readonly ILogger<SupplierPcnProbeModel> _logger;

    public SupplierPcnProbeModel(ISupplierNotificationService svc, ILogger<SupplierPcnProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Create ---
    [BindProperty] public int CreateCompanyId { get; set; } = 1;
    [BindProperty] public string? CreatePcnNumber { get; set; } = "PCN-2026-SKF-001";
    [BindProperty] public string? CreateTitle { get; set; } = "Process change notification — SKF 6207-2RS inner race grinding process upgrade from centerless to CNC";
    [BindProperty] public string? CreateDescription { get; set; }
    [BindProperty] public PcnType CreateType { get; set; } = PcnType.ProcessChange;
    [BindProperty] public int? CreateItemId { get; set; } = 9245;
    [BindProperty] public int? CreateVendorId { get; set; }
    [BindProperty] public int? CreateOriginatingEcrId { get; set; }
    [BindProperty] public string? CreateChangeDescription { get; set; } = "Migrate inner race grinding from centerless to CNC cylindrical — tighter concentricity control (2 µm vs 5 µm)";
    [BindProperty] public string? CreateImpactDescription { get; set; } = "Improved concentricity reduces vibration signature in high-speed applications. No dimensional change.";
    [BindProperty] public string? CreateCurrentSpec { get; set; } = "Drawing SKF-6207-IR Rev B, concentricity 5 µm max";
    [BindProperty] public string? CreateProposedSpec { get; set; } = "Drawing SKF-6207-IR Rev C, concentricity 2 µm max";
    [BindProperty] public bool CreateFirstArticleRequired { get; set; } = true;
    [BindProperty] public bool CreatePpapRequired { get; set; }
    [BindProperty] public NotificationDeliveryMethod CreateDeliveryMethod { get; set; } = NotificationDeliveryMethod.Email;

    // --- Mark Pending ---
    [BindProperty] public int PendingPcnId { get; set; }

    // --- Send ---
    [BindProperty] public int SendPcnId { get; set; }

    // --- Acknowledge ---
    [BindProperty] public int AckPcnId { get; set; }
    [BindProperty] public string? AckRespondent { get; set; }

    // --- Impact Assessment ---
    [BindProperty] public int ImpactPcnId { get; set; }
    [BindProperty] public string? ImpactAssessment { get; set; }
    [BindProperty] public decimal? ImpactCost { get; set; }
    [BindProperty] public int? ImpactLeadTimeDays { get; set; }

    // --- Approve ---
    [BindProperty] public int ApprovePcnId { get; set; }

    // --- Reject ---
    [BindProperty] public int RejectPcnId { get; set; }
    [BindProperty] public string? RejectReason { get; set; }

    // --- Close ---
    [BindProperty] public int ClosePcnId { get; set; }
    [BindProperty] public string? CloseShipmentRef { get; set; }

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetPcnId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public SupplierProcessChangeNotification? PcnView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var num = CreatePcnNumber ?? $"PCN-2026-TEST-{ts}";

        var request = new CreateSupplierPcnRequest(
            CompanyId: CreateCompanyId,
            PcnNumber: num,
            Title: CreateTitle ?? "Supplier process change notification",
            Type: CreateType,
            VendorId: CreateVendorId,
            ItemId: CreateItemId,
            OriginatingEcrId: CreateOriginatingEcrId,
            ChangeDescription: CreateChangeDescription,
            ImpactDescription: CreateImpactDescription,
            CurrentSpecification: CreateCurrentSpec,
            ProposedSpecification: CreateProposedSpec,
            FirstArticleRequired: CreateFirstArticleRequired,
            PpapRequired: CreatePpapRequired,
            DeliveryMethod: CreateDeliveryMethod,
            Description: CreateDescription,
            CreatedBy: by);

        var r = await _svc.CreateAsync(request, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created SupplierPCN {r.Value!.Id} '{r.Value.PcnNumber}' Status={r.Value.Status} Type={r.Value.Type} Item={r.Value.ItemId} FAI={r.Value.FirstArticleRequired}."
            : r.Error);
        _logger.LogInformation("SupplierPcnProbe Create: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostMarkPendingAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.MarkPendingAsync(PendingPcnId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SupplierPCN {r.Value!.Id} '{r.Value.PcnNumber}' → Status={r.Value.Status}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SendAsync(SendPcnId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SupplierPCN {r.Value!.Id} '{r.Value.PcnNumber}' SENT via {r.Value.DeliveryMethod} at {r.Value.SentAtUtc:u} (correlation={r.Value.OutboxCorrelationId})."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(CancellationToken ct)
    {
        var r = await _svc.RecordAcknowledgementAsync(AckPcnId, AckRespondent ?? "supplier-contact", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SupplierPCN {r.Value!.Id} '{r.Value.PcnNumber}' → SupplierAcknowledged at {r.Value.SupplierAcknowledgedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRecordImpactAsync(CancellationToken ct)
    {
        var r = await _svc.RecordImpactAssessmentAsync(ImpactPcnId,
            ImpactAssessment ?? "No material impact — tooling change is cost-neutral",
            ImpactCost, ImpactLeadTimeDays, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SupplierPCN {r.Value!.Id} '{r.Value.PcnNumber}' → ImpactAssessmentReceived. Cost={r.Value.SupplierEstimatedCostImpact} LT={r.Value.SupplierEstimatedLeadTimeImpactDays}d."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ApproveAsync(ApprovePcnId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SupplierPCN {r.Value!.Id} '{r.Value.PcnNumber}' → Approved by {r.Value.ApprovedBy} at {r.Value.ApprovedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostRejectAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RejectAsync(RejectPcnId, by, RejectReason ?? "(no reason)", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SupplierPCN {r.Value!.Id} '{r.Value.PcnNumber}' → Rejected: {r.Value.RejectionReason}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CloseAsync(ClosePcnId, by, CloseShipmentRef, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"SupplierPCN {r.Value!.Id} '{r.Value.PcnNumber}' → Closed. 1st shipment={r.Value.FirstConformingShipmentRef ?? "—"}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetPcnAsync(CancellationToken ct)
    {
        if (GetPcnId <= 0) { Set(false, "PcnId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetPcnId > 0) PcnView = await _svc.GetAsync(GetPcnId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
