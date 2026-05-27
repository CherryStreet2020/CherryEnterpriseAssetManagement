// Sprint 14.3 PR-5 (2026-05-27) — Customer notification service interface.
//
// Outbound notification lifecycle for customer change communications.
// Lifecycle: Draft → Pending → Sent → Acknowledged → Closed
//                                   ↘ Disputed → Resolved → Closed
// Integrates with webhook outbox for delivery tracking.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering
{
    public interface ICustomerNotificationService
    {
        /// <summary>Create a new customer notice in Draft status.</summary>
        Task<Result<CustomerNotice>> CreateAsync(CreateCustomerNoticeRequest request, CancellationToken ct = default);

        /// <summary>Mark a Draft notice as Pending (approved for sending).</summary>
        Task<Result<CustomerNotice>> MarkPendingAsync(int noticeId, string approvedBy, CancellationToken ct = default);

        /// <summary>Send the notice to the customer (transitions Pending → Sent, fires outbox event).</summary>
        Task<Result<CustomerNotice>> SendAsync(int noticeId, string sentBy, CancellationToken ct = default);

        /// <summary>Record customer acknowledgement of the notice.</summary>
        Task<Result<CustomerNotice>> RecordAcknowledgementAsync(int noticeId, string acknowledgedBy,
            string? responseText = null, CancellationToken ct = default);

        /// <summary>Record a customer dispute of the change.</summary>
        Task<Result<CustomerNotice>> RecordDisputeAsync(int noticeId, string disputeReason, CancellationToken ct = default);

        /// <summary>Resolve a customer dispute.</summary>
        Task<Result<CustomerNotice>> ResolveDisputeAsync(int noticeId, string resolvedBy,
            string resolution, CancellationToken ct = default);

        /// <summary>Close a notice after acknowledgement or dispute resolution.</summary>
        Task<Result<CustomerNotice>> CloseAsync(int noticeId, string closedBy, CancellationToken ct = default);

        /// <summary>Get a notice by ID with navigation properties.</summary>
        Task<CustomerNotice?> GetAsync(int noticeId, CancellationToken ct = default);

        /// <summary>List notices for an Item (optionally including closed).</summary>
        Task<IReadOnlyList<CustomerNotice>> GetForItemAsync(int itemId, bool includeClosed = false, CancellationToken ct = default);
    }

    public record CreateCustomerNoticeRequest(
        int CompanyId,
        string NoticeNumber,
        string Title,
        CustomerNoticeType Type,
        int? CustomerId = null,
        int? ItemId = null,
        int? OriginatingEcrId = null,
        int? OriginatingDeviationId = null,
        int? OriginatingWaiverId = null,
        int? OriginatingConcessionId = null,
        string? ChangeDescription = null,
        string? ImpactDescription = null,
        DateTime? ChangeEffectiveDate = null,
        string? AffectedSalesOrderReferences = null,
        string? AffectedContractReferences = null,
        bool AffectsForm = false,
        bool AffectsFit = false,
        bool AffectsFunction = false,
        bool SafetyImpact = false,
        NotificationDeliveryMethod DeliveryMethod = NotificationDeliveryMethod.Email,
        DateTime? RequiredResponseDate = null,
        string? CustomerContactName = null,
        string? CustomerContactEmail = null,
        string? Description = null,
        string? CreatedBy = null);
}
