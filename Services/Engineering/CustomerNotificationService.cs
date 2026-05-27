// Sprint 14.3 PR-5 (2026-05-27) — Customer notification service implementation.
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
    public class CustomerNotificationService : ICustomerNotificationService
    {
        private readonly AppDbContext _db;
        private readonly IOutboxWriter _outbox;
        private readonly ILogger<CustomerNotificationService> _log;

        public CustomerNotificationService(AppDbContext db, IOutboxWriter outbox,
            ILogger<CustomerNotificationService> log)
        {
            _db = db;
            _outbox = outbox;
            _log = log;
        }

        public async Task<Result<CustomerNotice>> CreateAsync(
            CreateCustomerNoticeRequest req, CancellationToken ct = default)
        {
            var exists = await _db.Set<CustomerNotice>()
                .AnyAsync(n => n.CompanyId == req.CompanyId
                    && n.NoticeNumber == req.NoticeNumber, ct);
            if (exists)
                return Result.Failure<CustomerNotice>(
                    $"Customer notice '{req.NoticeNumber}' already exists in company {req.CompanyId}.");

            var notice = new CustomerNotice
            {
                CompanyId = req.CompanyId,
                NoticeNumber = req.NoticeNumber,
                Title = req.Title,
                Type = req.Type,
                Status = CustomerNoticeStatus.Draft,
                CustomerId = req.CustomerId,
                ItemId = req.ItemId,
                OriginatingEcrId = req.OriginatingEcrId,
                OriginatingDeviationId = req.OriginatingDeviationId,
                OriginatingWaiverId = req.OriginatingWaiverId,
                OriginatingConcessionId = req.OriginatingConcessionId,
                ChangeDescription = req.ChangeDescription,
                ImpactDescription = req.ImpactDescription,
                ChangeEffectiveDate = req.ChangeEffectiveDate,
                AffectedSalesOrderReferences = req.AffectedSalesOrderReferences,
                AffectedContractReferences = req.AffectedContractReferences,
                AffectsForm = req.AffectsForm,
                AffectsFit = req.AffectsFit,
                AffectsFunction = req.AffectsFunction,
                SafetyImpact = req.SafetyImpact,
                DeliveryMethod = req.DeliveryMethod,
                RequiredResponseDate = req.RequiredResponseDate,
                CustomerContactName = req.CustomerContactName,
                CustomerContactEmail = req.CustomerContactEmail,
                Description = req.Description,
                CreatedBy = req.CreatedBy,
            };

            _db.Set<CustomerNotice>().Add(notice);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CustomerNotice {Number} created (Id={Id}, Type={Type})",
                notice.NoticeNumber, notice.Id, notice.Type);
            return Result.Success(notice);
        }

        public async Task<Result<CustomerNotice>> MarkPendingAsync(
            int noticeId, string approvedBy, CancellationToken ct = default)
        {
            var notice = await _db.Set<CustomerNotice>().FindAsync(new object[] { noticeId }, ct);
            if (notice is null) return Result.Failure<CustomerNotice>("Customer notice not found.");
            if (notice.Status != CustomerNoticeStatus.Draft)
                return Result.Failure<CustomerNotice>(
                    $"Cannot mark pending — status is {notice.Status}, expected Draft.");

            notice.Status = CustomerNoticeStatus.Pending;
            notice.UpdatedAt = DateTime.UtcNow;
            notice.UpdatedBy = approvedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CustomerNotice {Number} marked pending by {User}",
                notice.NoticeNumber, approvedBy);
            return Result.Success(notice);
        }

        public async Task<Result<CustomerNotice>> SendAsync(
            int noticeId, string sentBy, CancellationToken ct = default)
        {
            var notice = await _db.Set<CustomerNotice>().FindAsync(new object[] { noticeId }, ct);
            if (notice is null) return Result.Failure<CustomerNotice>("Customer notice not found.");
            if (notice.Status != CustomerNoticeStatus.Pending)
                return Result.Failure<CustomerNotice>(
                    $"Cannot send — status is {notice.Status}, expected Pending.");

            var correlationId = Guid.NewGuid().ToString("N");
            notice.Status = CustomerNoticeStatus.Sent;
            notice.SentBy = sentBy;
            notice.SentAtUtc = DateTime.UtcNow;
            notice.OutboxCorrelationId = correlationId;
            notice.UpdatedAt = DateTime.UtcNow;
            notice.UpdatedBy = sentBy;

            // Fire outbox event for delivery tracking
            await _outbox.EnqueueAsync(notice.CompanyId, null,
                new CustomerNoticeSentV1(notice.Id, notice.NoticeNumber, notice.Type.ToString(),
                    notice.CustomerId, notice.ItemId, notice.DeliveryMethod.ToString()),
                correlationId);

            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CustomerNotice {Number} sent by {User} via {Method} (correlation={Corr})",
                notice.NoticeNumber, sentBy, notice.DeliveryMethod, correlationId);
            return Result.Success(notice);
        }

        public async Task<Result<CustomerNotice>> RecordAcknowledgementAsync(
            int noticeId, string acknowledgedBy, string? responseText = null,
            CancellationToken ct = default)
        {
            var notice = await _db.Set<CustomerNotice>().FindAsync(new object[] { noticeId }, ct);
            if (notice is null) return Result.Failure<CustomerNotice>("Customer notice not found.");
            if (notice.Status != CustomerNoticeStatus.Sent &&
                notice.Status != CustomerNoticeStatus.Resolved)
                return Result.Failure<CustomerNotice>(
                    $"Cannot acknowledge — status is {notice.Status}, expected Sent or Resolved.");

            notice.Status = CustomerNoticeStatus.Acknowledged;
            notice.AcknowledgedBy = acknowledgedBy;
            notice.AcknowledgedAtUtc = DateTime.UtcNow;
            notice.CustomerRespondent = acknowledgedBy;
            notice.CustomerResponseDateUtc = DateTime.UtcNow;
            notice.CustomerResponseText = responseText;
            notice.UpdatedAt = DateTime.UtcNow;
            notice.UpdatedBy = acknowledgedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CustomerNotice {Number} acknowledged by {User}",
                notice.NoticeNumber, acknowledgedBy);
            return Result.Success(notice);
        }

        public async Task<Result<CustomerNotice>> RecordDisputeAsync(
            int noticeId, string disputeReason, CancellationToken ct = default)
        {
            var notice = await _db.Set<CustomerNotice>().FindAsync(new object[] { noticeId }, ct);
            if (notice is null) return Result.Failure<CustomerNotice>("Customer notice not found.");
            if (notice.Status != CustomerNoticeStatus.Sent)
                return Result.Failure<CustomerNotice>(
                    $"Cannot dispute — status is {notice.Status}, expected Sent.");

            notice.Status = CustomerNoticeStatus.Disputed;
            notice.DisputeReason = disputeReason;
            notice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CustomerNotice {Number} disputed: {Reason}",
                notice.NoticeNumber, disputeReason);
            return Result.Success(notice);
        }

        public async Task<Result<CustomerNotice>> ResolveDisputeAsync(
            int noticeId, string resolvedBy, string resolution, CancellationToken ct = default)
        {
            var notice = await _db.Set<CustomerNotice>().FindAsync(new object[] { noticeId }, ct);
            if (notice is null) return Result.Failure<CustomerNotice>("Customer notice not found.");
            if (notice.Status != CustomerNoticeStatus.Disputed)
                return Result.Failure<CustomerNotice>(
                    $"Cannot resolve — status is {notice.Status}, expected Disputed.");

            notice.Status = CustomerNoticeStatus.Resolved;
            notice.DisputeResolution = resolution;
            notice.DisputeResolvedAtUtc = DateTime.UtcNow;
            notice.DisputeResolvedBy = resolvedBy;
            notice.UpdatedAt = DateTime.UtcNow;
            notice.UpdatedBy = resolvedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CustomerNotice {Number} dispute resolved by {User}: {Resolution}",
                notice.NoticeNumber, resolvedBy, resolution);
            return Result.Success(notice);
        }

        public async Task<Result<CustomerNotice>> CloseAsync(
            int noticeId, string closedBy, CancellationToken ct = default)
        {
            var notice = await _db.Set<CustomerNotice>().FindAsync(new object[] { noticeId }, ct);
            if (notice is null) return Result.Failure<CustomerNotice>("Customer notice not found.");
            if (notice.Status != CustomerNoticeStatus.Acknowledged &&
                notice.Status != CustomerNoticeStatus.Resolved)
                return Result.Failure<CustomerNotice>(
                    $"Cannot close — status is {notice.Status}, expected Acknowledged or Resolved.");

            notice.Status = CustomerNoticeStatus.Closed;
            notice.UpdatedAt = DateTime.UtcNow;
            notice.UpdatedBy = closedBy;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("CustomerNotice {Number} closed by {User}",
                notice.NoticeNumber, closedBy);
            return Result.Success(notice);
        }

        public async Task<CustomerNotice?> GetAsync(int noticeId, CancellationToken ct = default)
            => await _db.Set<CustomerNotice>()
                .Include(n => n.Customer)
                .Include(n => n.Item)
                .Include(n => n.OriginatingEcr)
                .Include(n => n.OriginatingDeviation)
                .Include(n => n.OriginatingWaiver)
                .Include(n => n.OriginatingConcession)
                .FirstOrDefaultAsync(n => n.Id == noticeId, ct);

        public async Task<IReadOnlyList<CustomerNotice>> GetForItemAsync(
            int itemId, bool includeClosed = false, CancellationToken ct = default)
        {
            var query = _db.Set<CustomerNotice>()
                .Where(n => n.ItemId == itemId);

            if (!includeClosed)
                query = query.Where(n =>
                    n.Status != CustomerNoticeStatus.Closed &&
                    n.Status != CustomerNoticeStatus.Cancelled);

            return await query.OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
        }
    }
}
