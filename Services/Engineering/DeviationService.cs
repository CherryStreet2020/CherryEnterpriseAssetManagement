// Sprint 14.3 PR-2 (2026-05-27) — Deviation service implementation.
// xmin concurrency via MapXminRowVersion at the AppDbContext level
// (HARD LOCK from PR #365 — no IsRowVersion()/bytea).

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering
{
    public class DeviationService : IDeviationService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<DeviationService> _log;

        public DeviationService(AppDbContext db, ILogger<DeviationService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<Result<Deviation>> CreateAsync(
            CreateDeviationRequest req, CancellationToken ct = default)
        {
            // Tenant-aware uniqueness check on DeviationNumber within company
            var exists = await _db.Set<Deviation>()
                .AnyAsync(d => d.CompanyId == req.CompanyId
                    && d.DeviationNumber == req.DeviationNumber, ct);
            if (exists)
                return Result.Failure<Deviation>(
                    $"Deviation '{req.DeviationNumber}' already exists in company {req.CompanyId}.");

            var dev = new Deviation
            {
                CompanyId = req.CompanyId,
                DeviationNumber = req.DeviationNumber,
                Title = req.Title,
                Type = req.Type,
                Status = DeviationStatus.Draft,
                ItemId = req.ItemId,
                ProductionOrderId = req.ProductionOrderId,
                OriginatingEcrId = req.OriginatingEcrId,
                MaxQuantity = req.MaxQuantity,
                EffectiveFromUtc = req.EffectiveFromUtc,
                ExpirationDateUtc = req.ExpirationDateUtc,
                MaxProductionOrders = req.MaxProductionOrders,
                AffectsForm = req.AffectsForm,
                AffectsFit = req.AffectsFit,
                AffectsFunction = req.AffectsFunction,
                SafetyImpact = req.SafetyImpact,
                CustomerApprovalRequired = req.CustomerApprovalRequired,
                OriginalSpecification = req.OriginalSpecification,
                DeviatedCondition = req.DeviatedCondition,
                Justification = req.Justification,
                Disposition = req.Disposition,
                Description = req.Description,
                RequestedBy = req.RequestedBy,
                RequestedAtUtc = DateTime.UtcNow,
                CreatedBy = req.RequestedBy,
            };

            _db.Set<Deviation>().Add(dev);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Deviation {Number} created (Id={Id}, Type={Type})",
                dev.DeviationNumber, dev.Id, dev.Type);
            return Result.Success(dev);
        }

        public async Task<Result<Deviation>> SubmitAsync(
            int deviationId, string submittedBy, CancellationToken ct = default)
        {
            var dev = await _db.Set<Deviation>().FindAsync(new object[] { deviationId }, ct);
            if (dev is null) return Result.Failure<Deviation>("Deviation not found.");
            if (dev.Status != DeviationStatus.Draft)
                return Result.Failure<Deviation>(
                    $"Cannot submit — status is {dev.Status}, expected Draft.");

            dev.Status = DeviationStatus.Submitted;
            dev.UpdatedAt = DateTime.UtcNow;
            dev.UpdatedBy = submittedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Deviation {Number} submitted by {User}", dev.DeviationNumber, submittedBy);
            return Result.Success(dev);
        }

        public async Task<Result<Deviation>> ApproveAsync(
            int deviationId, string approvedBy, CancellationToken ct = default)
        {
            var dev = await _db.Set<Deviation>().FindAsync(new object[] { deviationId }, ct);
            if (dev is null) return Result.Failure<Deviation>("Deviation not found.");
            if (dev.Status != DeviationStatus.Submitted && dev.Status != DeviationStatus.UnderReview)
                return Result.Failure<Deviation>(
                    $"Cannot approve — status is {dev.Status}, expected Submitted or UnderReview.");

            dev.Status = DeviationStatus.Approved;
            dev.ApprovedBy = approvedBy;
            dev.ApprovedAtUtc = DateTime.UtcNow;
            dev.UpdatedAt = DateTime.UtcNow;
            dev.UpdatedBy = approvedBy;

            // Auto-activate if effective date is now or past
            if (dev.EffectiveFromUtc == null || dev.EffectiveFromUtc <= DateTime.UtcNow)
            {
                dev.Status = DeviationStatus.Active;
                _log.LogInformation("Deviation {Number} auto-activated (effective date is now or past)",
                    dev.DeviationNumber);
            }

            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Deviation {Number} approved by {User}", dev.DeviationNumber, approvedBy);
            return Result.Success(dev);
        }

        public async Task<Result<Deviation>> RejectAsync(
            int deviationId, string rejectedBy, string reason, CancellationToken ct = default)
        {
            var dev = await _db.Set<Deviation>().FindAsync(new object[] { deviationId }, ct);
            if (dev is null) return Result.Failure<Deviation>("Deviation not found.");
            if (dev.Status != DeviationStatus.Submitted && dev.Status != DeviationStatus.UnderReview)
                return Result.Failure<Deviation>(
                    $"Cannot reject — status is {dev.Status}, expected Submitted or UnderReview.");

            dev.Status = DeviationStatus.Rejected;
            dev.RejectedBy = rejectedBy;
            dev.RejectedAtUtc = DateTime.UtcNow;
            dev.RejectionReason = reason;
            dev.UpdatedAt = DateTime.UtcNow;
            dev.UpdatedBy = rejectedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Deviation {Number} rejected by {User}: {Reason}",
                dev.DeviationNumber, rejectedBy, reason);
            return Result.Success(dev);
        }

        public async Task<Result<Deviation>> ActivateAsync(
            int deviationId, CancellationToken ct = default)
        {
            var dev = await _db.Set<Deviation>().FindAsync(new object[] { deviationId }, ct);
            if (dev is null) return Result.Failure<Deviation>("Deviation not found.");
            if (dev.Status != DeviationStatus.Approved)
                return Result.Failure<Deviation>(
                    $"Cannot activate — status is {dev.Status}, expected Approved.");

            dev.Status = DeviationStatus.Active;
            dev.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Deviation {Number} activated", dev.DeviationNumber);
            return Result.Success(dev);
        }

        public async Task<Result<Deviation>> CloseAsync(
            int deviationId, string closedBy, CancellationToken ct = default)
        {
            var dev = await _db.Set<Deviation>().FindAsync(new object[] { deviationId }, ct);
            if (dev is null) return Result.Failure<Deviation>("Deviation not found.");
            if (dev.Status != DeviationStatus.Active)
                return Result.Failure<Deviation>(
                    $"Cannot close — status is {dev.Status}, expected Active.");

            dev.Status = DeviationStatus.Closed;
            dev.UpdatedAt = DateTime.UtcNow;
            dev.UpdatedBy = closedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Deviation {Number} closed by {User}", dev.DeviationNumber, closedBy);
            return Result.Success(dev);
        }

        public async Task<Result<Deviation>> RecordConsumptionAsync(
            int deviationId, decimal quantity, CancellationToken ct = default)
        {
            var dev = await _db.Set<Deviation>().FindAsync(new object[] { deviationId }, ct);
            if (dev is null) return Result.Failure<Deviation>("Deviation not found.");
            if (dev.Status != DeviationStatus.Active)
                return Result.Failure<Deviation>(
                    $"Cannot consume — status is {dev.Status}, expected Active.");

            dev.ConsumedQuantity += quantity;
            dev.UpdatedAt = DateTime.UtcNow;

            // Auto-expire if max quantity reached
            if (dev.MaxQuantity.HasValue && dev.ConsumedQuantity >= dev.MaxQuantity.Value)
            {
                dev.Status = DeviationStatus.Expired;
                _log.LogInformation("Deviation {Number} auto-expired — consumed {Consumed} >= max {Max}",
                    dev.DeviationNumber, dev.ConsumedQuantity, dev.MaxQuantity);
            }

            await _db.SaveChangesAsync(ct);
            return Result.Success(dev);
        }

        public async Task<Deviation?> GetAsync(int deviationId, CancellationToken ct = default)
            => await _db.Set<Deviation>()
                .Include(d => d.Item)
                .Include(d => d.ProductionOrder)
                .Include(d => d.OriginatingEcr)
                .FirstOrDefaultAsync(d => d.Id == deviationId, ct);

        public async Task<IReadOnlyList<Deviation>> GetForItemAsync(
            int itemId, bool includeExpired = false, CancellationToken ct = default)
        {
            var query = _db.Set<Deviation>()
                .Where(d => d.ItemId == itemId);

            if (!includeExpired)
                query = query.Where(d =>
                    d.Status != DeviationStatus.Expired &&
                    d.Status != DeviationStatus.Closed &&
                    d.Status != DeviationStatus.Cancelled &&
                    d.Status != DeviationStatus.Rejected);

            return await query.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
        }
    }
}
