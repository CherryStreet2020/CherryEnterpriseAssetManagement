using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    // Sprint 13.5 PRA-1 — first-class Carrier master.
    //
    // Replaces free-text `Carrier` column on `AdvancedShippingNotice` and
    // `ShippingMethod`. Required for PR #5 Customer Project Cockpit shipment
    // chips and downstream Shipping CC (Sprint 18).
    //
    // v1 scope (per audit): code + name + SCAC + tracking URL + contact +
    // API endpoint scaffold. EDI ingestion + carrier API integration defer
    // to Sprint 18.
    //
    // System-wide carriers seeded with CompanyId NULL (UPS / FedEx / DHL /
    // USPS / OnTrac / XPO / OD / YRC / Saia / pickup / will call). Tenants
    // can fork system carriers by creating their own row with CompanyId set.
    //
    // Source: docs/research/master-files-audit.md §3 + .ship/drafts/
    // sprint-13.5-PRA1-master-files.sql §5.
    [Table("Carriers")]
    public class Carrier
    {
        public int Id { get; set; }

        // NULL CompanyId = system-wide (seeded). Set CompanyId to fork a
        // system carrier for a tenant. UNIQUE (COALESCE(CompanyId,0), Code).
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required, StringLength(10)]
        public string Code { get; set; } = string.Empty;

        // Standard Carrier Alpha Code (4-char industry-standard ID).
        [StringLength(4)]
        [Display(Name = "SCAC Code")]
        public string? ScacCode { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ContactName { get; set; }

        [StringLength(100)]
        public string? ContactEmail { get; set; }

        [StringLength(30)]
        public string? ContactPhone { get; set; }

        [StringLength(200)]
        public string? WebsiteUrl { get; set; }

        // Tracking URL template with {0} placeholder for tracking number.
        // E.g. "https://www.fedex.com/fedextrack/?trknbr={0}".
        [StringLength(300)]
        public string? TrackingUrlTemplate { get; set; }

        // API endpoint for carrier integration (Sprint 18 wires this).
        [StringLength(300)]
        public string? ApiEndpoint { get; set; }

        // Reference to API auth credentials (Secrets/KeyVault key name, not
        // the secret itself). Sprint 18 wires this.
        [StringLength(100)]
        public string? ApiAuthRef { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedAt { get; set; }
    }
}
