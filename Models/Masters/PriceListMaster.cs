using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-9 — PriceListMaster (customer-facing price list header).
    //
    // Master Files Baseline cascade ship #7 of 10. Closes the "Real PriceList
    // master missing" gap from docs/research/master-files-baseline-2026-05-24.md
    // §6.7.
    //
    // POSITION IN THE STACK:
    //   PriceListMaster (this — the list header)
    //     └── PriceListLine (per-Item pricing rows within the list)
    //
    // ASSOCIATED ENTITIES (PRA-9 siblings):
    //   - DiscountSchema (promotional / contract / scale discounts)
    //   - RebateAgreement (customer back-end rebates)
    //
    // SCOPE PATTERN — a price list applies based on the (CustomerTier, CustomerId,
    // CurrencyId) tuple. Resolution order at order time:
    //   1. Customer-specific list (CustomerId IS NOT NULL AND CustomerId = X)
    //   2. CustomerTier-specific list (CustomerTier matches + CustomerId IS NULL)
    //   3. Company default list (CustomerTier IS NULL + CustomerId IS NULL)
    //   4. Item.UnitPrice fallback (legacy)
    //
    // CROSS-TENANT REFERENCE pattern:
    //   CompanyId NULL = system template (DEFAULT-WHOLESALE / DEFAULT-RETAIL /
    //                    DEFAULT-DISTRIBUTOR / DEFAULT-GOVERNMENT)
    //   CompanyId set  = tenant-owned list
    //
    // UNIQUE: (Code) WHERE CompanyId IS NULL + (CompanyId, Code) WHERE CompanyId
    // IS NOT NULL. Partial UNIQUEs per the Replit prod-validator convention.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.7
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    //   - SAP SD condition records pattern
    //   - Oracle Cloud Price Lists (QP_LIST_HEADERS_B)
    // =============================================================================
    [Table("PriceListMasters")]
    public class PriceListMaster
    {
        public int Id { get; set; }

        // NULL = system template, set = tenant-owned.
        public int? CompanyId { get; set; }

        // Stable code (e.g. "DEFAULT-WHOLESALE", "ACME-2026", "GOV-FED-2026-Q2").
        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Customer tier scope. NULL = applies to all tiers (company default).
        public CustomerTier? CustomerTier { get; set; }

        // Customer-specific override. NULL = applies to all customers in the
        // CustomerTier scope. SET = applies only to this customer.
        public int? CustomerId { get; set; }

        // Currency FK (FK to PRA-6 CurrencyMaster). All lines in the list are
        // denominated in this currency. Required.
        public int CurrencyId { get; set; }
        public CurrencyMaster? Currency { get; set; }

        // ---------------------------------------------------------------------
        // EFFECTIVE-DATING — the list is active in this window.
        // ---------------------------------------------------------------------
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        // NULL = open-ended.
        public DateTime? EffectiveToUtc { get; set; }

        // ---------------------------------------------------------------------
        // BEHAVIORAL FLAGS.
        // ---------------------------------------------------------------------

        // When true, lines in this list lock the customer's price — discounts
        // cannot reduce below ListPrice for contract-protected pricing
        // (typical aerospace contract pattern).
        public bool IsPriceLocked { get; set; } = false;

        // When true, a customer can stack DiscountSchema on top of these prices.
        // When false, these prices are NET (no discounts apply on top).
        public bool AllowsDiscounts { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    // =============================================================================
    // CustomerTier — wholesale / distribution / retail / government / custom.
    // =============================================================================
    public enum CustomerTier
    {
        Wholesale = 0,          // Bulk B2B at wholesale margin
        Distribution = 1,       // Distributors who resell
        Retail = 2,             // Direct retail / end-customer
        Government = 3,         // GSA / federal / municipal — typically separate tier with audit requirements
        Education = 4,          // K-12 / university — often discounted
        NonProfit = 5,          // 501(c)(3) and equivalents
        Internal = 6,           // Intercompany / employee — at-cost or below
        Custom = 99             // Tenant-defined tier
    }
}
