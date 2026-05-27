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

// Sprint 14.3 PR-5 (2026-05-27) — admin probe for ICustomerNotificationService.
//
// EIGHT WRITE BUTTONS per the Lock 16 corollary from PR #365:
//   1. Create Customer Notice
//   2. Mark Pending (Draft → Pending)
//   3. Send Notice (Pending → Sent, fires outbox event)
//   4. Record Acknowledgement (Sent → Acknowledged)
//   5. Record Dispute (Sent → Disputed)
//   6. Resolve Dispute (Disputed → Resolved)
//   7. Close Notice (Acknowledged/Resolved → Closed)
//   8. Get Notice (read form)
[Authorize(Roles = "Admin")]
public sealed class CustomerNoticeProbeModel : PageModel
{
    private readonly ICustomerNotificationService _svc;
    private readonly ILogger<CustomerNoticeProbeModel> _logger;

    public CustomerNoticeProbeModel(ICustomerNotificationService svc, ILogger<CustomerNoticeProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // --- Create ---
    [BindProperty] public int CreateCompanyId { get; set; } = 1;
    [BindProperty] public string? CreateNoticeNumber { get; set; } = "CN-2026-BRG-001";
    [BindProperty] public string? CreateTitle { get; set; } = "Engineering change notification — BRG-6207-2RS bearing spec revision from Rev C to Rev D";
    [BindProperty] public string? CreateDescription { get; set; }
    [BindProperty] public CustomerNoticeType CreateType { get; set; } = CustomerNoticeType.EngineeringChange;
    [BindProperty] public int? CreateItemId { get; set; } = 9245;
    [BindProperty] public int? CreateCustomerId { get; set; }
    [BindProperty] public int? CreateOriginatingEcrId { get; set; }
    [BindProperty] public string? CreateChangeDescription { get; set; } = "Bearing inner race hardness specification changed from HRC 58-62 to HRC 60-64 per metallurgical analysis";
    [BindProperty] public string? CreateImpactDescription { get; set; } = "Higher minimum hardness improves fatigue life by 15%. No dimensional or interchangeability impact.";
    [BindProperty] public NotificationDeliveryMethod CreateDeliveryMethod { get; set; } = NotificationDeliveryMethod.Email;
    [BindProperty] public string? CreateCustomerContactName { get; set; } = "Quality Engineering Dept";
    [BindProperty] public string? CreateCustomerContactEmail { get; set; }

    // --- Mark Pending ---
    [BindProperty] public int PendingNoticeId { get; set; }

    // --- Send ---
    [BindProperty] public int SendNoticeId { get; set; }

    // --- Acknowledge ---
    [BindProperty] public int AckNoticeId { get; set; }
    [BindProperty] public string? AckResponseText { get; set; }

    // --- Dispute ---
    [BindProperty] public int DisputeNoticeId { get; set; }
    [BindProperty] public string? DisputeReason { get; set; }

    // --- Resolve Dispute ---
    [BindProperty] public int ResolveNoticeId { get; set; }
    [BindProperty] public string? ResolveResolution { get; set; }

    // --- Close ---
    [BindProperty] public int CloseNoticeId { get; set; }

    // --- Read ---
    [BindProperty(SupportsGet = true)] public int GetNoticeId { get; set; }

    // --- Output ---
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public CustomerNotice? NoticeView { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var num = CreateNoticeNumber ?? $"CN-2026-TEST-{ts}";

        var request = new CreateCustomerNoticeRequest(
            CompanyId: CreateCompanyId,
            NoticeNumber: num,
            Title: CreateTitle ?? "Customer engineering change notification",
            Type: CreateType,
            ItemId: CreateItemId,
            CustomerId: CreateCustomerId,
            OriginatingEcrId: CreateOriginatingEcrId,
            ChangeDescription: CreateChangeDescription,
            ImpactDescription: CreateImpactDescription,
            DeliveryMethod: CreateDeliveryMethod,
            CustomerContactName: CreateCustomerContactName,
            CustomerContactEmail: CreateCustomerContactEmail,
            Description: CreateDescription,
            CreatedBy: by);

        var r = await _svc.CreateAsync(request, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Created CustomerNotice {r.Value!.Id} '{r.Value.NoticeNumber}' Status={r.Value.Status} Type={r.Value.Type} Item={r.Value.ItemId} Delivery={r.Value.DeliveryMethod}."
            : r.Error);
        _logger.LogInformation("CustomerNoticeProbe Create: Success={Ok} Err={Err}", r.IsSuccess, r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostMarkPendingAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.MarkPendingAsync(PendingNoticeId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CustomerNotice {r.Value!.Id} '{r.Value.NoticeNumber}' → Status={r.Value.Status}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.SendAsync(SendNoticeId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CustomerNotice {r.Value!.Id} '{r.Value.NoticeNumber}' SENT via {r.Value.DeliveryMethod} at {r.Value.SentAtUtc:u} (correlation={r.Value.OutboxCorrelationId})."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.RecordAcknowledgementAsync(AckNoticeId, by, AckResponseText, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CustomerNotice {r.Value!.Id} '{r.Value.NoticeNumber}' → Acknowledged at {r.Value.AcknowledgedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostDisputeAsync(CancellationToken ct)
    {
        var r = await _svc.RecordDisputeAsync(DisputeNoticeId, DisputeReason ?? "(no reason)", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CustomerNotice {r.Value!.Id} '{r.Value.NoticeNumber}' → Disputed: {r.Value.DisputeReason}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostResolveDisputeAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ResolveDisputeAsync(ResolveNoticeId, by, ResolveResolution ?? "(no resolution text)", ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CustomerNotice {r.Value!.Id} '{r.Value.NoticeNumber}' → Resolved at {r.Value.DisputeResolvedAtUtc:u}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CloseAsync(CloseNoticeId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"CustomerNotice {r.Value!.Id} '{r.Value.NoticeNumber}' → Status={r.Value.Status}."
            : r.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetNoticeAsync(CancellationToken ct)
    {
        if (GetNoticeId <= 0) { Set(false, "NoticeId must be > 0."); return Page(); }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetNoticeId > 0) NoticeView = await _svc.GetAsync(GetNoticeId, ct);
        return Page();
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
