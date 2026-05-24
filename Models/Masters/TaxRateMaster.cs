using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-10 — TaxRateMaster (effective-dated tax rate matrix).
    //
    // Master Files Baseline cascade ship #8 of 10. Closes the "Real
    // TaxAuthority+TaxRate effective-dated" gap from
    // docs/research/master-files-baseline-2026-05-24.md §6.8.
    //
    // POSITION IN THE STACK:
    //   TaxAuthority   (PRA-6 — who collects the tax)
    //   TaxCodeMaster  (PRA-6 — what kind of tax obligation)
    //   TaxRateMaster  (THIS — the actual rate at a point in time and place)
    //
    // RESOLUTION at invoice/shipment time:
    //   1. Identify TaxCode for the line (from Item.TaxCodeId or PriceListLine).
    //   2. Identify Jurisdiction tuple (CountryCode, SubdivisionCode, PostalCode)
    //      from CustomerShipTo / SiteLocation.
    //   3. Look up TaxRateMaster WHERE
    //        TaxCodeMasterId = tax_code
    //        AND CountryCode = jurisdiction.country
    //        AND (SubdivisionCode IS NULL OR SubdivisionCode = jurisdiction.state)
    //        AND EffectiveFromUtc <= sale_date
    //        AND (EffectiveToUtc IS NULL OR EffectiveToUtc > sale_date)
    //        AND (AppliesToItemGroupId IS NULL OR AppliesToItemGroupId = item.group)
    //   4. Most-specific match wins (jurisdiction granularity + product-class match).
    //   5. Apply Rate * line_subtotal = tax_amount, respect thresholds / caps.
    //
    // TIERED + THRESHOLD support (luxury tax, Social Security wage base, etc.):
    //   - MinThresholdAmount = "this rate kicks in above $X" (luxury surtax)
    //   - MaxThresholdAmount = "this rate caps at $Y" (FICA wage base)
    //   - For complex tiers (progressive brackets), service layer reads multiple
    //     TaxRateMaster rows for the same Code with non-overlapping thresholds.
    //
    // COMPOUNDING: Quebec QST applies ON TOP of GST (effectively GST and QST
    // are applied sequentially, not in parallel). IsCompounded=TRUE flags that
    // this rate applies to (subtotal + already-computed-tax) instead of subtotal.
    //
    // CROSS-TENANT REFERENCE pattern:
    //   CompanyId NULL = system template (US-CA-SALES base, CA-GST, EU-VAT std)
    //   CompanyId set  = tenant override / extension (city-level rates, special
    //                    contract rates, customer-specific tax exemption rates)
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.8
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    //   - SAP FI condition records (KONP/KONV/T030K)
    //   - Vertex O Series / Avalara AvaTax effective-dated rate model
    //   - ISO 3166-1 alpha-2 country codes / ISO 3166-2 subdivision codes
    //
    // NAMING: *Master suffix continues PRA-6 precedent. There's no legacy
    // TaxRate class to collide with today, but the suffix keeps the family
    // (CurrencyMaster / PaymentTermMaster / TaxCodeMaster / LaborRateMaster /
    // PriceListMaster / TaxRateMaster) visually consistent.
    // =============================================================================
    [Table("TaxRateMasters")]
    public class TaxRateMaster
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        // Stable code (e.g. "US-CA-SALES-7.25", "CA-ON-HST-13", "VAT-DE-19",
        // "VAT-DE-7-REDUCED", "US-FED-FICA-EE-6.20"). Hyphen-delimited.
        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // FK to TaxCodeMaster (PRA-6) — the tax SHAPE this rate belongs to.
        public int TaxCodeMasterId { get; set; }
        public TaxCodeMaster? TaxCodeMaster { get; set; }

        // FK to TaxAuthority (PRA-6) — denormalized for query speed
        // (also reachable via TaxCodeMaster.TaxAuthorityId). NULL allowed only
        // for the NOTAX/EXEMPT sentinel rates.
        public int? TaxAuthorityId { get; set; }
        public TaxAuthority? TaxAuthority { get; set; }

        // ---------------------------------------------------------------------
        // JURISDICTION.
        // ---------------------------------------------------------------------

        // ISO 3166-1 alpha-2 country code (e.g. "US", "CA", "DE", "GB"). REQUIRED.
        [Required, StringLength(2)]
        public string CountryCode { get; set; } = "US";

        // ISO 3166-2 subdivision (e.g. "US-CA", "US-NY", "CA-ON", "DE-BY").
        // NULL for federal-level / EU-wide rates.
        [StringLength(8)]
        public string? SubdivisionCode { get; set; }

        // Postal-code prefix when the rate varies sub-state (e.g. SF Bay Area
        // vs San Bernardino County). NULL = applies to entire subdivision.
        [StringLength(16)]
        public string? PostalCodePrefix { get; set; }

        public TaxJurisdictionLevel JurisdictionLevel { get; set; } = TaxJurisdictionLevel.State;

        // ---------------------------------------------------------------------
        // RATE.
        // ---------------------------------------------------------------------

        public TaxRateType RateType { get; set; } = TaxRateType.Standard;

        // Rate as a fraction (0.072500 = 7.25%). 6 decimal places for the
        // occasional weird basis-point rates (Korean VAT 10.00%, Iceland VAT
        // 24.50%, etc.).
        [Column(TypeName = "numeric(7,6)")]
        public decimal Rate { get; set; }

        // Threshold under which this rate does NOT apply (luxury tax above $X).
        // NULL = no minimum.
        [Column(TypeName = "numeric(18,4)")]
        public decimal? MinThresholdAmount { get; set; }

        // Cap above which this rate stops applying (FICA wage base, capped
        // surtaxes). NULL = no cap.
        [Column(TypeName = "numeric(18,4)")]
        public decimal? MaxThresholdAmount { get; set; }

        // True when the rate applies on top of already-computed tax (not just
        // the subtotal). Quebec QST sits on top of GST in this way.
        public bool IsCompounded { get; set; } = false;

        // ---------------------------------------------------------------------
        // SCOPE.
        // ---------------------------------------------------------------------

        // Restrict rate to specific ItemGroup (e.g. food at reduced VAT, fuel
        // at fuel-tax rate). NULL = applies to all item groups.
        public int? AppliesToItemGroupId { get; set; }

        // Free-form product class tag for finer scope (e.g. "DIGITAL-SERVICE",
        // "PROFESSIONAL-SERVICE", "ALCOHOL", "TOBACCO"). NULL = no class filter.
        [StringLength(64)]
        public string? AppliesToProductClass { get; set; }

        // ---------------------------------------------------------------------
        // EFFECTIVE-DATING.
        // ---------------------------------------------------------------------
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        public DateTime? EffectiveToUtc { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    // =============================================================================
    // TaxJurisdictionLevel — how deep does the rate apply?
    // =============================================================================
    public enum TaxJurisdictionLevel
    {
        Federal = 0,                // US Federal, CA Federal (GST), EU-wide VAT
        State = 1,                  // US state, CA province (HST/PST)
        Province = 2,               // synonym for State in non-US contexts
        County = 3,                 // US county sales-tax overlay
        City = 4,                   // US city sales tax / municipal
        SpecialDistrict = 5,        // transit district / improvement district overlays
        Combined = 6,               // pre-computed combined rate (e.g. "CA total 9.75%")
        Other = 99
    }

    // =============================================================================
    // TaxRateType — what kind of rate is this?
    // =============================================================================
    public enum TaxRateType
    {
        Standard = 0,               // The default rate for this tax (US sales tax, EU VAT 19-25%)
        Reduced = 1,                // EU VAT reduced (food / books, typically 5-10%)
        SuperReduced = 2,           // EU VAT super-reduced (rare — Spain food at 4%, etc.)
        ZeroRated = 3,              // 0% but the supply IS within the tax system (exports, books in UK)
        Exempt = 4,                 // Outside the tax system entirely (financial services)
        Luxury = 5,                 // Surtax on items above MinThresholdAmount
        Excise = 6,                 // Per-unit tax (fuel cents/gallon, tobacco $/pack)
        WithholdingIncome = 7,      // Payroll withholding income tax
        WithholdingFica = 8,        // FICA / Medicare / Social Security
        Threshold = 9,              // Stepwise tiered (progressive bracket — multiple rows together)
        Other = 99
    }
}
