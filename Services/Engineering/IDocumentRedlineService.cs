// Sprint 14.3 PR-7 (2026-05-27) — Document redline service interface.
// Structured markup annotations on document versions, linked to ECOs.
// AS9100 §7.5.3 controlled document change management.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering
{
    public interface IDocumentRedlineService
    {
        /// <summary>Create a new redline annotation on a document version.</summary>
        Task<Result<DocumentRedline>> CreateRedlineAsync(
            CreateRedlineRequest request, CancellationToken ct = default);

        /// <summary>Submit a redline for engineering review (Draft → UnderReview).</summary>
        Task<Result<DocumentRedline>> SubmitForReviewAsync(
            int redlineId, string submittedBy, CancellationToken ct = default);

        /// <summary>Approve a redline (UnderReview → Approved).</summary>
        Task<Result<DocumentRedline>> ApproveRedlineAsync(
            int redlineId, string approvedBy, string? approvalNotes = null,
            CancellationToken ct = default);

        /// <summary>Reject a redline (UnderReview → Rejected).</summary>
        Task<Result<DocumentRedline>> RejectRedlineAsync(
            int redlineId, string rejectedBy, string? reviewNotes = null,
            CancellationToken ct = default);

        /// <summary>Supersede a redline when a new revision absorbs the change.</summary>
        Task<Result<DocumentRedline>> SupersedeRedlineAsync(
            int redlineId, string supersededBy, CancellationToken ct = default);

        /// <summary>Get all redlines for a specific document version.</summary>
        Task<IReadOnlyList<DocumentRedline>> GetRedlinesForVersionAsync(
            int documentVersionId, CancellationToken ct = default);

        /// <summary>Get all redlines linked to a specific ECO.</summary>
        Task<IReadOnlyList<DocumentRedline>> GetRedlinesForEcoAsync(
            int ecoId, CancellationToken ct = default);

        /// <summary>Get a single redline by Id.</summary>
        Task<DocumentRedline?> GetAsync(int redlineId, CancellationToken ct = default);
    }

    public sealed record CreateRedlineRequest(
        int CompanyId,
        string RedlineNumber,
        int DocumentVersionId,
        int? EcoId,
        int? ItemId,
        RedlineType Type,
        RedlineSeverity Severity,
        string AffectedArea,
        string? OriginalValue,
        string? NewValue,
        string? MarkupDescription,
        string? SpecificationReference,
        string? DrawingZone,
        string? DrawingView,
        bool AffectsForm,
        bool AffectsFit,
        bool AffectsFunction,
        bool CustomerApprovalRequired,
        bool RequiresFaiRetrigger,
        string CreatedBy);
}
