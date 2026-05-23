using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13a — ProductionBatch polymorphic parent.
    //
    // Shared-operation batching: one physical execution, many parent
    // production orders, proportional cost-allocation back. The pattern
    // shows up across the shop:
    //   - Nest (cutting batch)
    //   - ProcessBatch (heat treat / paint / plating / anodize / coating)
    //   - Future: AssemblyBatch, TestBatch
    //
    // Closest industry analog is SAP PP-PI "Production Campaign"
    // (transactions PCA1/PCA2/PCA3). NOT "Collective Order" — that's
    // SAP's term for vertical BOM coupling, not horizontal multi-order
    // sharing. Tulip's Composable MES handles this via apps over a
    // generic table; CherryAI EAM goes one level deeper with the
    // polymorphic parent + per-type satellite pattern.
    //
    // Why polymorphic vs flat:
    //   - Common columns (number, status, schedule, operator, audit,
    //     allocation method) belong on the parent so cost-allocation
    //     queries are uniform across batch types
    //   - Per-type fields (sheet utilization on Nest; setpoint temp
    //     on heat treat) live on subtype tables
    //   - Mirrors ADR-013's MaterialStructure → Bom / Recipe pattern
    //
    // Why AllocationMethod is on the PARENT (not on each allocation row):
    //   ASC 330 inventory-valuation consistency. Auditors require one
    //   systematic allocation method per batch; per-row variation is an
    //   audit finding. Move-the-method-up call is research-validated
    //   against PwC / Deloitte / BEC published guidance.
    //
    // Why RecipeRevisionId FK from day one (vs varchar):
    //   The #1 documented v1-migration regret across NADCAP / AS9100
    //   shops. Recipe traceability per batch is required by AC7102 for
    //   heat treat and AC7108 for chemical processing. Retrofitting a
    //   varchar to a FK after 6 months of production data is brutal.
    //   Recipe table is a stub now; content schema lands in PR #119.14.
    //
    // Status machine:
    //   Planned -> Loaded -> Running -> Completed -> Closed
    //                     \-> Hold -> (back to Running, or Cancelled)
    //                     \-> Quarantined -> ReleasedAfterReview -> Closed
    //                                     \-> Cancelled (MRB scrap)
    //                     \-> Cancelled (terminal)
    //
    // Quarantined is a STATE not a flag (AS9100 MRB workflow demands
    // explicit transition + disposition FK). MrbDisposition is a stub
    // in this PR; full workflow in PR #119.13c.
    [Table("ProductionBatches")]
    public class ProductionBatch
    {
        public int Id { get; set; }

        // Sprint 13.5 PR #5c.2 — Direct tenant scoping (no longer parent-scoped only).
        // Both NOT NULL because every batch physically lives at exactly one site.
        // Backfilled in migration 20260524120000_TenantScopingHardeningPr5c2 via
        // PrimaryEquipment (Asset.CompanyId/LocationId) with ProductionBatchAllocation
        // → ProductionOrder fallback. UNIQUE on (CompanyId, LocationId, BatchNumber)
        // replaces the global BatchNumber UNIQUE that was the P0 cross-tenant leak.
        public int CompanyId { get; set; }
        public int LocationId { get; set; }

        // Human-facing identifier — "BATCH-2026-00042" or shop convention.
        // UNIQUE per (CompanyId, LocationId) — "BATCH-001" at Houston shop is a
        // different batch from "BATCH-001" at Dallas shop in the same company.
        [Required]
        [StringLength(32)]
        [Display(Name = "Batch #")]
        public string BatchNumber { get; set; } = string.Empty;

        // Discriminator. The renderer in Phase F branches on this; the
        // 1:0..1 subtype table (Nests or ProcessBatches) carries the
        // type-specific fields.
        public ProductionBatchType BatchType { get; set; } = ProductionBatchType.Nest;

        // Status — see file-level state-machine comment.
        public ProductionBatchStatus Status { get; set; } = ProductionBatchStatus.Planned;

        // Routing-side pool tag. Operations with the same code are
        // eligible-to-batch together. Example values: "HT-1450-OIL"
        // (heat treat at 1450 oil quench), "PAINT-RAL9010", "PLATE-NI-30um".
        // Free text 64 chars — every shop coins its own taxonomy; an
        // enum here would force a migration per customer. A future
        // BatchPoolDefinition lookup table can govern master data
        // without changing this column's type.
        [StringLength(64)]
        [Display(Name = "Batch Pool Code")]
        public string? BatchPoolCode { get; set; }

        // Single-equipment FK for the 80% case (one furnace, one paint
        // booth, one laser). Plating lines and multi-zone furnaces use
        // the ProductionBatchEquipmentLink child table instead/in addition.
        public int? PrimaryEquipmentId { get; set; }
        public Asset? PrimaryEquipment { get; set; }

        // Recipe traceability per batch. NADCAP AC7102 / AC7108 require
        // a controlled recipe identifier traceable to part number.
        // RecipeRevision is a stub table in this PR — content schema
        // lands in #119.14 (Phase E.3).
        public int? RecipeRevisionId { get; set; }
        public RecipeRevision? RecipeRevision { get; set; }

        [Display(Name = "Scheduled Start")]
        public DateTime? ScheduledStartAt { get; set; }

        [Display(Name = "Actual Start")]
        public DateTime? ActualStartAt { get; set; }

        [Display(Name = "Actual End")]
        public DateTime? ActualEndAt { get; set; }

        // Operator + supervisor for AS9100 8.5.2 traceability. Both
        // nullable — a Planned batch has no operator yet.
        public int? OperatorUserId { get; set; }
        public int? SupervisorUserId { get; set; }

        // Cost-allocation method. ONE method per batch (ASC 330
        // consistency). Each allocation row carries the measurement
        // (basis) and the resulting amount, never the method choice.
        public AllocationMethod AllocationMethod { get; set; } = AllocationMethod.PerPiece;

        [Display(Name = "Total Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? TotalCost { get; set; }

        // Hold reason — set when Status transitions to Hold. Cleared
        // when status moves back to Running.
        [StringLength(256)]
        public string? HoldReason { get; set; }

        // MRB (Material Review Board) disposition FK — set when
        // Status transitions to Quarantined and an MRB outcome is
        // recorded. MrbDisposition is a stub table in this PR.
        public int? QuarantineDispositionId { get; set; }
        public MrbDisposition? QuarantineDisposition { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        // Audit fields — same convention as WorkOrder + ProductionOrder.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        // Optimistic concurrency via PG xmin. See
        // Data/XminRowVersionExtensions.cs.
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // ----- Navs (1:0..1 subtypes) -----

        // Set when BatchType=Nest.
        public Nest? Nest { get; set; }

        // Set when BatchType=ProcessBatch.
        public ProcessBatch? ProcessBatch { get; set; }

        // 1:N children
        public ICollection<ProductionBatchAllocation>? Allocations { get; set; }
        public ICollection<ProductionBatchEquipmentLink>? EquipmentLinks { get; set; }
        public ICollection<ProductionBatchStateEvent>? StateEvents { get; set; }
    }

    // Discriminator for ProductionBatch. Two subtypes ship in #119.13a;
    // more (AssemblyBatch, TestBatch) can be added without schema churn —
    // just a new enum value and a new satellite table.
    public enum ProductionBatchType
    {
        Nest = 0,
        ProcessBatch = 1,
    }

    // State machine. See file-level comment in ProductionBatch.cs.
    public enum ProductionBatchStatus
    {
        Planned = 0,
        Loaded = 1,
        Running = 2,
        Hold = 3,
        Completed = 4,
        Quarantined = 5,
        ReleasedAfterReview = 6,
        Closed = 7,
        Cancelled = 8,
    }

    // Cost-allocation method. Canonical set across the verticals
    // research surveyed (sheet cutting, heat treat, paint, plating).
    // See PR #119.13a research report Q8.
    public enum AllocationMethod
    {
        PerPiece = 0,
        PerArea = 1,           // 2D sheet-area (nesting)
        PerSurfaceArea = 2,    // 3D part surface (plating)
        PerWeight = 3,
        PerLoadMass = 4,       // NADCAP alias of PerWeight; explicit for HT
        PerCycle = 5,
        PerDollar = 6,
        PerLinealLength = 7,
    }
}
