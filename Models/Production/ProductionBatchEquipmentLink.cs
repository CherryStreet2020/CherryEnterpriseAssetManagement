using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13a — ProductionBatchEquipmentLink.
    //
    // Multi-equipment child table for batches that span more than one
    // machine. Required for:
    //   - Plating lines: pre-clean -> activate -> plate -> rinse -> bake
    //     each in different tanks (NADCAP AC7108 logs chemistry per
    //     tank, so per-tank records are non-negotiable)
    //   - Multi-zone furnaces: carburize -> quench may be two physical
    //     machines (NADCAP AC7102 allows multi-zone with per-zone TUS)
    //
    // Cardinality:
    //   - Many EquipmentLinks per ProductionBatch
    //   - SequenceNo orders the link rows (1 = first stage)
    //   - Role distinguishes primary execution equipment from witness /
    //     stage / monitoring equipment
    //
    // Why ship now (research-driven):
    //   "Single equipment FK -> multi-equipment child table" is the
    //   #1 documented schema migration regret across plating MES
    //   systems surveyed. Ship the child table on day one. Heat-treat
    //   shops with single-furnace flows can leave this empty and use
    //   only ProductionBatch.PrimaryEquipmentId.
    //
    // Reference: PR #119.13a research report Q5 + Q10.
    [Table("ProductionBatchEquipmentLinks")]
    public class ProductionBatchEquipmentLink
    {
        public int Id { get; set; }

        public int ProductionBatchId { get; set; }
        public ProductionBatch? ProductionBatch { get; set; }

        public int EquipmentId { get; set; }
        public Asset? Equipment { get; set; }

        // Position in the multi-stage flow. 1 = first stage. Indexed
        // composite with ProductionBatchId for ORDER BY queries.
        public int SequenceNo { get; set; } = 1;

        public BatchEquipmentRole Role { get; set; } = BatchEquipmentRole.Primary;

        // Timing — set as the load moves through. Drives per-stage
        // dwell-time analytics.
        public DateTime? EnteredAt { get; set; }
        public DateTime? ExitedAt { get; set; }

        [StringLength(256)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // Role of this equipment within the batch flow.
    public enum BatchEquipmentRole
    {
        Primary = 0,     // the main execution equipment (furnace, paint booth)
        Stage = 1,       // pipeline stage (tank, dwell oven, rinse)
        Witness = 2,     // monitoring / coupon position
    }
}
