// Sprint 14.3 PR-1 (2026-05-27) — Engineering Change Order (ECO).
//
// The "do it" record created from an Approved ECR. Drives the actual
// supersede of DocumentVersions, the FAI re-trigger, the customer notice,
// and the in-flight ProductionOrder impact assessment.
//
// LIFECYCLE: Draft → InApproval → Approved → Released → Implemented → Closed
//                                                              ↘ Cancelled
//
// MULTI-STAGE APPROVAL: each ECO has an N-stage approval chain (EcoApproval
// rows). ECO flips from InApproval → Approved when ALL stages are Approved.
// Stage ordering is honored — a stage can't be approved until earlier stages
// are Approved (or Skipped).
//
// EFFECTIVITY: when an ECO is Released, its effectivity rule determines
// WHICH instances are subject to the change:
//   - Immediate: all in-flight + future production
//   - DateBased: production starting on/after EffectiveFromUtc
//   - SerialNumber: serial range (EffectivitySerialFrom..To)
//   - LotNumber: lot range
//   - Job: a specific ProductionOrder onward
//   - NextProduction: next PRO release for this Item
//
// ATOMIC DOCUMENT SUPERSEDE: ReleaseEcoAsync walks the ECO's line items and,
// for each one with NewDocumentVersionId set, invokes IDocumentService
// .ReleaseVersionAsync to flip the new version to Released (which atomically
// supersedes the prior Released version per the PR #366 DMS substrate).
// One transactional unit across all line items (PR-FS-6 atomic lesson).

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Models.Engineering
{
    /// <summary>
    /// ECO lifecycle status.
    /// </summary>
    public enum EcoStatus
    {
        /// <summary>Draft — author working copy; line items + approvals can be added.</summary>
        Draft = 0,

        /// <summary>In approval — submitted to the multi-stage approval chain.</summary>
        InApproval = 1,

        /// <summary>Approved — all approval stages green; ready for Release.</summary>
        Approved = 2,

        /// <summary>Released — effective per the EffectivityType rule; DocumentVersions superseded.</summary>
        Released = 3,

        /// <summary>Implemented — change has propagated to production (kits issued, drawings on the floor).</summary>
        Implemented = 4,

        /// <summary>Closed — change closed-loop verified (FAI re-baseline complete if F/F/F).</summary>
        Closed = 5,

        /// <summary>Cancelled — terminated before Release. Terminal.</summary>
        Cancelled = 6,
    }

    /// <summary>
    /// How does an ECO take effect? Drives which in-flight production is
    /// affected vs. which future production picks up the change.
    /// </summary>
    public enum EcoEffectivityType
    {
        /// <summary>Immediate — applies to all in-flight + future production at Release time.</summary>
        Immediate = 0,

        /// <summary>Date-based — applies to production released on/after EffectiveFromUtc.</summary>
        DateBased = 1,

        /// <summary>Serial-number — applies to a serial range (EffectivitySerialFrom..To).</summary>
        SerialNumber = 2,

        /// <summary>Lot-number — applies to a lot range (EffectivityLotFrom..To).</summary>
        LotNumber = 3,

        /// <summary>Job — applies to a specific ProductionOrder onward.</summary>
        Job = 4,

        /// <summary>Next-production — applies to the next PRO release for this Item.</summary>
        NextProduction = 5,
    }

    /// <summary>
    /// Engineering Change Order — the controlled execution record for an
    /// approved change. Created from an Approved ECR; drives DocumentVersion
    /// supersede, FAI re-trigger, and ProductionOrder impact assessment.
    /// </summary>
    [Table("EngineeringChangeOrders")]
    public class EngineeringChangeOrder
    {
        public int Id { get; set; }

        // ===== Tenant trio =================================================

        [Required]
        public int CompanyId { get; set; }

        public int? LocationId { get; set; }

        // ===== Identity ====================================================

        [Required, StringLength(50)]
        [Display(Name = "ECO Number")]
        public string EcoNumber { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        // ===== Source ECR ==================================================

        /// <summary>
        /// FK to the source ECR. RESTRICT on ECR delete — an ECO can't outlive
        /// the ECR that created it; closure of the chain requires both sides.
        /// </summary>
        [Required]
        public int SourceEcrId { get; set; }
        public EngineeringChangeRequest? SourceEcr { get; set; }

        [Required]
        public ChangeUrgency Urgency { get; set; } = ChangeUrgency.Routine;

        [Required]
        public EcoStatus Status { get; set; } = EcoStatus.Draft;

        // ===== Effectivity =================================================

        [Required]
        [Display(Name = "Effectivity Type")]
        public EcoEffectivityType EffectivityType { get; set; } = EcoEffectivityType.Immediate;

        [Display(Name = "Effective From")]
        public DateTime? EffectiveFromUtc { get; set; }

        [Display(Name = "Effective To")]
        public DateTime? EffectiveToUtc { get; set; }

        [StringLength(50)]
        [Display(Name = "Serial From")]
        public string? EffectivitySerialFrom { get; set; }

        [StringLength(50)]
        [Display(Name = "Serial To")]
        public string? EffectivitySerialTo { get; set; }

        [StringLength(50)]
        [Display(Name = "Lot From")]
        public string? EffectivityLotFrom { get; set; }

        [StringLength(50)]
        [Display(Name = "Lot To")]
        public string? EffectivityLotTo { get; set; }

        /// <summary>
        /// For Job-effectivity: the ProductionOrder that this ECO takes
        /// effect from. SET NULL on PRO delete preserves ECO history.
        /// </summary>
        public int? EffectivityProductionOrderId { get; set; }
        public ProductionOrder? EffectivityProductionOrder { get; set; }

        // ===== AS9145 / Customer / Regulatory flags =======================

        /// <summary>
        /// Set TRUE when the originating ECR had AffectsForm/Fit/Function.
        /// Drives downstream FAI re-baseline trigger (Sprint 14.3 PR-7).
        /// </summary>
        [Display(Name = "Requires FAI Re-Trigger")]
        public bool RequiresFaiRetrigger { get; set; } = false;

        /// <summary>
        /// Set TRUE when the originating ECR had AffectsCustomers. Drives
        /// downstream Customer Notice generation (Sprint 14.3 PR-3).
        /// </summary>
        [Display(Name = "Requires Customer Notice")]
        public bool RequiresCustomerNotice { get; set; } = false;

        [Display(Name = "Customer Notice Sent At")]
        public DateTime? CustomerNoticeSentAtUtc { get; set; }

        /// <summary>
        /// Set TRUE when the originating ECR had AffectsRegulatory. Drives
        /// downstream regulatory body notification.
        /// </summary>
        [Display(Name = "Requires Regulatory Notice")]
        public bool RequiresRegulatoryNotice { get; set; } = false;

        // ===== Lifecycle stamps ===========================================

        [Display(Name = "Approved At")]
        public DateTime? ApprovedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Released At")]
        public DateTime? ReleasedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Released By")]
        public string? ReleasedBy { get; set; }

        [Display(Name = "Implemented At")]
        public DateTime? ImplementedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Implemented By")]
        public string? ImplementedBy { get; set; }

        [Display(Name = "Closed At")]
        public DateTime? ClosedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Closed By")]
        public string? ClosedBy { get; set; }

        // ===== Audit + concurrency ========================================

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        public byte[]? RowVersion { get; set; }

        // ===== Navs ========================================================

        public ICollection<EcoLineItem>? LineItems { get; set; }
        public ICollection<EcoApproval>? Approvals { get; set; }
    }
}
