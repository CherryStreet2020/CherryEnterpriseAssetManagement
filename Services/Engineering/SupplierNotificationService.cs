// Sprint 14.3 PR-5 (2026-05-27) — Supplier notification service implementation.
// xmin concurrency via MapXminRowVersion at the AppDbContext level.
// Integrates with IOutboxWriter for webhook delivery tracking on Send.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering
{
    public class SupplierNotificationService : ISupplierNotificationService
    {
        private readonly AppDbContext _db;
        private readonly IOutboxWriter _outbox;
        private readonly ILogger<SupplierNotificationService> _log;

        public SupplierNotificationService(AppDbContext db, IOutboxWriter outbox,
            ILogger<SupplierNotificationService> log)
        {
            _db = db;
            _outbox = outbox;
            _log = log;
        }

        public async Task<Result<SupplierProcessChangeNotification>> CreateAsync(
            CreateSupplierPcnRequest req, CancellationToken ct = default)
        {
            var exists = await _db.Set<SupplierProcessChangeNotification>()
                .AnyAsync(p => p.CompanyId == req.CompanyId
                    && p.PcnNumber == req.PcnNumber, ct);
            if (exists)
                return Result.Failure<SupplierProcessChangeNotification>(
                    $"Supplier PCN '{req.PcnNumber}' already exists in company {req.CompanyId}.");

            var pcn = new SupplierProcessChangeNotification
            {
                CompanyId = req.CompanyId,
                PcnNumber = req.PcnNumber,
                Title = req.Title,
                Type = req.Type,
                Status = PcnStatus.Draft,
                VendorId = req.VendorId,
                ItemId = req.ItemId,
                OriginatingEcrId = req.OriginatingEcrId,
                ChangeDescription = req.ChangeDescription,
                ImpactDescription = req.ImpactDescription,
                ProposedEffectiveDate = req.ProposedEffectiveDate,
                CurrentSpecification = req.CurrentSpecification,
                ProposedSpecification = req.ProposedSpecification,
                AffectsForm = req.AffectsForm,
                AffectsFit = req.AffectsFit,
                AffectsFunction = req.AffectsFunction,
                SafetyImpact = req.SafetyImpact,
                FirstArticleRequired = req.FirstArticleRequired,
                PpapRequired = req.PpapRequired,
                QualityPlanUpdateRequired = req.QualityPlanUpdateRequired,
                SampleQuantityRequired = req.SampleQuantityRequired,
                ApprovalRequired = req.ApprovalRequired,
                DeliveryMethod = req.DeliveryMethod,
                RequiredResponseDate = req.RequiredResponseDate,
                SupplierContactName = req.SupplierContactName,
                SupplierContactEmail = req.SupplierContactEmail,
                Description = req.Description,
                CreatedBy = req.CreatedBy,
            };

            _db.Set<SupplierProcessChangeNotification>().Add(pcn);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("SupplierPCN {Number} created (Id={Id}, Type={Type})",
                pcn.PcnNumber, pcn.Id, pcn.Type);
            return Result.Success(pcn);
        }

        public async Task<Result<SupplierProcessChangeNotification>> MarkPendingAsync(
            int pcnId, string approvedBy, CancellationToken ct = default)
        {
            var pcn = await _db.Set<SupplierProcessChangeNotification>().FindAsync(new object[] { pcnId }, ct);
            if (pcn is null) return Result.Failure<SupplierProcessChangeNotification>("Supplier PCN not found.");
            if (pcn.Status != PcnStatus.Draft)
                return Result.Failure<SupplierProcessChangeNotification>(
                    $"Cannot mark pending — status is {pcn.Status}, expected Draft.");

            pcn.Status = PcnStatus.Pending;
            pcn.UpdatedAt = DateTime.UtcNow;
            pcn.UpdatedBy = approvedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("SupplierPCN {Number} marked pending by {User}", pcn.PcnNumber, approvedBy);
            return Result.Success(pcn);
        }

        public async Task<Result<SupplierProcessChangeNotification>> SendAsync(
            int pcnId, string sentBy, CancellationToken ct = default)
        {
            var pcn = await _db.Set<SupplierProcessChangeNotification>().FindAsync(new object[] { pcnId }, ct);
            if (pcn is null) return Result.Failure<SupplierProcessChangeNotification>("Supplier PCN not found.");
            if (pcn.Status != PcnStatus.Pending)
                return Result.Failure<SupplierProcessChangeNotification>(
                    $"Cannot send — status is {pcn.Status}, expected Pending.");

            var correlationId = Guid.NewGuid().ToString("N");
            pcn.Status = PcnStatus.SentToSupplier;
            pcn.SentBy = sentBy;
            pcn.SentAtUtc = DateTime.UtcNow;
            pcn.OutboxCorrelationId = correlationId;
            pcn.UpdatedAt = DateTime.UtcNow;
            pcn.UpdatedBy = sentBy;

            // Fire outbox event for delivery tracking
            await _outbox.EnqueueAsync(pcn.CompanyId, null,
                new SupplierPcnSentV1(pcn.Id, pcn.PcnNumber, pcn.Type.ToString(),
                    pcn.VendorId, pcn.ItemId, pcn.DeliveryMethod.ToString()),
                correlationId);

            await _db.SaveChangesAsync(ct);

            _log.LogInformation("SupplierPCN {Number} sent by {User} via {Method} (correlation={Corr})",
                pcn.PcnNumber, sentBy, pcn.DeliveryMethod, correlationId);
            return Result.Success(pcn);
        }

        public async Task<Result<SupplierProcessChangeNotification>> RecordAcknowledgementAsync(
            int pcnId, string supplierRespondent, CancellationToken ct = default)
        {
            var pcn = await _db.Set<SupplierProcessChangeNotification>().FindAsync(new object[] { pcnId }, ct);
            if (pcn is null) return Result.Failure<SupplierProcessChangeNotification>("Supplier PCN not found.");
            if (pcn.Status != PcnStatus.SentToSupplier)
                return Result.Failure<SupplierProcessChangeNotification>(
                    $"Cannot acknowledge — status is {pcn.Status}, expected SentToSupplier.");

            pcn.Status = PcnStatus.SupplierAcknowledged;
            pcn.SupplierRespondent = supplierRespondent;
            pcn.SupplierAcknowledgedAtUtc = DateTime.UtcNow;
            pcn.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("SupplierPCN {Number} acknowledged by supplier ({Respondent})",
                pcn.PcnNumber, supplierRespondent);
            return Result.Success(pcn);
        }

        public async Task<Result<SupplierProcessChangeNotification>> RecordImpactAssessmentAsync(
            int pcnId, string assessment, decimal? costImpact = null,
            int? leadTimeImpactDays = null, CancellationToken ct = default)
        {
            var pcn = await _db.Set<SupplierProcessChangeNotification>().FindAsync(new object[] { pcnId }, ct);
            if (pcn is null) return Result.Failure<SupplierProcessChangeNotification>("Supplier PCN not found.");
            if (pcn.Status != PcnStatus.SupplierAcknowledged)
                return Result.Failure<SupplierProcessChangeNotification>(
                    $"Cannot record impact assessment — status is {pcn.Status}, expected SupplierAcknowledged.");

            pcn.Status = PcnStatus.ImpactAssessmentReceived;
            pcn.SupplierImpactAssessment = assessment;
            pcn.SupplierEstimatedCostImpact = costImpact;
            pcn.SupplierEstimatedLeadTimeImpactDays = leadTimeImpactDays;
            pcn.ImpactAssessmentReceivedAtUtc = DateTime.UtcNow;
            pcn.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("SupplierPCN {Number} impact assessment received (cost={Cost}, LT={LT}d)",
                pcn.PcnNumber, costImpact, leadTimeImpactDays);
            return Result.Success(pcn);
        }

        public async Task<Result<SupplierProcessChangeNotification>> ApproveAsync(
            int pcnId, string approvedBy, CancellationToken ct = default)
        {
            var pcn = await _db.Set<SupplierProcessChangeNotification>().FindAsync(new object[] { pcnId }, ct);
            if (pcn is null) return Result.Failure<SupplierProcessChangeNotification>("Supplier PCN not found.");
            if (pcn.Status != PcnStatus.ImpactAssessmentReceived)
                return Result.Failure<SupplierProcessChangeNotification>(
                    $"Cannot approve — status is {pcn.Status}, expected ImpactAssessmentReceived.");

            pcn.Status = PcnStatus.Approved;
            pcn.ApprovedBy = approvedBy;
            pcn.ApprovedAtUtc = DateTime.UtcNow;
            pcn.UpdatedAt = DateTime.UtcNow;
            pcn.UpdatedBy = approvedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("SupplierPCN {Number} approved by {User}", pcn.PcnNumber, approvedBy);
            return Result.Success(pcn);
        }

        public async Task<Result<SupplierProcessChangeNotification>> RejectAsync(
            int pcnId, string rejectedBy, string reason, CancellationToken ct = default)
        {
            var pcn = await _db.Set<SupplierProcessChangeNotification>().FindAsync(new object[] { pcnId }, ct);
            if (pcn is null) return Result.Failure<SupplierProcessChangeNotification>("Supplier PCN not found.");
            if (pcn.Status != PcnStatus.ImpactAssessmentReceived)
                return Result.Failure<SupplierProcessChangeNotification>(
                    $"Cannot reject — status is {pcn.Status}, expected ImpactAssessmentReceived.");

            pcn.Status = PcnStatus.Rejected;
            pcn.RejectedBy = rejectedBy;
            pcn.RejectedAtUtc = DateTime.UtcNow;
            pcn.RejectionReason = reason;
            pcn.UpdatedAt = DateTime.UtcNow;
            pcn.UpdatedBy = rejectedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("SupplierPCN {Number} rejected by {User}: {Reason}",
                pcn.PcnNumber, rejectedBy, reason);
            return Result.Success(pcn);
        }

        public async Task<Result<SupplierProcessChangeNotification>> CloseAsync(
            int pcnId, string closedBy, string? firstConformingShipmentRef = null,
            CancellationToken ct = default)
        {
            var pcn = await _db.Set<SupplierProcessChangeNotification>().FindAsync(new object[] { pcnId }, ct);
            if (pcn is null) return Result.Failure<SupplierProcessChangeNotification>("Supplier PCN not found.");
            if (pcn.Status != PcnStatus.Approved)
                return Result.Failure<SupplierProcessChangeNotification>(
                    $"Cannot close — status is {pcn.Status}, expected Approved.");

            pcn.Status = PcnStatus.Closed;
            pcn.FirstConformingShipmentRef = firstConformingShipmentRef;
            pcn.VerifiedAtUtc = DateTime.UtcNow;
            pcn.VerifiedBy = closedBy;
            pcn.UpdatedAt = DateTime.UtcNow;
            pcn.UpdatedBy = closedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("SupplierPCN {Number} closed by {User} (1st shipment ref={Ref})",
                pcn.PcnNumber, closedBy, firstConformingShipmentRef ?? "—");
            return Result.Success(pcn);
        }

        public async Task<SupplierProcessChangeNotification?> GetAsync(
            int pcnId, CancellationToken ct = default)
            => await _db.Set<SupplierProcessChangeNotification>()
                .Include(p => p.Vendor)
                .Include(p => p.Item)
                .Include(p => p.OriginatingEcr)
                .FirstOrDefaultAsync(p => p.Id == pcnId, ct);

        public async Task<IReadOnlyList<SupplierProcessChangeNotification>> GetForItemAsync(
            int itemId, bool includeClosed = false, CancellationToken ct = default)
        {
            var query = _db.Set<SupplierProcessChangeNotification>()
                .Where(p => p.ItemId == itemId);

            if (!includeClosed)
                query = query.Where(p =>
                    p.Status != PcnStatus.Closed &&
                    p.Status != PcnStatus.Cancelled);

            return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        }
    }
}
