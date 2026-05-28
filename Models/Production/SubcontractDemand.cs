// Sprint 15.1 PR-4 (2026-05-28) — SubcontractDemand entity.
//
// THE DUAL-DEMAND PATTERN from spec §9.
//
// A subcontract operation generates TWO linked demands:
//   1. SERVICE PURCHASE DEMAND — the vendor's processing work (service item PO line)
//   2. WIP MOVEMENT DEMAND     — the physical material going outside
//
// "The service PO pays for the processing. The WIP shipment tracks the
//  physical material. Do not confuse them." — Dean's spec §9.
//
// This entity links a SubcontractOperation to the two ProductionSupplyDemand
// rows it generates. One SubcontractDemand row per subcontract operation
// instance with both ServicePurchaseDemandId and WipMovementDemandId FKs.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// Lifecycle of the dual-demand binding.
    /// </summary>
    public enum SubcontractDemandStatus
    {
        /// <summary>Both demands created but neither resolved.</summary>
        Open = 0,
        /// <summary>Service PO created and approved.</summary>
        ServiceCommitted = 1,
        /// <summary>WIP shipped to vendor (movement demand satisfied).</summary>
        WipAtVendor = 2,
        /// <summary>Vendor returned WIP (movement satisfied) and invoice posted (service satisfied).</summary>
        BothSatisfied = 3,
        /// <summary>Service rejected / cancelled.</summary>
        Cancelled = 4,
    }

    /// <summary>
    /// Binding row that joins a SubcontractOperation with its two linked
    /// ProductionSupplyDemand rows (service + WIP movement).
    /// </summary>
    public class SubcontractDemand
    {
        public int Id { get; set; }

        // ──────────────────────────── Tenant trio ───────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── Subcontract operation ─────────────────────

        [Required]
        public int SubcontractOperationId { get; set; }
        public SubcontractOperation? SubcontractOperation { get; set; }

        /// <summary>Denormalized PRO id for fast filtering.</summary>
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        /// <summary>Denormalized op sequence.</summary>
        public int OperationSequence { get; set; }

        // ──────────────────────── §9: Service Purchase Demand ───────────────

        /// <summary>FK to the ProductionSupplyDemand row that captures the
        /// vendor service purchase (§9 Demand 1).</summary>
        public int? ServicePurchaseDemandId { get; set; }
        public ProductionSupplyDemand? ServicePurchaseDemand { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Service Qty Required")]
        public decimal ServiceQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Service Unit Cost")]
        public decimal ServiceUnitCost { get; set; }

        // ──────────────────────── §9: WIP Movement Demand ───────────────────

        /// <summary>FK to the ProductionSupplyDemand row that captures the
        /// physical WIP movement out and back (§9 Demand 2).</summary>
        public int? WipMovementDemandId { get; set; }
        public ProductionSupplyDemand? WipMovementDemand { get; set; }

        /// <summary>Item being shipped to the vendor (the physical part).</summary>
        public int? WipItemId { get; set; }
        public Item? WipItem { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "WIP Qty To Send")]
        public decimal WipQuantityToSend { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "WIP Qty Returned")]
        public decimal WipQuantityReturned { get; set; }

        // ──────────────────────── Routing context ───────────────────────────

        /// <summary>Internal WIP operation the part comes FROM (§9 "From Internal WIP Op 030").</summary>
        public int? FromOperationSequence { get; set; }

        /// <summary>Internal op the part returns TO (§9 "Return to Op 050 Assembly").</summary>
        public int? ToOperationSequence { get; set; }

        // ──────────────────────── Lifecycle + dates ─────────────────────────

        public SubcontractDemandStatus Status { get; set; } = SubcontractDemandStatus.Open;

        [Display(Name = "Required Back Date")]
        public DateTime? RequiredBackDate { get; set; }

        [Display(Name = "Service Committed UTC")]
        public DateTime? ServiceCommittedUtc { get; set; }

        [Display(Name = "Wip At Vendor UTC")]
        public DateTime? WipAtVendorUtc { get; set; }

        [Display(Name = "Both Satisfied UTC")]
        public DateTime? BothSatisfiedUtc { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
