using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Services.Engineering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 14.2 PR-1 (2026-05-26 evening) — admin probe for IDocumentService.
//
// FIVE WRITE BUTTONS (per the new HARD LOCK from PR #365 — every probe
// must exercise the INSERT path before merge so latent xmin/bytea config
// issues surface in dev, not prod):
//   1. Create Document
//   2. Add Version
//   3. Approve Version
//   4. Release Version
//   5. Link to Item
// Plus a read-only "Get for Item" handler.
//
// Service-only DI per CHERRY025.
[Authorize(Roles = "Admin")]
public sealed class DocumentProbeModel : PageModel
{
    private readonly IDocumentService _svc;
    private readonly ILogger<DocumentProbeModel> _logger;

    public DocumentProbeModel(IDocumentService svc, ILogger<DocumentProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // Form: Create Document
    [BindProperty] public int CreateCompanyId { get; set; } = 1;
    [BindProperty] public string? CreateDocumentNumber { get; set; }
    [BindProperty] public string? CreateTitle { get; set; }
    [BindProperty] public DocumentType CreateDocumentType { get; set; } = DocumentType.Drawing;
    [BindProperty] public bool CreateIsControlled { get; set; } = true;
    [BindProperty] public string? CreateDescription { get; set; }
    [BindProperty] public string? CreateOwnerName { get; set; }

    // Form: Add Version
    [BindProperty] public int AddVerDocumentId { get; set; }
    [BindProperty] public string? AddVerRevisionCode { get; set; }
    [BindProperty] public string? AddVerFileName { get; set; }
    [BindProperty] public string? AddVerContentType { get; set; } = "application/pdf";
    [BindProperty] public long AddVerFileSizeBytes { get; set; } = 0;
    [BindProperty] public string? AddVerContentHash { get; set; }
    [BindProperty] public string? AddVerContentLocationUri { get; set; }
    [BindProperty] public string? AddVerSourceEcoNumber { get; set; }

    // Form: Approve / Release
    [BindProperty] public int LifecycleVersionId { get; set; }

    // Form: Link to Item
    [BindProperty] public int LinkItemId { get; set; }
    [BindProperty] public int LinkDocumentId { get; set; }
    [BindProperty] public ItemDocumentLinkPurpose LinkPurpose { get; set; } = ItemDocumentLinkPurpose.BillOfDrawing;
    [BindProperty] public bool LinkIsPrimary { get; set; } = true;

    // Form: Get for Item
    [BindProperty(SupportsGet = true)] public int GetForItemId { get; set; }

    // Output
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public IReadOnlyList<ItemDocumentSummary>? Summaries { get; private set; }

    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ReloadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var result = await _svc.CreateAsync(
            CreateCompanyId,
            CreateDocumentNumber ?? string.Empty,
            CreateTitle ?? string.Empty,
            CreateDocumentType,
            CreateIsControlled,
            CreateDescription,
            CreateOwnerName,
            by,
            ct);
        Set(result.IsSuccess, result.IsSuccess
            ? $"Created Document {result.Value!.Id} ('{result.Value.DocumentNumber}', {result.Value.DocumentType}, Status={result.Value.Status})."
            : result.Error);
        _logger.LogInformation("DocumentProbe Create: Success={Ok} Error={Err}", result.IsSuccess, result.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostAddVersionAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var result = await _svc.AddVersionAsync(
            AddVerDocumentId,
            AddVerRevisionCode ?? string.Empty,
            AddVerFileName ?? string.Empty,
            AddVerContentType,
            AddVerFileSizeBytes,
            null,
            AddVerContentHash,
            AddVerContentLocationUri,
            AddVerSourceEcoNumber,
            null,
            by,
            ct);
        Set(result.IsSuccess, result.IsSuccess
            ? $"Added DocumentVersion {result.Value!.Id} (Document={result.Value.DocumentId}, V{result.Value.VersionNumber} Rev '{result.Value.RevisionCode}', Status={result.Value.Status})."
            : result.Error);
        _logger.LogInformation("DocumentProbe AddVersion: Success={Ok} Error={Err}", result.IsSuccess, result.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var result = await _svc.ApproveVersionAsync(LifecycleVersionId, by, ct);
        Set(result.IsSuccess, result.IsSuccess
            ? $"Approved DocumentVersion {result.Value!.Id} (Document={result.Value.DocumentId}, Rev '{result.Value.RevisionCode}', ApprovedAt={result.Value.ApprovedAtUtc:u})."
            : result.Error);
        _logger.LogInformation("DocumentProbe Approve: VId={VId} Success={Ok} Error={Err}", LifecycleVersionId, result.IsSuccess, result.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostReleaseAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var result = await _svc.ReleaseVersionAsync(LifecycleVersionId, by, null, ct);
        Set(result.IsSuccess, result.IsSuccess
            ? $"Released DocumentVersion {result.Value!.Id} (Document={result.Value.DocumentId}, Rev '{result.Value.RevisionCode}', ReleasedAt={result.Value.ReleasedAtUtc:u})."
            : result.Error);
        _logger.LogInformation("DocumentProbe Release: VId={VId} Success={Ok} Error={Err}", LifecycleVersionId, result.IsSuccess, result.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostLinkAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var result = await _svc.LinkToItemAsync(LinkItemId, LinkDocumentId, LinkPurpose, LinkIsPrimary, null, by, ct);
        Set(result.IsSuccess, result.IsSuccess
            ? $"Linked Item {result.Value!.ItemId} ↔ Document {result.Value.DocumentId} ({result.Value.LinkPurpose}, Primary={result.Value.IsPrimary}). LinkId={result.Value.Id}."
            : result.Error);
        _logger.LogInformation("DocumentProbe Link: Item={ItemId} Doc={DocId} Purpose={Purpose} Success={Ok} Error={Err}",
            LinkItemId, LinkDocumentId, LinkPurpose, result.IsSuccess, result.Error);
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostGetForItemAsync(CancellationToken ct)
    {
        if (GetForItemId <= 0)
        {
            Set(false, "ItemId is required (must be > 0).");
            return Page();
        }
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (GetForItemId > 0)
        {
            Summaries = await _svc.GetDocumentsForItemAsync(GetForItemId, ct);
        }
        return Page();
    }

    private void Set(bool ok, string? msg)
    {
        Outcome = msg;
        OutcomeIsError = !ok;
    }
}
