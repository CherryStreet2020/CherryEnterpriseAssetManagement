using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Models.Quality
{
    // ================================================================
    // Sprint 13.5 PR #1.75 — AS9102 First Article Inspection workflow.
    //
    // Three tables modeling the AS9102 Rev C standard:
    //   - FaiReports              (Form 1 header + lifecycle)
    //   - FaiCharacteristics       (Form 3 — per-balloon dim check rows)
    //   - FaiProductAccountability (Form 2 — material / spec / process / test rows)
    //
    // Plus 3 nullable FK columns on existing `Attachments` so ballooned
    // drawings, CMM reports, material certs, part photos, and test
    // documents tie back to the FAI parent.
    //
    // Research: docs/research/customerproject-field-set.md (style) +
    //   memory/research_fai_workflow_schema.md (this PR's design memo).
    //
    // CUSTOMER DRIVERS:
    //   - ABS Machining Thursday demo: canonical part "2P156324-B1"
    //     Power Frame Slip for Weir Oil & Gas has 126-row dimensional
    //     check sheet + ballooned multi-page drawing PDF today.
    //   - EVS June 3: "why is FAI X late?" voice query.
    //
    // KEY DESIGN CALLS (per research §3-§5):
    //   1. Three tables, not five. Form 2 collapses Material / Special-
    //      Process / FunctionalTest into one wide table with EntryType
    //      discriminator.
    //   2. Reuse Attachments — no FaiAttachment table. Add 3 nullable FKs
    //      + new AttachmentSource/Category values.
    //   3. Numeric + text fields SIDE-BY-SIDE on FaiCharacteristic.
    //      Drawing dims like "Ø.31 thru 3/8-16 UNC-2B" don't fit numeric
    //      only. Jsonb-only is anti-pattern.
    //   4. Snapshot Form 1 text fields alongside FKs. FAI is legal-audit
    //      evidence — must survive Item/Customer rename.
    //   5. AI fields mirror PR #1.5 (6-field pattern). RiskScore is
    //      DETERMINISTIC from NonConformCount + Status (NOT LLM).
    //   6. Status-regression trigger blocks Approved → Draft (only
    //      Voided is legal post-Approved). Append-only discipline.
    //   7. ChainOfCustody integration via string constants (PR #2+).
    //      Emit FaiCharacteristic nodes ONLY on non-conform rows — a
    //      126-row FAI would otherwise blow up the graph.
    //   8. Denormalized counts on FaiReports (CharacteristicCount /
    //      NonConformCount / WaivedCount). Cockpit can't aggregate
    //      126-row child per render.
    //
    // DEAN'S CALLS (2026-05-23, "nothing but the best"):
    //   - FaiNumber: per-tenant monotonic ("FAI-2026-00042"). Cleaner
    //     than per-project, matches NumberSequenceService pattern.
    //   - BaselineFaiReportId: ships NOW (nullable self-FK). Required
    //     by AS9102 Rev C for Partial/Delta FAI to point at the Full
    //     FAI baseline. Deferring would force a follow-up migration.
    //   - MrbDispositionId on FaiCharacteristic: FK to existing
    //     MrbDispositions entity (Sprint 3 Phase E.1) — full traceability
    //     for non-conform rows.
    // ================================================================

    // ----------------------------------------------------------------
    // FaiReport — AS9102 Form 1 header + lifecycle.
    // ----------------------------------------------------------------
    [Table("FaiReports")]
    public class FaiReport
    {
        public long Id { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // Per-tenant monotonic ("FAI-2026-00042"). Service layer assigns
        // via NumberSequenceService with prefix=FAI-{YYYY}-.
        [Required, StringLength(50)]
        public string FaiNumber { get; set; } = string.Empty;

        // FAI itself can rev (Rev 1 = original; Rev 2 = updated).
        public short Revision { get; set; } = 1;

        // 0=Full, 1=Partial, 2=Delta (AS9102 Rev C addition).
        public FaiType Type { get; set; } = FaiType.Full;

        // 0=Detail (single part), 1=Assembly (per Rev C section 4).
        public FaiPartType PartType { get; set; } = FaiPartType.Detail;

        // 0=NewPart..7=Other. See enum below for full list.
        public FaiReason Reason { get; set; } = FaiReason.NewPart;

        // Rev C requires reason text on every FAI type.
        public string? ReasonText { get; set; }

        // ---- FK identity (resolves Part / Drawing / Project / etc.) ----

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public int? ItemRevisionId { get; set; }

        // Nullable: internal-quality FAI may have no project linkage.
        public int? CustomerProjectId { get; set; }
        public CustomerProject? CustomerProject { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        // The job that produced the first article (nullable: incoming
        // FAI off a StockReceipt has no internal job).
        public int? ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        // Incoming-FAI link (subcontract return).
        public int? StockReceiptId { get; set; }
        public StockReceipt? StockReceipt { get; set; }

        // Subcontract FAI.
        public int? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        // BASELINE LINEAGE (Dean's call 2026-05-23): Partial/Delta FAI
        // points at the Full FAI it amends. NULL for Full FAIs.
        // CHECK: if Type IN (Partial, Delta) then BaselineFaiReportId
        // SHOULD be set (service-layer enforced).
        public long? BaselineFaiReportId { get; set; }
        public FaiReport? BaselineFaiReport { get; set; }

        // ---- Snapshot text fields (audit-legal — survive rename) ----

        [Required, StringLength(100)]
        public string PartNumberSnapshot { get; set; } = string.Empty;

        [StringLength(200)]
        public string? PartNameSnapshot { get; set; }

        [StringLength(100)]
        public string? DrawingNumberSnapshot { get; set; }

        [StringLength(20)]
        public string? DrawingRevSnapshot { get; set; }

        [StringLength(100)]
        public string? SerialNumberSnapshot { get; set; }

        [StringLength(100)]
        public string? LotNumberSnapshot { get; set; }

        // Mill cert thread.
        [StringLength(100)]
        public string? HeatNumberSnapshot { get; set; }

        // Customer's PO that birthed this work.
        [StringLength(100)]
        public string? CustomerPoSnapshot { get; set; }

        [StringLength(200)]
        public string? OrganizationName { get; set; }

        [StringLength(200)]
        public string? SupplierName { get; set; }

        [StringLength(200)]
        public string? ManufacturingProcessRef { get; set; }

        // ---- Workflow lifecycle ----

        public FaiStatus Status { get; set; } = FaiStatus.Draft;

        [DataType(DataType.Date)]
        public DateTime? FirstArticleProducedAt { get; set; }

        public DateTime? InspectionStartedAt { get; set; }
        public DateTime? InspectionCompletedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }
        public int? SubmittedById { get; set; }
        public User? SubmittedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }
        // AS9102 Rev C: ApprovedById != SubmittedById (service-layer enforced).
        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        [StringLength(100)]
        public string? ApprovedByName { get; set; }

        // Some customers lapse FAI after N years per their contract.
        [DataType(DataType.Date)]
        public DateTime? ExpiresAt { get; set; }

        // ---- Denormalized counts (refreshed by IFaiService on child save) ----

        public int CharacteristicCount { get; set; } = 0;
        public int NonConformCount { get; set; } = 0;
        public int WaivedCount { get; set; } = 0;

        // ---- AI fields (mirror PR #1.5 CustomerProjects pattern) ----

        public string? AiSummaryText { get; set; }

        [StringLength(64)]
        public string? AiSummaryModel { get; set; }

        public DateTime? AiSummaryGeneratedAt { get; set; }
        public DateTime? AiRefreshLockedUntil { get; set; }

        // 0..100, DETERMINISTIC from NonConformCount + Status (NOT LLM).
        // CHECK ck_faireports_airiskscore_range enforces 0..100.
        public short? AiRiskScore { get; set; }

        // Reuses Projects.RiskTone enum (Green/Amber/Red).
        public RiskTone? AiRiskTone { get; set; }

        // ---- Audit ----

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // Navigation
        public ICollection<FaiCharacteristic>? Characteristics { get; set; }
        public ICollection<FaiProductAccountability>? ProductAccountability { get; set; }
    }

    // ----------------------------------------------------------------
    // FaiCharacteristic — AS9102 Form 3 per-balloon dimensional row.
    // The 126-row sheet ABS uses today.
    // ----------------------------------------------------------------
    [Table("FaiCharacteristics")]
    public class FaiCharacteristic
    {
        public long Id { get; set; }

        public long FaiReportId { get; set; }
        public FaiReport? FaiReport { get; set; }

        // Free-text balloon ref ("1.1", "2.3"). Matches the ABS sheet.
        [Required, StringLength(20)]
        public string BalloonNumber { get; set; } = string.Empty;

        // E.g. "S1-B7" = sheet 1, grid B7.
        [StringLength(20)]
        public string? DrawingZone { get; set; }

        public short? DrawingPageNumber { get; set; }

        // E.g. "Ø.31 thru 3/8-16 UNC-2B thread"
        [Required, StringLength(500)]
        public string CharacteristicDescription { get; set; } = string.Empty;

        public FaiMeasurementType MeasurementType { get; set; } = FaiMeasurementType.Dimension;

        // ---- Numeric tolerance fields (when applicable) ----

        [Column(TypeName = "decimal(18,6)")]
        public decimal? NominalValue { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? UpperToleranceValue { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? LowerToleranceValue { get; set; }

        [StringLength(20)]
        public string? UnitOfMeasure { get; set; }

        // ---- Text tolerance fields (when dim is non-numeric like a thread) ----

        // Stores the raw text as it appears on the drawing.
        [StringLength(500)]
        public string? RequirementText { get; set; }

        [StringLength(200)]
        public string? ToleranceText { get; set; }

        // ---- Actual measurement ----

        [Column(TypeName = "decimal(18,6)")]
        public decimal? ActualResult { get; set; }

        [StringLength(500)]
        public string? ActualText { get; set; }

        public FaiConformance Conformance { get; set; } = FaiConformance.Conforms;

        // ---- Inspection metadata ----

        public int? InspectorId { get; set; }
        public User? Inspector { get; set; }

        [StringLength(100)]
        public string? InspectorName { get; set; }    // snapshot

        public DateTime? InspectionDate { get; set; }

        // E.g. "Caliper SN ABC-123" or "CMM program PRG-456".
        [StringLength(200)]
        public string? InstrumentUsed { get; set; }

        // ---- Non-conformance handling ----

        public string? NonConformanceNotes { get; set; }

        // FK to existing MrbDispositions (Sprint 3 Phase E.1) for full
        // non-conform traceability.
        public int? MrbDispositionId { get; set; }
        public MrbDisposition? MrbDisposition { get; set; }

        // ---- Audit ----

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }

    // ----------------------------------------------------------------
    // FaiProductAccountability — AS9102 Form 2 row (one wide table with
    // EntryType discriminator). Material certs, specifications, special
    // processes, functional tests all flow through here.
    // ----------------------------------------------------------------
    [Table("FaiProductAccountability")]
    public class FaiProductAccountability
    {
        public long Id { get; set; }

        public long FaiReportId { get; set; }
        public FaiReport? FaiReport { get; set; }

        public FaiAccountabilityType EntryType { get; set; } = FaiAccountabilityType.Material;

        // Description / name of the material, spec, process, or test.
        [Required, StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Spec / standard reference ("AMS 5643", "MIL-A-8625", "AMS 2759/3").
        [StringLength(100)]
        public string? SpecReference { get; set; }

        // Specific cert # ("Heat 12345", "Cert 67890") — for material certs.
        [StringLength(100)]
        public string? CertificateNumber { get; set; }

        // Heat number (mill cert thread). When EntryType=Material, links
        // back to MaterialMaster via this text (Sprint 3 Phase E.2 has
        // HeatNumber as a snapshot field on MaterialMaster).
        [StringLength(100)]
        public string? HeatNumber { get; set; }

        // Lot number (per-lot certs).
        [StringLength(100)]
        public string? LotNumber { get; set; }

        // The vendor / supplier who provided the cert or performed the
        // special process. Free text + optional FK.
        [StringLength(200)]
        public string? SupplierName { get; set; }

        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        // For functional tests — the result.
        [StringLength(500)]
        public string? TestResult { get; set; }

        // For functional tests — a pass/fail rollup.
        public FaiConformance? Conformance { get; set; }

        // Linkage to actual MaterialMaster row when traceable.
        public int? MaterialMasterId { get; set; }

        // Linkage to StockReceipt when traceable.
        public int? StockReceiptId { get; set; }

        public string? Notes { get; set; }

        // ---- Audit ----

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }

    // ================================================================
    // Enums (AS9102 Rev C aligned)
    // ================================================================

    public enum FaiType : short
    {
        Full = 0,        // Complete FAI on all characteristics
        Partial = 1,     // Subset (e.g. only changed dims) per Rev C
        Delta = 2        // Delta against a baseline FAI per Rev C
    }

    public enum FaiPartType : short
    {
        Detail = 0,      // Single part
        Assembly = 1     // Assembly of multiple parts (Rev C section 4)
    }

    public enum FaiReason : short
    {
        NewPart = 0,
        DesignChange = 1,
        LocationChange = 2,
        ProcessChange = 3,
        LapseGreaterThan2Years = 4,
        CustomerRequest = 5,
        ToolingChange = 6,
        Other = 7
    }

    // Workflow state. Append-only discipline — Postgres trigger
    // fn_block_fai_status_regression blocks illegal Status transitions.
    public enum FaiStatus : short
    {
        Draft = 0,
        InProgress = 1,
        Submitted = 2,
        Approved = 3,
        // Customer accepted with conditions (e.g. waiver on one dim).
        Conditional = 4,
        Rejected = 5,
        // After-the-fact reversal. Preserves audit. TERMINAL.
        Voided = 6
    }

    public enum FaiMeasurementType : short
    {
        Dimension = 0,
        Geometric = 1,       // GD&T
        Surface = 2,
        Material = 3,
        Visual = 4,
        Test = 5
    }

    public enum FaiConformance : short
    {
        Conforms = 0,
        NonConforms = 1,
        Conditional = 2,     // Pass with waiver
        Waived = 3,          // Skipped per customer/engineer concession
        NotApplicable = 4
    }

    public enum FaiAccountabilityType : short
    {
        Material = 0,         // Raw material cert (heat, mill cert)
        Specification = 1,    // ASTM / AMS / MIL spec reference
        SpecialProcess = 2,   // Heat treat, plate, anodize, etc.
        FunctionalTest = 3    // Hardness test, load test, pressure test
    }
}
