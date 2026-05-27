// Sprint 14.3 PR-7 (2026-05-27) — ChangeImpactAnalysis + ChangeImpactLine.
//
// When an Engineering Change Order (ECO) is released or implemented,
// the system performs an IMPACT ANALYSIS that walks every affected
// item, production order, deviation, CAR/CAPA, and document version
// to quantify the blast radius of the change.
//
// This is the CLOSED-LOOP mechanism that:
// 1. Identifies every in-flight production order that uses an affected part/rev
// 2. Surfaces active deviations/waivers/concessions that may conflict
// 3. Triggers FAI re-qualification per AS9102 §3.2 on form/fit/function changes
// 4. Queues customer notifications per AS9100 §8.5.6
// 5. Links redline markup annotations to the source ECO
//
// Industry context: SAP PLM calls this "Where-Used Impact Analysis."
// Oracle Agile calls it "Change Impact Report." Arena PLM calls it
// "Impact Analysis Matrix." Ours walks the FULL chain of custody:
// ECO → EcoLineItem → Item → ProductionOrder → MaterialStructureLine
// → Deviation/CAR → DocumentVersion → FAI trigger. No competitor
// walks the chain this deeply because their BOM, quality, and ECM
// modules are separate products.
//
// AS9100 §8.5.6: "The organization shall review and control changes
// to production... to the extent necessary to ensure continuing
// conformity with requirements."

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    // ────────────────────────────────────────────
    // ENUMS
    // ────────────────────────────────────────────

    /// <summary>
    /// Lifecycle status for a change impact analysis.
    /// </summary>
    public enum ImpactAnalysisStatus
    {
        Pending = 0,            // Analysis initiated, not yet complete
        Complete = 1,           // All impact lines identified
        ActionRequired = 2,     // At least one critical impact line unresolved
        AllResolved = 3,        // Every impact line resolved or acknowledged
        Cancelled = 4,
    }

    /// <summary>
    /// Type of entity affected by the engineering change.
    /// </summary>
    public enum ImpactLineType
    {
        ProductionOrder = 0,            // In-flight PRO using affected item/rev
        Deviation = 1,                  // Active deviation on affected item
        Waiver = 2,                     // Active waiver on affected item
        Concession = 3,                 // Active concession on affected item
        CorrectiveAction = 4,           // Open CAR referencing affected item
        DocumentVersion = 5,            // Document version requiring redline/supersede
        CustomerNotice = 6,             // Customer notification required
        SupplierNotification = 7,       // Supplier process change notification
        FaiRetrigger = 8,               // FAI re-qualification required (AS9102 §3.2)
        InventoryDisposition = 9,       // Existing inventory requiring disposition
    }

    /// <summary>
    /// Severity classification for an individual impact line.
    /// </summary>
    public enum ImpactSeverity
    {
        Info = 0,               // Informational — no action needed
        Warning = 1,            // Action recommended but not blocking
        Critical = 2,           // Must be resolved before change can proceed
    }

    // ────────────────────────────────────────────
    // ENTITIES
    // ────────────────────────────────────────────

    /// <summary>
    /// Header entity for a change impact analysis tied to an ECO.
    /// One analysis per ECO (1:1), created when the ECO is released.
    /// </summary>
    [Table("ChangeImpactAnalyses")]
    public class ChangeImpactAnalysis
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        // ----- Identity -----
        [Required] [StringLength(32)]
        public string AnalysisNumber { get; set; } = string.Empty;

        // ----- ECO linkage (1:1) -----
        public int EcoId { get; set; }
        public EngineeringChangeOrder? Eco { get; set; }

        // ----- Status -----
        public ImpactAnalysisStatus Status { get; set; } = ImpactAnalysisStatus.Pending;

        // ----- Summary counters (denormalized for dashboard speed) -----
        public int AffectedProductionOrderCount { get; set; }
        public int AffectedDeviationCount { get; set; }
        public int AffectedCarCount { get; set; }
        public int AffectedDocumentCount { get; set; }
        public int AffectedCustomerCount { get; set; }
        public int TotalImpactLines { get; set; }
        public int ResolvedImpactLines { get; set; }
        public int CriticalImpactLines { get; set; }

        // ----- FAI re-trigger tracking -----
        public bool RequiresFaiRetrigger { get; set; }
        public DateTime? FaiTriggeredAtUtc { get; set; }
        [StringLength(120)]
        public string? FaiTriggeredBy { get; set; }
        public int FaiReportsCreated { get; set; }

        // ----- Customer notice tracking -----
        public bool RequiresCustomerNotice { get; set; }
        public DateTime? CustomerNoticeTriggeredAtUtc { get; set; }
        [StringLength(120)]
        public string? CustomerNoticeTriggeredBy { get; set; }

        // ----- Audit -----
        public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? AnalyzedBy { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        [StringLength(120)]
        public string? CompletedBy { get; set; }

        [StringLength(4000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        [StringLength(120)]
        public string? UpdatedBy { get; set; }

        // ----- Concurrency -----
        public byte[]? RowVersion { get; set; }

        // ----- Navigation -----
        public ICollection<ChangeImpactLine> Lines { get; set; } = new List<ChangeImpactLine>();
    }

    /// <summary>
    /// Individual impact line — one per affected entity discovered during analysis.
    /// </summary>
    [Table("ChangeImpactLines")]
    public class ChangeImpactLine
    {
        public int Id { get; set; }

        // ----- Parent -----
        public int ChangeImpactAnalysisId { get; set; }
        public ChangeImpactAnalysis? Analysis { get; set; }

        // ----- What is affected -----
        public ImpactLineType LineType { get; set; } = ImpactLineType.ProductionOrder;
        public ImpactSeverity Severity { get; set; } = ImpactSeverity.Info;

        /// <summary>PK of the affected entity (PRO Id, Deviation Id, CAR Id, etc.).</summary>
        public int AffectedEntityId { get; set; }

        /// <summary>Snapshot description at analysis time (e.g. "PRO-1005 Trent XWB Bracket Assy — 47 units in-process").</summary>
        [Required] [StringLength(500)]
        public string AffectedEntityDescription { get; set; } = string.Empty;

        /// <summary>The item affected by this ECO line (nullable if the impact is not item-specific).</summary>
        public int? AffectedItemId { get; set; }
        public Item? AffectedItem { get; set; }

        // ----- Recommended action -----
        [StringLength(1000)]
        public string? RecommendedAction { get; set; }

        // ----- Resolution -----
        public bool IsResolved { get; set; }
        [StringLength(2000)]
        public string? ActionTaken { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
        [StringLength(120)]
        public string? ResolvedBy { get; set; }

        // ----- FAI linkage (when LineType = FaiRetrigger) -----
        public long? TriggeredFaiReportId { get; set; }

        // ----- Audit -----
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? CreatedBy { get; set; }
    }
}
