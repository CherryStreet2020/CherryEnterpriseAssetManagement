using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-6 — TaxAuthority master.
    //
    // The agency that issues a tax obligation. TaxCode rows point to a
    // TaxAuthority. Example authorities: IRS (US Federal), CA-CRA (Canada
    // Federal), US-CA-CDTFA (California State), EU member-state ministries.
    //
    // Separate from TaxCode because:
    //   (a) One authority can administer multiple TaxCodes (US-IRS administers
    //       Federal income tax, Federal excise tax, FICA, etc.).
    //   (b) Filing frequency + agency URL are authority-level, not code-level.
    //   (c) Future rate-effective-date tables (PRA-10) hang off the authority
    //       + jurisdiction combo.
    //
    // CROSS-TENANT REFERENCE pattern: CompanyId NULL = system, set = tenant.
    //
    // FOREWARD-LOOKING:
    //   - TaxCode.TaxAuthorityId (added in this PR) points here.
    //   - PRA-10 will add TaxRate (effective-dated rates per authority +
    //     jurisdiction + product class).
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.3
    //   - memory: reference_master_files_baseline.md
    // =============================================================================
    [Table("TaxAuthorities")]
    public class TaxAuthority
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        // Stable code (e.g. "US-IRS", "CA-CRA", "US-CA-CDTFA", "US-NY-DTF",
        // "CA-ON-MOF", "EU-VAT-EE"). UPPERCASE, hyphen-delimited.
        [Required, StringLength(32)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        // ISO 3166-1 alpha-2 country code the authority operates in.
        [Required, StringLength(2)]
        public string CountryCode { get; set; } = "US";

        // ISO 3166-2 subdivision code if the authority is sub-national
        // (e.g. "US-CA", "US-NY", "CA-ON", "DE-BY"). NULL for federal-level
        // authorities (IRS, CRA, etc.).
        [StringLength(8)]
        public string? SubdivisionCode { get; set; }

        public TaxAdministrativeLevel AdministrativeLevel { get; set; } = TaxAdministrativeLevel.Federal;

        public TaxFilingFrequency FilingFrequency { get; set; } = TaxFilingFrequency.Quarterly;

        [StringLength(500)]
        public string? AgencyUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    public enum TaxAdministrativeLevel
    {
        Federal = 0,
        State = 1,          // US state, Mexican state
        Province = 2,       // Canadian province, German Bundesland
        County = 3,         // US county, UK county
        City = 4,           // US city, Canadian municipality
        Other = 99
    }

    public enum TaxFilingFrequency
    {
        Monthly = 0,
        Quarterly = 1,
        SemiAnnual = 2,
        Annual = 3,
        OnEvent = 4,        // Per-transaction (e.g. excise on shipment)
        OnRequest = 99
    }
}
