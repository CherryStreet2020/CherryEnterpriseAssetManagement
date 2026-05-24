using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-6 — TaxCode master.
    //
    // Replaces the orphan `int? TaxCodeId` FK on Customer (added by PRA-1
    // but with no target table) + the 3-row `TaxJurisdiction.json` LookupValue
    // (US/CA/INTL labels only — no rates, no agencies, no behavior).
    //
    // TaxCode is the SHAPE of a tax obligation:
    //   - TaxAuthorityId — who administers it (PRA-6 sibling table)
    //   - IsRecoverable — VAT/GST/HST is recoverable; US sales tax isn't
    //   - IsInclusive — tax already in the displayed price (some jurisdictions)
    //   - IsReverseCharge — buyer self-assesses (EU intra-community)
    //   - GlAccountId Input/Output — recoverable input vs payable output
    //
    // RATES are SEPARATE (PRA-10 — effective-dated `TaxRate` table joining
    // TaxCode + jurisdiction + product class + effective-date range). PRA-6
    // ships the SHELL only.
    //
    // CROSS-TENANT REFERENCE pattern: CompanyId NULL = system, set = tenant.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.3
    //   - memory: reference_master_files_baseline.md
    // =============================================================================
    [Table("TaxCodeMasters")]
    public class TaxCodeMaster
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        // Stable code (e.g. "NOTAX", "US-CA-SALES-7.25", "EU-VAT-STANDARD-20",
        // "CA-GST-5", "CA-HST-13", "ZERO-RATED", "EXEMPT"). Hyphen-delimited.
        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // FK to the issuing authority. NULL only allowed for the "NOTAX"
        // sentinel; everything else must reference an authority.
        public int? TaxAuthorityId { get; set; }
        public TaxAuthority? TaxAuthority { get; set; }

        // True for VAT/GST/HST. False for US sales tax.
        public bool IsRecoverable { get; set; } = false;

        // True when the displayed unit price already includes the tax
        // (UK VAT-inclusive retail, EU consumer pricing).
        public bool IsInclusive { get; set; } = false;

        // True for EU intra-community / reverse-charge mechanism — buyer
        // self-assesses the tax instead of the seller charging it.
        public bool IsReverseCharge { get; set; } = false;

        // GL accounts. NULL during PRA-6 ship; tenant onboarding flow OR a
        // follow-up service wires these per-tenant.
        public int? InputGlAccountId { get; set; }     // recoverable side (debit on receipt)
        public int? OutputGlAccountId { get; set; }    // payable side (credit on sale)

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
