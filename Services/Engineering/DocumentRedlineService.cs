// Sprint 14.3 PR-7 (2026-05-27) — DocumentRedlineService implementation.
// xmin concurrency via MapXminRowVersion at the AppDbContext level.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering
{
    public class DocumentRedlineService : IDocumentRedlineService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<DocumentRedlineService> _log;

        public DocumentRedlineService(AppDbContext db, ILogger<DocumentRedlineService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<Result<DocumentRedline>> CreateRedlineAsync(
            CreateRedlineRequest req, CancellationToken ct = default)
        {
            // ---- Duplicate check ----
            var exists = await _db.Set<DocumentRedline>()
                .AnyAsync(r => r.CompanyId == req.CompanyId && r.RedlineNumber == req.RedlineNumber, ct);
            if (exists)
                return Result.Failure<DocumentRedline>(
                    $"Redline '{req.RedlineNumber}' already exists in company {req.CompanyId}.");

            // ---- Validate document version exists ----
            var dvExists = await _db.Set<DocumentVersion>()
                .AnyAsync(dv => dv.Id == req.DocumentVersionId, ct);
            if (!dvExists)
                return Result.Failure<DocumentRedline>(
                    $"Document version {req.DocumentVersionId} not found.");

            var redline = new DocumentRedline
            {
                CompanyId = req.CompanyId,
                RedlineNumber = req.RedlineNumber,
                DocumentVersionId = req.DocumentVersionId,
                EcoId = req.EcoId,
                ItemId = req.ItemId,
                Status = RedlineStatus.Draft,
                Type = req.Type,
                Severity = req.Severity,
                AffectedArea = req.AffectedArea,
                OriginalValue = req.OriginalValue,
                NewValue = req.NewValue,
                MarkupDescription = req.MarkupDescription,
                SpecificationReference = req.SpecificationReference,
                DrawingZone = req.DrawingZone,
                DrawingView = req.DrawingView,
                AffectsForm = req.AffectsForm,
                AffectsFit = req.AffectsFit,
                AffectsFunction = req.AffectsFunction,
                CustomerApprovalRequired = req.CustomerApprovalRequired,
                RequiresFaiRetrigger = req.RequiresFaiRetrigger,
                CreatedBy = req.CreatedBy,
            };

            _db.Set<DocumentRedline>().Add(redline);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Created redline {Number} on doc version {DocVer}: {Type} {Severity} — '{Area}' {Old} → {New}",
                redline.RedlineNumber, redline.DocumentVersionId,
                redline.Type, redline.Severity,
                redline.AffectedArea, redline.OriginalValue, redline.NewValue);

            return Result.Success(redline);
        }

        public async Task<Result<DocumentRedline>> SubmitForReviewAsync(
            int redlineId, string submittedBy, CancellationToken ct = default)
        {
            var r = await _db.Set<DocumentRedline>().FindAsync(new object[] { redlineId }, ct);
            if (r == null) return Result.Failure<DocumentRedline>($"Redline {redlineId} not found.");
            if (r.Status != RedlineStatus.Draft)
                return Result.Failure<DocumentRedline>(
                    $"Cannot submit — status is {r.Status}, expected Draft.");

            r.Status = RedlineStatus.UnderReview;
            r.UpdatedAt = DateTime.UtcNow;
            r.UpdatedBy = submittedBy;
            await _db.SaveChangesAsync(ct);
            return Result.Success(r);
        }

        public async Task<Result<DocumentRedline>> ApproveRedlineAsync(
            int redlineId, string approvedBy, string? approvalNotes = null,
            CancellationToken ct = default)
        {
            var r = await _db.Set<DocumentRedline>().FindAsync(new object[] { redlineId }, ct);
            if (r == null) return Result.Failure<DocumentRedline>($"Redline {redlineId} not found.");
            if (r.Status != RedlineStatus.UnderReview)
                return Result.Failure<DocumentRedline>(
                    $"Cannot approve — status is {r.Status}, expected UnderReview.");

            r.Status = RedlineStatus.Approved;
            r.ApprovedBy = approvedBy;
            r.ApprovedAtUtc = DateTime.UtcNow;
            r.ApprovalNotes = approvalNotes;
            r.UpdatedAt = DateTime.UtcNow;
            r.UpdatedBy = approvedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Redline {Number} approved by {By}", r.RedlineNumber, approvedBy);
            return Result.Success(r);
        }

        public async Task<Result<DocumentRedline>> RejectRedlineAsync(
            int redlineId, string rejectedBy, string? reviewNotes = null,
            CancellationToken ct = default)
        {
            var r = await _db.Set<DocumentRedline>().FindAsync(new object[] { redlineId }, ct);
            if (r == null) return Result.Failure<DocumentRedline>($"Redline {redlineId} not found.");
            if (r.Status != RedlineStatus.UnderReview)
                return Result.Failure<DocumentRedline>(
                    $"Cannot reject — status is {r.Status}, expected UnderReview.");

            r.Status = RedlineStatus.Rejected;
            r.ReviewedBy = rejectedBy;
            r.ReviewedAtUtc = DateTime.UtcNow;
            r.ReviewNotes = reviewNotes;
            r.UpdatedAt = DateTime.UtcNow;
            r.UpdatedBy = rejectedBy;
            await _db.SaveChangesAsync(ct);
            return Result.Success(r);
        }

        public async Task<Result<DocumentRedline>> SupersedeRedlineAsync(
            int redlineId, string supersededBy, CancellationToken ct = default)
        {
            var r = await _db.Set<DocumentRedline>().FindAsync(new object[] { redlineId }, ct);
            if (r == null) return Result.Failure<DocumentRedline>($"Redline {redlineId} not found.");
            if (r.Status == RedlineStatus.Superseded || r.Status == RedlineStatus.Archived)
                return Result.Failure<DocumentRedline>(
                    $"Redline already {r.Status}.");

            r.Status = RedlineStatus.Superseded;
            r.UpdatedAt = DateTime.UtcNow;
            r.UpdatedBy = supersededBy;
            await _db.SaveChangesAsync(ct);
            return Result.Success(r);
        }

        public async Task<IReadOnlyList<DocumentRedline>> GetRedlinesForVersionAsync(
            int documentVersionId, CancellationToken ct = default)
            => await _db.Set<DocumentRedline>()
                .Where(r => r.DocumentVersionId == documentVersionId)
                .OrderBy(r => r.Id)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<DocumentRedline>> GetRedlinesForEcoAsync(
            int ecoId, CancellationToken ct = default)
            => await _db.Set<DocumentRedline>()
                .Where(r => r.EcoId == ecoId)
                .OrderBy(r => r.Id)
                .ToListAsync(ct);

        public async Task<DocumentRedline?> GetAsync(int redlineId, CancellationToken ct = default)
            => await _db.Set<DocumentRedline>().FindAsync(new object[] { redlineId }, ct);
    }
}
