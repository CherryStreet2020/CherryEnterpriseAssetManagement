// B6 Foundation Sprint PR-FS-2 (2026-05-26) — ItemSite entity.
//
// SAP MARC equivalent — per-Site override of Item Master attributes.
// Carries NULLABLE override fields for every Item attribute that can
// reasonably differ between plants. The resolver service cascades:
//
//     ItemSite.X  →  Item.X  →  null / hardcoded default
//
// Common per-Site overrides in tier-1 ERPs:
//   - Plant material status (Active at Plant A, PendingDiscontinue at Plant B)
//   - MRP type / planning policy (manual at one plant, EOQ at another)
//   - Lead time (in-house plant has 2 days, contract plant has 14)
//   - Safety stock + reorder point (per-plant demand profile)
//   - ABC indicator (A at high-volume plant, C at low-volume plant)
//   - Costing (different standard cost per plant for transfer pricing)
//   - Default buyer / preferred vendor (per-plant procurement org)
//   - Storage / hazmat handling (only one plant licensed for hazmat)
//   - ItemGroup override (rare — one plant treats a part as FG, another as RAW
//     for inter-plant transfer pricing)
//
// Uniqueness: (TenantId, ItemId, SiteId) — one ItemSite row per (Item × Site).
//
// Tenant trio enforced:
//   TenantId  — required for cross-tenant query isolation
//   CompanyId — denormalized from Site.CompanyId for direct queries
//   SiteId    — the override scope
//
// HARD LOCK B6 GO BIG: full BIC field set. NOT a Minimal subset. See
// docs/research/b6-foundation-sprint-design-2026-05-26.md for the spec.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Models.Masters
{
    /// <summary>
    /// Per-Site override row for Item Master attributes. SAP MARC equivalent.
    /// All override fields are nullable — null = "use Item-level default".
    /// </summary>
    public class ItemSite
    {
        public int Id { get; set; }

        // ===== Identity + tenant trio =====================================

        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Required]
        public int SiteId { get; set; }
        public Site? Site { get; set; }

        // Tenant trio — denormalized from Site for cross-tenant isolation
        // (Site already carries CompanyId; we denormalize both onto the row
        // so query plans don't have to JOIN through Site for tenant filtering).
        public int? TenantId { get; set; }
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        // ===== Status overrides ===========================================

        /// <summary>
        /// Plant material status override. NULL = use Item.Status. Allows
        /// "Active everywhere except Plant B where it's PendingDiscontinue".
        /// </summary>
        [Display(Name = "Status (per Site)")]
        public ItemStatus? Status { get; set; }

        [Display(Name = "Is Active (per Site)")]
        public bool IsActive { get; set; } = true;

        // ===== Stocking / procurement overrides ===========================

        [Display(Name = "Is Stocked (per Site)")]
        public bool? IsStocked { get; set; }

        [Display(Name = "Is Purchasable (per Site)")]
        public bool? IsPurchasable { get; set; }

        [Display(Name = "Is Critical Spare (per Site)")]
        public bool? IsCriticalSpare { get; set; }

        [Display(Name = "Stock Policy (per Site)")]
        public StockPolicy? StockPolicy { get; set; }

        [Display(Name = "ABC Classification (per Site)")]
        public ABCClassification? ABCClass { get; set; }

        // ===== Inventory levels (per Site) ================================

        [Display(Name = "Minimum Quantity (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MinQuantity { get; set; }

        [Display(Name = "Maximum Quantity (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MaxQuantity { get; set; }

        [Display(Name = "Reorder Point (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ReorderPoint { get; set; }

        [Display(Name = "Reorder Quantity (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ReorderQuantity { get; set; }

        [Display(Name = "Safety Stock (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? SafetyStock { get; set; }

        [Display(Name = "Economic Order Quantity (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? EOQ { get; set; }

        [Display(Name = "Lead Time Days (per Site)")]
        public int? LeadTimeDays { get; set; }

        [Display(Name = "Reorder Method (per Site)")]
        public ReorderMethod? ReorderMethod { get; set; }

        [Display(Name = "Auto-Reorder Enabled (per Site)")]
        public bool? AutoReorderEnabled { get; set; }

        // ===== Costing overrides (per Site) ===============================

        [Display(Name = "Cost Method (per Site)")]
        public CostMethod? CostMethod { get; set; }

        [Display(Name = "Standard Cost (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? StandardCost { get; set; }

        [Display(Name = "Average Cost (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? AverageCost { get; set; }

        [Display(Name = "Last Purchase Cost (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? LastPurchaseCost { get; set; }

        [Display(Name = "List Price (per Site)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ListPrice { get; set; }

        // ===== Sourcing overrides (per Site) ==============================

        [Display(Name = "Preferred Vendor (per Site)")]
        public int? PreferredVendorId { get; set; }
        public Vendor? PreferredVendor { get; set; }

        [Display(Name = "Default Buyer Id (per Site)")]
        public int? DefaultBuyerId { get; set; }

        [Display(Name = "Default Location (per Site)")]
        public int? DefaultLocationId { get; set; }
        public Location? DefaultLocationRef { get; set; }

        [StringLength(20)]
        [Display(Name = "Default Warehouse (per Site)")]
        public string? DefaultWarehouse { get; set; }

        [StringLength(20)]
        [Display(Name = "Default Bin (per Site)")]
        public string? DefaultBin { get; set; }

        // ===== Tracking overrides (per Site) ==============================

        [Display(Name = "Tracking Type (per Site)")]
        public TrackingType? TrackingType { get; set; }

        [Display(Name = "Shelf Life Days (per Site)")]
        public int? ShelfLifeDays { get; set; }

        // ===== Substance overrides (per Site) =============================

        [Display(Name = "Is Hazmat (per Site)")]
        public bool? IsHazmat { get; set; }

        [StringLength(100)]
        [Display(Name = "Storage Requirements (per Site)")]
        public string? StorageRequirements { get; set; }

        // ===== Classification override (per Site) =========================
        //
        // Rare — allows one plant to treat a part as FG while another treats
        // it as SUBASSY for inter-plant transfer pricing. Null = use
        // Item.ItemGroupId.

        [Display(Name = "Item Group (per Site)")]
        public int? ItemGroupId { get; set; }
        public ItemGroup? ItemGroup { get; set; }

        // ===== Audit =======================================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
