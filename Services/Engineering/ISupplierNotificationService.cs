// Sprint 14.3 PR-5 (2026-05-27) — Supplier notification service interface.
//
// Outbound PCN (Process Change Notification) lifecycle for supplier
// change communications. The supplier must acknowledge, assess impact,
// and potentially re-qualify (FAI/PPAP) before close.
//
// Lifecycle: Draft → Pending → SentToSupplier → SupplierAcknowledged
//            → ImpactAssessmentReceived → Approved → Closed
//                                       ↘ Rejected → (rework)

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering
{
    public interface ISupplierNotificationService
    {
        /// <summary>Create a new supplier PCN in Draft status.</summary>
        Task<Result<SupplierProcessChangeNotification>> CreateAsync(CreateSupplierPcnRequest request, CancellationToken ct = default);

        /// <summary>Mark a Draft PCN as Pending (approved for sending).</summary>
        Task<Result<SupplierProcessChangeNotification>> MarkPendingAsync(int pcnId, string approvedBy, CancellationToken ct = default);

        /// <summary>Send the PCN to the supplier (fires outbox event).</summary>
        Task<Result<SupplierProcessChangeNotification>> SendAsync(int pcnId, string sentBy, CancellationToken ct = default);

        /// <summary>Record supplier acknowledgement of receipt.</summary>
        Task<Result<SupplierProcessChangeNotification>> RecordAcknowledgementAsync(int pcnId, string supplierRespondent, CancellationToken ct = default);

        /// <summary>Record the supplier's formal impact assessment.</summary>
        Task<Result<SupplierProcessChangeNotification>> RecordImpactAssessmentAsync(int pcnId,
            string assessment, decimal? costImpact = null, int? leadTimeImpactDays = null,
            CancellationToken ct = default);

        /// <summary>Approve the supplier's response — change can proceed.</summary>
        Task<Result<SupplierProcessChangeNotification>> ApproveAsync(int pcnId, string approvedBy, CancellationToken ct = default);

        /// <summary>Reject the supplier's response — supplier must rework their proposal.</summary>
        Task<Result<SupplierProcessChangeNotification>> RejectAsync(int pcnId, string rejectedBy, string reason, CancellationToken ct = default);

        /// <summary>Close PCN after first conforming shipment verified.</summary>
        Task<Result<SupplierProcessChangeNotification>> CloseAsync(int pcnId, string closedBy,
            string? firstConformingShipmentRef = null, CancellationToken ct = default);

        /// <summary>Get a PCN by ID with navigation properties.</summary>
        Task<SupplierProcessChangeNotification?> GetAsync(int pcnId, CancellationToken ct = default);

        /// <summary>List PCNs for an Item (optionally including closed).</summary>
        Task<IReadOnlyList<SupplierProcessChangeNotification>> GetForItemAsync(int itemId, bool includeClosed = false, CancellationToken ct = default);
    }

    public record CreateSupplierPcnRequest(
        int CompanyId,
        string PcnNumber,
        string Title,
        PcnType Type,
        int? VendorId = null,
        int? ItemId = null,
        int? OriginatingEcrId = null,
        string? ChangeDescription = null,
        string? ImpactDescription = null,
        DateTime? ProposedEffectiveDate = null,
        string? CurrentSpecification = null,
        string? ProposedSpecification = null,
        bool AffectsForm = false,
        bool AffectsFit = false,
        bool AffectsFunction = false,
        bool SafetyImpact = false,
        bool FirstArticleRequired = false,
        bool PpapRequired = false,
        bool QualityPlanUpdateRequired = false,
        int? SampleQuantityRequired = null,
        bool ApprovalRequired = true,
        NotificationDeliveryMethod DeliveryMethod = NotificationDeliveryMethod.Email,
        DateTime? RequiredResponseDate = null,
        string? SupplierContactName = null,
        string? SupplierContactEmail = null,
        string? Description = null,
        string? CreatedBy = null);
}
