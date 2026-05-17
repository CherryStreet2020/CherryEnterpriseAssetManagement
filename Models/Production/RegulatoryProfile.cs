using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.14 — RegulatoryProfile.
    //
    // Per-regulation-regime configuration. When a MaterialStructure
    // (Bom or Recipe) must conform to a specific regime — FDA 21 CFR
    // 820 (medical devices), AS9100 (aerospace), NADCAP (special
    // processes), REACH (EU chemicals) — the profile gates per-record
    // behavior at runtime: which fields are mandatory, which
    // approvals are required, what retention applies, what audit
    // events to fire.
    //
    // Why config not schema:
    //   Plex's pre-configured-industry-SKU pattern. ADR-013
    //   §"Recommendation" item 6: "lot/serial regulation as policy,
    //   not schema." Same lot+serial schema everywhere; the profile
    //   toggles which gates fire.
    //
    // The Gates jsonb column carries the actual rules:
    //   {
    //     "requireSerialOnReceipt": true,
    //     "requireHeatNumberOnReceipt": true,
    //     "requireMrbDispositionOnQuarantine": true,
    //     "minimumRetentionYears": 40,
    //     "requirePyrometryChart": true,
    //     "requireOperatorBadge": "level-3",
    //     "auditEvents": ["receipt", "issue", "release", "ship"]
    //   }
    //
    // Each regime ships with a sensible default Gates payload that
    // tenants can override. Service-layer code reads the profile and
    // enforces accordingly.
    //
    // Reference: ADR-013 §"Recommendation" item 6 + Plex pre-configured
    // industry SKU pattern.
    [Table("RegulatoryProfiles")]
    public class RegulatoryProfile
    {
        public int Id { get; set; }

        [Required]
        [StringLength(64)]
        public string Name { get; set; } = string.Empty;

        public RegulatoryRegime Regime { get; set; } = RegulatoryRegime.None;

        [StringLength(500)]
        public string? Description { get; set; }

        // True when a real regulator (FDA, EASA, FAA, EU REACH) issues
        // the rule. False for internal corporate policy profiles.
        public bool IsExternalRegime { get; set; } = true;

        // Mandatory retention years for records covered by this regime.
        // 40 for AS9100, 7 for SOX, 10 for FDA 21 CFR 820, etc.
        public int? MinimumRetentionYears { get; set; }

        // Whether this profile is active. Inactive profiles stay in
        // place for historical records but aren't picked for new ones.
        public bool IsActive { get; set; } = true;

        // The actual gate rules. jsonb shape documented in file-level
        // comment.
        [Column(TypeName = "jsonb")]
        public string? Gates { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }

    // Regulatory regimes. Add new values as new verticals onboard —
    // migration-free. Plus regime-specific defaults can ship as seed
    // data later.
    public enum RegulatoryRegime
    {
        None = 0,
        FDA_21CFR820 = 1,         // FDA Medical Devices QSR
        FDA_21CFR210 = 2,         // FDA Pharma Manufacturing cGMP
        FDA_21CFR211 = 3,         // FDA Pharma Finished cGMP
        FDA_21CFR111 = 4,         // FDA Dietary Supplements
        AS9100 = 5,               // Aerospace QMS
        AS9145 = 6,               // Aerospace APQP / PPAP
        NADCAP_AC7102 = 7,        // Heat Treat
        NADCAP_AC7108 = 8,        // Chemical Processing
        IATF_16949 = 9,           // Automotive QMS
        ISO_13485 = 10,           // Medical Devices QMS (intl)
        ISO_45001 = 11,           // OH&S Management
        ISO_22000 = 12,           // Food Safety
        REACH = 13,               // EU Chemicals
        RoHS = 14,                // EU Restriction of Hazardous Substances
        Other = 99,
    }
}
