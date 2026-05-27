using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering
{
    public class WaiverService : IWaiverService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<WaiverService> _log;

        public WaiverService(AppDbContext db, ILogger<WaiverService> log)
        { _db = db; _log = log; }

        public async Task<Result<Waiver>> CreateAsync(CreateWaiverRequest req, CancellationToken ct = default)
        {
            var exists = await _db.Set<Waiver>()
                .AnyAsync(w => w.CompanyId == req.CompanyId && w.WaiverNumber == req.WaiverNumber, ct);
            if (exists) return Result.Failure<Waiver>($"Waiver '{req.WaiverNumber}' already exists.");

            var w = new Waiver
            {
                CompanyId = req.CompanyId, WaiverNumber = req.WaiverNumber,
                Title = req.Title, Type = req.Type, Status = WaiverStatus.Draft,
                ItemId = req.ItemId, CustomerId = req.CustomerId,
                ProductionOrderId = req.ProductionOrderId,
                OriginatingEcrId = req.OriginatingEcrId,
                RelatedDeviationId = req.RelatedDeviationId,
                CustomerContractReference = req.CustomerContractReference,
                MaxQuantity = req.MaxQuantity, EffectiveFromUtc = req.EffectiveFromUtc,
                ExpirationDateUtc = req.ExpirationDateUtc,
                OriginalSpecification = req.OriginalSpecification,
                WaivedCondition = req.WaivedCondition,
                Justification = req.Justification, Disposition = req.Disposition,
                Description = req.Description, RequestedBy = req.RequestedBy,
                RequestedAtUtc = DateTime.UtcNow, CreatedBy = req.RequestedBy,
            };
            _db.Set<Waiver>().Add(w);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Waiver {Number} created (Id={Id})", w.WaiverNumber, w.Id);
            return Result.Success(w);
        }

        public async Task<Result<Waiver>> SubmitAsync(int waiverId, string submittedBy, CancellationToken ct = default)
        {
            var w = await _db.Set<Waiver>().FindAsync(new object[] { waiverId }, ct);
            if (w is null) return Result.Failure<Waiver>("Waiver not found.");
            if (w.Status != WaiverStatus.Draft)
                return Result.Failure<Waiver>($"Cannot submit - status is {w.Status}.");
            w.Status = WaiverStatus.Submitted;
            w.UpdatedAt = DateTime.UtcNow; w.UpdatedBy = submittedBy;
            await _db.SaveChangesAsync(ct);
            return Result.Success(w);
        }

        public async Task<Result<Waiver>> ApproveAsync(int waiverId, string approvedBy, CancellationToken ct = default)
        {
            var w = await _db.Set<Waiver>().FindAsync(new object[] { waiverId }, ct);
            if (w is null) return Result.Failure<Waiver>("Waiver not found.");
            if (w.Status != WaiverStatus.Submitted && w.Status != WaiverStatus.CustomerReview)
                return Result.Failure<Waiver>($"Cannot approve - status is {w.Status}.");
            w.Status = WaiverStatus.Approved;
            w.ApprovedBy = approvedBy; w.ApprovedAtUtc = DateTime.UtcNow;
            w.UpdatedAt = DateTime.UtcNow; w.UpdatedBy = approvedBy;
            if (w.EffectiveFromUtc == null || w.EffectiveFromUtc <= DateTime.UtcNow)
                w.Status = WaiverStatus.Active;
            await _db.SaveChangesAsync(ct);
            return Result.Success(w);
        }

        public async Task<Result<Waiver>> RejectAsync(int waiverId, string rejectedBy, string reason, CancellationToken ct = default)
        {
            var w = await _db.Set<Waiver>().FindAsync(new object[] { waiverId }, ct);
            if (w is null) return Result.Failure<Waiver>("Waiver not found.");
            if (w.Status != WaiverStatus.Submitted && w.Status != WaiverStatus.CustomerReview)
                return Result.Failure<Waiver>($"Cannot reject - status is {w.Status}.");
            w.Status = WaiverStatus.Rejected;
            w.RejectedBy = rejectedBy; w.RejectedAtUtc = DateTime.UtcNow;
            w.RejectionReason = reason;
            w.UpdatedAt = DateTime.UtcNow; w.UpdatedBy = rejectedBy;
            await _db.SaveChangesAsync(ct);
            return Result.Success(w);
        }

        public async Task<Result<Waiver>> ActivateAsync(int waiverId, CancellationToken ct = default)
        {
            var w = await _db.Set<Waiver>().FindAsync(new object[] { waiverId }, ct);
            if (w is null) return Result.Failure<Waiver>("Waiver not found.");
            if (w.Status != WaiverStatus.Approved)
                return Result.Failure<Waiver>($"Cannot activate - status is {w.Status}.");
            w.Status = WaiverStatus.Active; w.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Result.Success(w);
        }

        public async Task<Result<Waiver>> RevokeAsync(int waiverId, string revokedBy, string reason, CancellationToken ct = default)
        {
            var w = await _db.Set<Waiver>().FindAsync(new object[] { waiverId }, ct);
            if (w is null) return Result.Failure<Waiver>("Waiver not found.");
            if (w.Status != WaiverStatus.Active)
                return Result.Failure<Waiver>($"Cannot revoke - status is {w.Status}.");
            w.Status = WaiverStatus.Revoked;
            w.RevokedBy = revokedBy; w.RevokedAtUtc = DateTime.UtcNow;
            w.RevocationReason = reason;
            w.UpdatedAt = DateTime.UtcNow; w.UpdatedBy = revokedBy;
            await _db.SaveChangesAsync(ct);
            return Result.Success(w);
        }

        public async Task<Result<Waiver>> RecordConsumptionAsync(int waiverId, decimal quantity, CancellationToken ct = default)
        {
            var w = await _db.Set<Waiver>().FindAsync(new object[] { waiverId }, ct);
            if (w is null) return Result.Failure<Waiver>("Waiver not found.");
            if (w.Status != WaiverStatus.Active)
                return Result.Failure<Waiver>($"Cannot consume - status is {w.Status}.");
            w.ConsumedQuantity += quantity; w.UpdatedAt = DateTime.UtcNow;
            if (w.MaxQuantity.HasValue && w.ConsumedQuantity >= w.MaxQuantity.Value)
                w.Status = WaiverStatus.Expired;
            await _db.SaveChangesAsync(ct);
            return Result.Success(w);
        }

        public async Task<Waiver?> GetAsync(int waiverId, CancellationToken ct = default)
            => await _db.Set<Waiver>()
                .Include(w => w.Item).Include(w => w.Customer)
                .Include(w => w.ProductionOrder).Include(w => w.RelatedDeviation)
                .FirstOrDefaultAsync(w => w.Id == waiverId, ct);
    }
}
