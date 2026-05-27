// Sprint 14.3 PR-4 (2026-05-27) — Concession service implementation.
// xmin concurrency via MapXminRowVersion at the AppDbContext level
// (HARD LOCK from PR #365 — no IsRowVersion()/bytea).

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering
{
    public class ConcessionService : IConcessionService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ConcessionService> _log;

        public ConcessionService(AppDbContext db, ILogger<ConcessionService> log)
        { _db = db; _log = log; }

        public async Task<Result<Concession>> CreateAsync(CreateConcessionRequest req, CancellationToken ct = default)
        {
            // Tenant-aware uniqueness check on ConcessionNumber within company
            var exists = await _db.Set<Concession>()
                .AnyAsync(c => c.CompanyId == req.CompanyId && c.ConcessionNumber == req.ConcessionNumber, ct);
            if (exists) return Result.Failure<Concession>($"Concession '{req.ConcessionNumber}' already exists in company {req.CompanyId}.");

            var c = new Concession
            {
                CompanyId = req.CompanyId, ConcessionNumber = req.ConcessionNumber,
                Title = req.Title, Type = req.Type, Status = ConcessionStatus.Draft,
                ItemId = req.ItemId, ProductionOrderId = req.ProductionOrderId,
                AffectedQuantity = req.AffectedQuantity,
                AffectedLotSerials = req.AffectedLotSerials,
                CustomerId = req.CustomerId,
                OriginalSpecification = req.OriginalSpecification,
                ActualCondition = req.ActualCondition,
                Justification = req.Justification, Disposition = req.Disposition,
                Description = req.Description, RequestedBy = req.RequestedBy,
                NcrReference = req.NcrReference,
                RequestedAtUtc = DateTime.UtcNow, CreatedBy = req.RequestedBy,
            };
            _db.Set<Concession>().Add(c);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Concession {Number} created (Id={Id}, Type={Type})",
                c.ConcessionNumber, c.Id, c.Type);
            return Result.Success(c);
        }

        public async Task<Result<Concession>> SubmitAsync(int concessionId, string submittedBy, CancellationToken ct = default)
        {
            var c = await _db.Set<Concession>().FindAsync(new object[] { concessionId }, ct);
            if (c is null) return Result.Failure<Concession>("Concession not found.");
            if (c.Status != ConcessionStatus.Draft)
                return Result.Failure<Concession>($"Cannot submit — status is {c.Status}, expected Draft.");
            c.Status = ConcessionStatus.Submitted;
            c.UpdatedAt = DateTime.UtcNow; c.UpdatedBy = submittedBy;
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Concession {Number} submitted by {User}", c.ConcessionNumber, submittedBy);
            return Result.Success(c);
        }

        public async Task<Result<Concession>> AcceptAsync(int concessionId, string acceptedBy, CancellationToken ct = default)
        {
            var c = await _db.Set<Concession>().FindAsync(new object[] { concessionId }, ct);
            if (c is null) return Result.Failure<Concession>("Concession not found.");
            if (c.Status != ConcessionStatus.Submitted && c.Status != ConcessionStatus.CustomerReview)
                return Result.Failure<Concession>($"Cannot accept — status is {c.Status}, expected Submitted or CustomerReview.");
            c.Status = ConcessionStatus.Accepted;
            c.AcceptedBy = acceptedBy; c.AcceptedAtUtc = DateTime.UtcNow;
            c.UpdatedAt = DateTime.UtcNow; c.UpdatedBy = acceptedBy;
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Concession {Number} accepted by {User}", c.ConcessionNumber, acceptedBy);
            return Result.Success(c);
        }

        public async Task<Result<Concession>> RejectAsync(
            int concessionId, string rejectedBy, RejectedDisposition disposition, string reason, CancellationToken ct = default)
        {
            var c = await _db.Set<Concession>().FindAsync(new object[] { concessionId }, ct);
            if (c is null) return Result.Failure<Concession>("Concession not found.");
            if (c.Status != ConcessionStatus.Submitted && c.Status != ConcessionStatus.CustomerReview)
                return Result.Failure<Concession>($"Cannot reject — status is {c.Status}, expected Submitted or CustomerReview.");
            c.Status = ConcessionStatus.Rejected;
            c.RejectedBy = rejectedBy; c.RejectedAtUtc = DateTime.UtcNow;
            c.RejectedDisposition = disposition;
            c.RejectionReason = reason;
            c.UpdatedAt = DateTime.UtcNow; c.UpdatedBy = rejectedBy;
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Concession {Number} rejected by {User}: {Disposition} — {Reason}",
                c.ConcessionNumber, rejectedBy, disposition, reason);
            return Result.Success(c);
        }

        public async Task<Result<Concession>> CloseAsync(int concessionId, string closedBy, CancellationToken ct = default)
        {
            var c = await _db.Set<Concession>().FindAsync(new object[] { concessionId }, ct);
            if (c is null) return Result.Failure<Concession>("Concession not found.");
            if (c.Status != ConcessionStatus.Accepted)
                return Result.Failure<Concession>($"Cannot close — status is {c.Status}, expected Accepted.");
            c.Status = ConcessionStatus.Closed;
            c.UpdatedAt = DateTime.UtcNow; c.UpdatedBy = closedBy;
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Concession {Number} closed by {User}", c.ConcessionNumber, closedBy);
            return Result.Success(c);
        }

        public async Task<Concession?> GetAsync(int concessionId, CancellationToken ct = default)
            => await _db.Set<Concession>()
                .Include(c => c.Item).Include(c => c.Customer)
                .Include(c => c.ProductionOrder).Include(c => c.RelatedDeviation)
                .Include(c => c.OriginatingEcr)
                .FirstOrDefaultAsync(c => c.Id == concessionId, ct);
    }
}
