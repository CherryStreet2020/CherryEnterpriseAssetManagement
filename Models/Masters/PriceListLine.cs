using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-9 — PriceListLine (per-Item pricing within a price list).
    //
    // Belongs to a PriceListMaster (header). Carries the actual $/UOM the
    // customer sees, with optional volume-break tiers and contract-lock
    // expiration. Effective-dated separately from the header — common pattern
    // is the header is open-ended but specific lines re-price quarterly.
    //
    // VOLUME BREAKS are stored as JSONB so we don't proliferate a 3rd table.
    // Shape:
    //   [{"qty":  10, "price": 9.50},
    //    {"qty":  50, "price": 9.00},
    //    {"qty": 100, "price": 8.50}]
    // At order time, service reads the breaks descending and picks the first
    // tier where order qty >= break qty. Single-unit price is the UnitPrice
    // column (the no-break price).
    //
    // TENANT trio inherited from PriceListMaster (no direct CompanyId on the
    // line — it flows transitively through the FK).
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.7
    //   - SAP SD VKKDP (sales price condition)
    //   - Oracle Cloud QP_LIST_LINES
    // =============================================================================
    [Table("PriceListLines")]
    public class PriceListLine
    {
        public int Id { get; set; }

        // FK to PriceListMaster — REQUIRED.
        public int PriceListMasterId { get; set; }
        public PriceListMaster? PriceListMaster { get; set; }

        // FK to Item — REQUIRED. Composite UNIQUE with PriceListMasterId means
        // one row per (List, Item) pair.
        public int ItemId { get; set; }

        // UOM the price is denominated against. NULL = inherit from Item
        // primary UOM. Lets a tenant publish "EA $9.50 / CASE $108.00" as
        // two lines in the same list.
        public int? UomId { get; set; }

        // ---------------------------------------------------------------------
        // PRICING.
        // ---------------------------------------------------------------------

        // The single-unit / no-break price.
        [Column(TypeName = "numeric(18,4)")]
        public decimal UnitPrice { get; set; }

        // The rack / list price BEFORE any contract-negotiated discount.
        // Often equals UnitPrice for new lists; for renegotiated lists,
        // UnitPrice falls below ListPrice and the gap shows on the order as
        // "Customer Contract Discount: $X".
        [Column(TypeName = "numeric(18,4)")]
        public decimal? ListPrice { get; set; }

        // Volume break tiers — JSONB array as documented above.
        [Column(TypeName = "jsonb")]
        public string? VolumeBreaksJson { get; set; }

        // Minimum order quantity. NULL = no minimum.
        [Column(TypeName = "numeric(18,4)")]
        public decimal? MinimumQuantity { get; set; }

        // Maximum per-order quantity. NULL = no max.
        [Column(TypeName = "numeric(18,4)")]
        public decimal? MaximumQuantity { get; set; }

        // ---------------------------------------------------------------------
        // EFFECTIVE-DATING — line-level overrides the header.
        // ---------------------------------------------------------------------
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        public DateTime? EffectiveToUtc { get; set; }

        // Contract price lock — for ETO contracts where price is guaranteed
        // until a specific date regardless of input cost changes. NULL = no
        // lock (price can be changed per the EffectiveTo / new-line workflow).
        public DateTime? PriceLockUntilUtc { get; set; }

        // ---------------------------------------------------------------------
        // FLAGS.
        // ---------------------------------------------------------------------

        // True = DiscountSchema rows can stack on top of this UnitPrice.
        // False = this is a NET price.
        public bool DiscountAllowed { get; set; } = true;

        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
