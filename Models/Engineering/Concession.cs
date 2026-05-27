// Sprint 14.3 PR-4 (2026-05-27) — Concession entity.
//
// RETROACTIVE customer acceptance of ALREADY-PRODUCED non-conforming
// material. Unlike Deviation (pre-production exception) or Waiver
// (ongoing customer-approved divergence), a Concession is issued AFTER
// the fact — material has already been produced, inspected, and found
// non-conforming, and the customer agrees to accept it anyway.
//
// AS9100 §8.7.1: "The organization shall obtain a concession from the
// customer when nonconforming product is to be delivered to the customer."
//
// LIFECYCLE: Draft → Submitted → CustomerReview → Accepted → Closed
//                                                ↘ Rejected → (MRB/Scrap/Rework)
//                                                ↘ Cancelled

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    public enum ConcessionType
    {
        Material = 0,       // Non-conforming material accepted as-is
        Dimensional = 1,    // Out-of-tolerance part accepted
        Cosmetic = 2,       // Surface/appearance defect accepted
        Process = 3,        // Process deviation on completed work accepted
        Documentation = 4,  // Missing/incomplete documentation accepted retroactively
    }

    public enum ConcessionStatus
    {
        Draft = 0,
        Submitted = 1,
        CustomerReview = 2,
        Accepted = 3,       // Customer accepted the non-conforming material
        Rejected = 4,       // Customer rejected — triggers MRB/scrap/rework
        Cancelled = 5,
        Closed = 6,         // Post-acceptance administrative close
    }

    /// <summary>
    /// Disposition of rejected concession material.
    /// </summary>
    public enum RejectedDisposition
    {
        ReturnToVendor = 0,
        Scrap = 1,
        Rework = 2,
        MrbReview = 3,      // Material Review Board decides
        UseOnAlternateJob = 4,
    }

    [Table("Concessions")]
    public class Concession
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        [Required] [StringLength(32)]
        public string ConcessionNumber { get; set; } = string.Empty;

        [Required] [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public ConcessionType Type { get; set; } = ConcessionType.Material;
        public ConcessionStatus Status { get; set; } = ConcessionStatus.Draft;

        // ----- What was produced -----
        public int? ItemId { get; set; }
        public Item? Item { get; set; }
        public int? ProductionOrderId { get; set; }
        public Production.ProductionOrder? ProductionOrder { get; set; }

        /// <summary>Quantity of non-conforming material seeking acceptance.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal AffectedQuantity { get; set; }

        /// <summary>Lot/serial numbers of the affected material.</summary>
        [StringLength(500)]
        public string? AffectedLotSerials { get; set; }

        // ----- Linkage to quality records -----
        public int? OriginatingEcrId { get; set; }
        public EngineeringChangeRequest? OriginatingEcr { get; set; }
        public int? RelatedDeviationId { get; set; }
        public Deviation? RelatedDeviation { get; set; }

        /// <summary>NCR number that surfaced the non-conformance.</summary>
        [StringLength(50)]
        public string? NcrReference { get; set; }

        /// <summary>Inspection report reference.</summary>
        [StringLength(50)]
        public string? InspectionReportReference { get; set; }

        // ----- Customer acceptance -----
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [StringLength(100)]
        public string? CustomerContractReference { get; set; }
        [StringLength(200)]
        public string? CustomerAcceptingAuthority { get; set; }
        public DateTime? CustomerAcceptanceDateUtc { get; set; }
        [StringLength(100)]
        public string? CustomerAcceptanceDocumentNumber { get; set; }

        // ----- Non-conformance detail -----
        [StringLength(1000)]
        public string? OriginalSpecification { get; set; }
        [StringLength(1000)]
        public string? ActualCondition { get; set; }
        [StringLength(4000)]
        public string? Justification { get; set; }
        [StringLength(2000)]
        public string? Disposition { get; set; }

        // ----- Impact flags -----
        public bool AffectsForm { get; set; }
        public bool AffectsFit { get; set; }
        public bool AffectsFunction { get; set; }
        public bool SafetyImpact { get; set; }

        // ----- Rejection handling -----
        public RejectedDisposition? RejectedDisposition { get; set; }
        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        // ----- Approval chain -----
        [StringLength(100)] public string? RequestedBy { get; set; }
        public DateTime? RequestedAtUtc { get; set; }
        [StringLength(100)] public string? AcceptedBy { get; set; }
        public DateTime? AcceptedAtUtc { get; set; }
        [StringLength(100)] public string? RejectedBy { get; set; }
        public DateTime? RejectedAtUtc { get; set; }

        // ----- Audit -----
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [StringLength(100)] public string? CreatedBy { get; set; }
        [StringLength(100)] public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
