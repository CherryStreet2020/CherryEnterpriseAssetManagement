using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-7 — WarehouseMaster (financial inventory unit).
    //
    // Master Files Baseline cascade ship #5. Closes the "Warehouse collapses
    // into Location" gap surfaced in docs/research/master-files-baseline-2026-05-24.md
    // §6.4. Models the SAP S/4HANA + Dynamics 365 separation-of-concerns
    // pattern (see ADR-019): EAM Location is the ASSET hierarchy
    // (Building/Floor/Bay/Rack), WarehouseMaster is the FINANCIAL inventory
    // unit that anchors PostingProfile + carries default GL accounts.
    //
    // EXPLICITLY REJECTED: the NetSuite single-Location pattern (where one
    // table conflates premises + asset shelving + inventory accounting).
    // That collapse blocks separate financial postings per warehouse type
    // (DC vs Plant vs 3PL vs Consignment) and forces the inventory team to
    // share columns with the maintenance team — exactly the "going backwards"
    // Dean called out in the 2026-05-24 morning call.
    //
    // OPTION D SHAPE (locked 2026-05-24):
    //   Site (EXISTING, untouched) — premises
    //     ├─ Location (EXISTING, untouched) — asset hierarchy
    //     └─ WarehouseMaster (THIS) — financial inventory unit
    //           └─ BinMaster — physical leaf inventory location
    //
    // CROSS-TENANT REFERENCE pattern (mirrors UnitOfMeasureMaster /
    // CurrencyMaster / TaxAuthority):
    //   - CompanyId NULL  = system template row (DC-DEFAULT / PROD-DEFAULT /
    //                       3PL-DEFAULT) — used to clone-into-tenant during
    //                       tenant onboarding
    //   - CompanyId set   = tenant-owned operational warehouse
    //
    // UNIQUE: (Code) WHERE CompanyId IS NULL for system templates;
    // (CompanyId, Code) WHERE CompanyId IS NOT NULL for tenant rows.
    // No COALESCE-in-index (Replit prod-validator lesson from PR #5c.1.1).
    //
    // AUTHORITY:
    //   - docs/adr/ADR-019-wms-posting-profile-pattern.md (THIS PR ships it)
    //   - docs/research/master-files-baseline-2026-05-24.md §6.4
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    // =============================================================================
    [Table("WarehouseMasters")]
    public class WarehouseMaster
    {
        public int Id { get; set; }

        // NULL = system template row. INT > 0 = tenant-owned warehouse.
        public int? CompanyId { get; set; }

        // FK to the EXISTING Site (premises) entity. NULL for system templates
        // (which are pre-onboarding shells) + cross-site "company-wide" pools.
        // INT > 0 for tenant-owned warehouses that physically live at a site.
        public int? SiteId { get; set; }

        // Stable warehouse code. UPPERCASE, hyphen/underscore-delimited.
        // System templates: "DC-DEFAULT" / "PROD-DEFAULT" / "3PL-DEFAULT" /
        //                   "CONSIGN-DEFAULT" / "QUAR-DEFAULT" / "RTN-DEFAULT".
        // Tenant: free-form per their nomenclature.
        [Required, StringLength(32)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Type drives default posting behavior + UI affordances.
        public WarehouseType WarehouseType { get; set; } = WarehouseType.DistributionCenter;

        // ---------------------------------------------------------------------
        // DEFAULT GL ACCOUNTS — the financial half of the SAP/Dynamics
        // separation. PostingProfile (PRA-7 sibling) reads these as the
        // fallback when an ItemGroup-specific override isn't found.
        //
        // All four are nullable so tenant onboarding can wire them
        // post-create; system templates leave them NULL.
        // ---------------------------------------------------------------------
        public int? DefaultInventoryGlAccountId { get; set; }
        public int? DefaultCogsGlAccountId { get; set; }
        public int? DefaultScrapGlAccountId { get; set; }
        public int? DefaultVarianceGlAccountId { get; set; }

        // Operating currency (FK to CurrencyMaster). NULL = inherit from
        // Company.FunctionalCurrency at posting time.
        public int? DefaultCurrencyId { get; set; }

        // ---------------------------------------------------------------------
        // BEHAVIORAL FLAGS — drive PostingProfile branching + reporting cuts.
        // ---------------------------------------------------------------------

        // True when goods physically live here but title remains with a
        // supplier (consignment IN) or a customer (consignment OUT).
        // Affects inventory valuation: consignment IN is NOT on our balance
        // sheet until consumed.
        public bool IsConsignment { get; set; } = false;

        // True for bonded / customs-controlled warehouses (duty deferred
        // until withdrawal). Affects duty + landed-cost posting.
        public bool IsBonded { get; set; } = false;

        // True for warehouses where inbound triggers tax accrual on receipt
        // (typical for VAT/GST jurisdictions). False for US sales-tax pattern.
        public bool IsTaxOnReceipt { get; set; } = false;

        // True when warehouse is a quarantine/hold area — inventory exists
        // but cannot be allocated against open orders until released.
        public bool IsQuarantine { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    // =============================================================================
    // WarehouseType — drives default posting behavior + UI affordances.
    //
    // Modeled after SAP S/4HANA EWM "Warehouse Process Type" and Dynamics 365
    // F&O "Warehouse type", filtered to the categories the IndustryOS multi-
    // vertical baseline actually needs (manufacturing + distribution + 3PL).
    // =============================================================================
    public enum WarehouseType
    {
        // Distribution / fulfillment node — bulk inbound, pick/pack outbound.
        DistributionCenter = 0,

        // Manufacturing plant — receiving + RM + WIP + FG + shipping under
        // one roof. Most ABS/EVS demos run here.
        Plant = 1,

        // Third-party logistics — title is ours but operations are outsourced.
        ThirdPartyLogistics = 2,

        // Consignment warehouse — goods here but title remains with supplier
        // (or with us at customer site).
        Consignment = 3,

        // Quarantine / hold — failed inspection, supplier hold, regulatory
        // hold. Cannot allocate to open orders until released.
        Quarantine = 4,

        // Customer returns — RMA receiving, disposition pending.
        Returns = 5,

        // Scrap / write-off staging.
        Scrap = 6,

        // Work-in-process staging (between work centers).
        WorkInProcess = 7,

        // In-transit pseudo-warehouse — inventory leaving but not yet
        // received elsewhere.
        InTransit = 8,

        Other = 99
    }
}
