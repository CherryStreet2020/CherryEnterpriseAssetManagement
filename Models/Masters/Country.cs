using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // Sprint 13.5 PRA-2 — Country master (ISO 3166-1).
    //
    // System-wide reference table. NOT tenant-scoped (countries are universal).
    // Seeded with the eight Tier-1 trading partners customers actually use:
    // US / CA / MX / GB / DE / FR / JP / CN. Extending the list is a single
    // INSERT — no schema migration needed.
    //
    // Forward-looking: Customer.Country / Vendor.Country / billing/shipping
    // addresses currently store free-text country names. Future polish PR
    // can add CountryId FK columns alongside the legacy strings (DEF-008
    // pattern — service layer reads FK first, falls back to text).
    //
    // Source: ISO 3166-1:2020 (alpha-2 + alpha-3 + numeric).
    [Table("Countries")]
    public class Country
    {
        public int Id { get; set; }

        // ISO 3166-1 alpha-2 (e.g. "US", "CA"). PRIMARY business key —
        // every address surface stores this, not the Id.
        [Required, StringLength(2)]
        [Display(Name = "ISO Alpha-2")]
        public string Alpha2 { get; set; } = string.Empty;

        // ISO 3166-1 alpha-3 (e.g. "USA", "CAN"). Required for some
        // customs / export-control surfaces (CBP forms expect alpha-3).
        [Required, StringLength(3)]
        [Display(Name = "ISO Alpha-3")]
        public string Alpha3 { get; set; } = string.Empty;

        // ISO 3166-1 numeric (e.g. 840, 124). Stored as string so leading
        // zeros survive ("004" for Afghanistan).
        [Required, StringLength(3)]
        [Display(Name = "ISO Numeric")]
        public string Numeric { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Customers often type the "official" longer form on regulatory
        // filings (e.g. "United States of America"). Optional — UI uses
        // Name as the display label.
        [StringLength(200)]
        public string? OfficialName { get; set; }

        // ITU-T E.164 country calling code (e.g. "+1", "+44"). Stored
        // including the "+" prefix. Optional — empty for entries where
        // calling code doesn't apply.
        [StringLength(8)]
        public string? CallingCode { get; set; }

        // Top-level currency code (ISO 4217). Most countries have one;
        // some (Cuba, Panama, Zimbabwe) have multiple — UI lets the
        // operator pick on PO/invoice forms.
        [StringLength(3)]
        public string? DefaultCurrencyCode { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Subdivision>? Subdivisions { get; set; }
    }
}
