// Theme B9 Wave 6 PR-18 (2026-05-31) — IProjectServiceService. CLOSES B9.
//
// Service handoff + warranty (research §15) + the AI-assisted project review.
// The handoff is the equipment-project tail (installed asset / commissioning /
// warranty / as-built / training / customer sign-off). Signing off a handoff is
// gated on startup-checklist + training complete; the project close gate
// (CustomerProjectService.UpdateStatusAsync) blocks while a handoff is unsigned.
//
// GenerateProjectReviewAsync composes a DATA-DRIVEN project review (status,
// projected margin, open RAID + quality blockers, billing position, and a
// closeout-readiness checklist) and stamps it onto the existing CustomerProject
// AI-summary fields. No external model call — the "AI review" is a deterministic
// synthesis of the project's own substrate (preview-only; never mutates the
// project beyond the summary stamp).
//
// ADR-025: read THROUGH this service; tenant scope flows through the project.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectServiceService
{
    Task<Result<ProjectServiceView>> GetServiceAsync(int projectId, CancellationToken ct = default);

    Task<Result<int>> CreateServiceHandoffAsync(CreateServiceHandoffRequest req, CancellationToken ct = default);
    Task<Result<ProjectServiceHandoff>> UpdateHandoffProgressAsync(UpdateHandoffProgressRequest req, CancellationToken ct = default);
    /// <summary>Sign off a handoff (set-once). Gated: startup checklist + training must be complete.</summary>
    Task<Result<ProjectServiceHandoff>> SignOffHandoffAsync(SignOffHandoffRequest req, CancellationToken ct = default);
    Task<Result<ProjectServiceHandoff>> TransitionHandoffAsync(int handoffId, ProjectHandoffStatus newStatus, string? modifiedBy = null, CancellationToken ct = default);

    Task<Result<int>> CreateWarrantyAsync(CreateWarrantyRequest req, CancellationToken ct = default);
    Task<Result<ProjectWarranty>> ActivateWarrantyAsync(ActivateWarrantyRequest req, CancellationToken ct = default);
    Task<Result<ProjectWarranty>> TransitionWarrantyAsync(int warrantyId, ProjectWarrantyStatus newStatus, string? modifiedBy = null, CancellationToken ct = default);

    /// <summary>Compose a data-driven project review + stamp the AI-summary fields.</summary>
    Task<Result<ProjectReviewResult>> GenerateProjectReviewAsync(int projectId, string? generatedBy = null, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectServiceView(
    int ProjectId,
    bool CloseoutReady,
    IReadOnlyList<string> CloseoutBlockers,
    IReadOnlyList<HandoffRow> Handoffs,
    IReadOnlyList<WarrantyRow> Warranties);

public sealed record HandoffRow(int Id, int Number, string? Title, int? InstalledAssetId, string? SerialNumber,
    string? InstallLocation, DateTime? InstallDate, DateTime? CommissioningDate, bool StartupChecklistComplete,
    bool TrainingCompleted, bool CustomerSignoff, ProjectHandoffStatus Status, int? AffectedPhaseId, bool IsSignedOff);

public sealed record WarrantyRow(int Id, int Number, string? Title, int? ProjectServiceHandoffId,
    ProjectWarrantyType WarrantyType, DateTime? StartDate, DateTime? EndDate, string? Provider,
    ProjectWarrantyStatus Status, int ClaimCount);

public sealed record ProjectReviewResult(
    int ProjectId,
    string Narrative,
    decimal? ProjectedMargin,
    decimal? ProjectedMarginPct,
    int OpenChangeRequests,
    int OpenRisks,
    int OpenIssues,
    int QualityBlockers,
    decimal? PercentBilled,
    bool CloseoutReady,
    IReadOnlyList<ReviewChecklistItem> CloseoutChecklist,
    string Model,
    DateTime GeneratedAt);

public sealed record ReviewChecklistItem(string Item, bool Done, string? Detail);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateServiceHandoffRequest(int CustomerProjectId, string? Title = null,
    int? InstalledAssetId = null, string? SerialNumber = null, string? CustomerAssetNumber = null,
    string? InstallLocation = null, DateTime? InstallDate = null, DateTime? CommissioningDate = null,
    string? ServiceContractReference = null, string? PmTemplateReference = null, string? AsBuiltBomReference = null,
    string? AsBuiltDrawingReference = null, int? AffectedPhaseId = null, string? Notes = null, string? CreatedBy = null);

public sealed record UpdateHandoffProgressRequest(int ProjectServiceHandoffId,
    bool? StartupChecklistComplete = null, bool? TrainingCompleted = null, DateTime? InstallDate = null,
    DateTime? CommissioningDate = null, int? InstalledAssetId = null, string? SerialNumber = null,
    string? AsBuiltBomReference = null, string? AsBuiltDrawingReference = null, string? ModifiedBy = null);

public sealed record SignOffHandoffRequest(int ProjectServiceHandoffId, string? CustomerSignoffBy = null,
    string? ModifiedBy = null);

public sealed record CreateWarrantyRequest(int CustomerProjectId, string? Title = null,
    int? ProjectServiceHandoffId = null, ProjectWarrantyType WarrantyType = ProjectWarrantyType.Full,
    DateTime? StartDate = null, DateTime? EndDate = null, string? Provider = null, string? Terms = null,
    string? Notes = null, string? CreatedBy = null);

public sealed record ActivateWarrantyRequest(int ProjectWarrantyId, DateTime? StartDate = null,
    DateTime? EndDate = null, string? ModifiedBy = null);
