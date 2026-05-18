using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-015 — Industry-Agnostic Receipt Schema.
    //
    // Catalog of receipt "profiles" — one row per industry vertical
    // that defines the schema for the StockReceipt.Attributes jsonb
    // payload. Same pattern as RegulatoryProfile (PR #119.14 / #216)
    // but at the receipt level rather than the regulation level.
    //
    // Why this exists:
    //   StockReceipt is the physical-lot record that arrives at the
    //   dock. Steel needs heatNumber + mill + dimensions; pharma needs
    //   lot + expiry + GTIN + serial; food needs traceabilityLotCode +
    //   harvest date + COA URL. Hard-coding all of them as nullable
    //   columns produces column sprawl and namespace collisions; pure
    //   polymorphism multiplies tables; EAV is ~1000× slower than JSONB
    //   on Postgres benchmarks.
    //
    //   ReceiptProfile + JSON Schema + UiFormSpec is what SAP MM, Oracle
    //   Cloud, NetSuite, and D365 F&O all converged on independently
    //   (research: docs/research/industry-agnostic-receipt-schema.md).
    //
    // What each column does:
    //   JsonSchema       — JSON Schema (Draft 2020-12) describing the
    //                      shape of StockReceipt.Attributes for receipts
    //                      with this profile. Validated by the service
    //                      layer at create/update via JsonSchema.Net.
    //   PromotedFacets   — list of jsonb keys to expose via expression
    //                      indexes for fast equality / range queries
    //                      (e.g. ["heatNumber","mill"] for STEEL).
    //   DefaultAttributes — defaults seeded into Attributes on new
    //                      receipt creation (rare per-profile defaults;
    //                      most defaults come from the Item master).
    //   UiFormSpec       — the dynamic Edit-page form description.
    //                      Groups, fields, types, labels, voice hints,
    //                      and (per ADR-015 D10 spike) `scope`,
    //                      `exampleQueries`, `disambiguation`,
    //                      `semanticAction` keys for voice-AI grounding.
    //   RegulatoryProfileIds — which RegulatoryProfile gates fire on
    //                          receipt of this type (e.g. AS9100 for
    //                          AEROSPACE, FDA 21 CFR 211 for PHARMA).
    //
    // 12 starter profiles ship with the seed migration:
    //   STEEL · PHARMA · FOOD · CHEMICAL · ELECTRONICS · MEDICAL_DEVICE
    //   · AEROSPACE · CANNABIS · AUTOMOTIVE · APPAREL · CONSTRUCTION
    //   · OIL_GAS
    //
    // Tenants may add their own profiles or fork the starters without
    // a code deploy.
    //
    // Reference: ADR-015 D2 + D10 spike report.
    [Table("ReceiptProfiles")]
    public class ReceiptProfile
    {
        public int Id { get; set; }

        // Stable, machine-friendly code: STEEL, PHARMA, FOOD, etc.
        [Required]
        [StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(128)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // JSON Schema for Attributes payload. NOT NULL — every profile
        // must declare its shape. May be a minimal `{"type":"object"}`
        // for a permissive profile.
        [Required]
        [Column(TypeName = "jsonb")]
        public string JsonSchema { get; set; } = "{}";

        // Postgres text[] of jsonb keys to expression-index. Defaults
        // to empty array; migration adds the indexes for STEEL's
        // ["heatNumber","mill"], PHARMA's ["expirationDate","ndc"], etc.
        // Stored as jsonb-of-array for portability across providers.
        [Required]
        [Column(TypeName = "jsonb")]
        public string PromotedFacets { get; set; } = "[]";

        // Defaults seeded into Attributes on new receipt creation.
        [Required]
        [Column(TypeName = "jsonb")]
        public string DefaultAttributes { get; set; } = "{}";

        // Razor Edit page renders dynamically from this. Carries:
        //   groups[].title
        //   groups[].fields[].key / label / type / voice / required /
        //                       scope / exampleQueries /
        //                       disambiguation / semanticAction
        // See docs/research/voice-ai-spike-adr015-d10.md §6 for the
        // full field-spec contract.
        [Required]
        [Column(TypeName = "jsonb")]
        public string UiFormSpec { get; set; } = "{}";

        // RegulatoryProfile IDs that fire on receipt of this type.
        // Postgres int[] stored as jsonb array of ints for portability.
        [Required]
        [Column(TypeName = "jsonb")]
        public string RegulatoryProfileIds { get; set; } = "[]";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }
}
