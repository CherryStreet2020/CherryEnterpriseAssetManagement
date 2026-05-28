// Sprint 15.1 PR-5 (2026-05-28) — VendorLocation entity.
//
// THE VENDOR'S PHYSICAL SITE for holding WIP.
//
// A subcontract vendor often has multiple facilities. Material we ship out
// for processing has to land at a SPECIFIC vendor location — not just "to
// the supplier." This entity registers each vendor site/warehouse that
// holds our WIP, with the flags that govern what kind of material can land
// there (vendor-managed, customer-owned, consigned).
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §14
//   - 18 spec'd fields verbatim from §14 "Vendor location fields"

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Masters;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// What kind of vendor site is this (logistics/storage type).
    /// </summary>
    public enum VendorLocationType
    {
        /// <summary>Vendor's primary processing facility.</summary>
        ProcessingPlant = 0,
        /// <summary>Vendor's warehouse / storage site (no processing).</summary>
        Warehouse = 1,
        /// <summary>Inspection / staging site.</summary>
        Inspection = 2,
        /// <summary>Distribution center / cross-dock.</summary>
        DistributionCenter = 3,
        /// <summary>Customer's site (for drop-ship subcontract patterns).</summary>
        CustomerSite = 4,
        /// <summary>Third-party logistics provider.</summary>
        Tpl = 5,
    }

    public class VendorLocation
    {
        public int Id { get; set; }

        // ──────────────────────── Tenant trio ───────────────────────────────
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        // ──────────────────────── §14 fields ────────────────────────────────

        [Required, StringLength(48)]
        [Display(Name = "Vendor Location Code")]
        public string LocationCode { get; set; } = string.Empty;

        [Required]
        public int SupplierId { get; set; }
        public Vendor? Supplier { get; set; }

        [StringLength(64)]
        [Display(Name = "Supplier Site")]
        public string? SupplierSiteCode { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        /// <summary>Linked internal warehouse master record (for cost tracking).</summary>
        public int? LinkedWarehouseId { get; set; }
        public WarehouseMaster? LinkedWarehouse { get; set; }

        /// <summary>Linked internal bin/location (sub-warehouse).</summary>
        public int? LinkedBinLocationId { get; set; }
        public Location? LinkedBinLocation { get; set; }

        public VendorLocationType LocationType { get; set; } = VendorLocationType.ProcessingPlant;

        [Display(Name = "Vendor Managed")]
        public bool VendorManaged { get; set; }

        [Display(Name = "Customer-Owned Material Allowed")]
        public bool CustomerOwnedMaterialAllowed { get; set; }

        [Display(Name = "Consigned Material Allowed")]
        public bool ConsignedMaterialAllowed { get; set; }

        [Display(Name = "WIP Allowed")]
        public bool WipAllowed { get; set; } = true;

        [Display(Name = "Inspection Required on Return")]
        public bool InspectionRequiredOnReturn { get; set; }

        [StringLength(64)]
        [Display(Name = "Default Shipping Method")]
        public string? DefaultShippingMethod { get; set; }

        [Display(Name = "Default Transit Days")]
        public int DefaultTransitDays { get; set; }

        /// <summary>Default location at our facility for receiving from this vendor.</summary>
        public int? DefaultReceivingLocationId { get; set; }
        public Location? DefaultReceivingLocation { get; set; }

        [Display(Name = "Default Return-To Operation Seq")]
        public int? DefaultReturnToOperationSequence { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [StringLength(2000)]
        public string? Notes { get; set; }

        // ──────────────────────── Audit ─────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(120)]
        public string? CreatedBy { get; set; }

        public ICollection<VendorWipBalance> Balances { get; set; } = new List<VendorWipBalance>();

        public byte[]? RowVersion { get; set; }
    }
}
