using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13a — ProcessBatch subtype of ProductionBatch.
    //
    // The "special-process" satellite: heat treat / paint / plating /
    // anodize / e-coat / passivation / wash / leak test / pressure test.
    // One physical execution (one furnace load, one paint setup, one
    // tank cycle) aggregating parts across many parent production
    // orders.
    //
    // Relationship: 1:0..1 with ProductionBatch.
    //   - UNIQUE on ProductionBatchId, ON DELETE CASCADE
    //   - Set only when ProductionBatch.BatchType = ProcessBatch
    //
    // Regulatory drivers (per the PR #119.13a research report):
    //   - NADCAP AC7102 (heat treat) requires recipe FK, furnace ID,
    //     time-temp chart, operator ID, rack/fixture position
    //   - NADCAP AC7108 (chemical processing) requires tank ID + chem
    //     log + plating thickness measurement
    //   - AS9100 / AS9145 requires lot genealogy + witness coupons +
    //     20-40 yr retention
    //   - IATF 16949 CQI-9 requires TUS / SAT cal records + continuous
    //     pyrometer chart per batch
    //
    // What this PR ships:
    //   - Process-type discriminator + the common per-type scalars
    //   - HeatTreatChartUrl + WitnessCouponLotId nullable FK (stubs)
    //
    // What lands later (PR #119.13c):
    //   - Full HeatTreatChart table (per-thermocouple chart files)
    //   - TankChemistryReading audit-trail child
    //   - WitnessCoupon real content schema
    //   - PyrometryCal (TUS/SAT) records
    //
    // Reference: PR #119.13a research report Q2 + Q7 + recommended
    // schema §3.
    [Table("ProcessBatches")]
    public class ProcessBatch
    {
        public int Id { get; set; }

        public int ProductionBatchId { get; set; }
        public ProductionBatch? ProductionBatch { get; set; }

        // What kind of special-process is this batch running.
        public ProcessType ProcessType { get; set; } = ProcessType.HeatTreat;

        // ---- Heat-treat fields ----
        [Display(Name = "Setpoint Temp (°C)")]
        [Column(TypeName = "decimal(8,2)")]
        public decimal? SetpointTempC { get; set; }

        [Display(Name = "Soak Time (min)")]
        public int? SoakTimeMinutes { get; set; }

        [StringLength(32)]
        public string? QuenchMedium { get; set; }     // "Oil", "Water", "Polymer", "Air", "Vacuum"

        [StringLength(32)]
        public string? AtmosphereType { get; set; }   // "Air", "Nitrogen", "Argon", "Endothermic", "Vacuum"

        // ---- Paint / coating fields ----
        [StringLength(32)]
        public string? ColorCode { get; set; }        // "RAL9010", "FED-STD-595-17875", or shop code

        // FK to a paint lot — paint batch traceability. Items table
        // already holds paint as a stockable item; this is the specific
        // can/pail consumed. Stays nullable until PR #119.13b's
        // StockReceipts table is wired.
        public int? PaintBatchLotItemId { get; set; }

        // ---- Plating / anodize / chemical-process fields ----
        [StringLength(64)]
        public string? ChemistrySpec { get; set; }    // "MIL-A-8625 Type II Class 2", "ASTM B633 SC1", etc.

        [Display(Name = "Tank Concentration %")]
        [Column(TypeName = "decimal(6,3)")]
        public decimal? TankConcentrationPct { get; set; }

        [Display(Name = "Bath pH")]
        [Column(TypeName = "decimal(4,2)")]
        public decimal? BathPh { get; set; }

        // ---- Universal load metrics ----
        // Furnace-load mass for NADCAP energy reporting + per-load-mass
        // cost allocation method.
        [Display(Name = "Load Mass (kg)")]
        [Column(TypeName = "decimal(10,3)")]
        public decimal? LoadMassKg { get; set; }

        // External storage pointer to the pyrometry chart / time-temp
        // chart blob. AS9100 + NADCAP AC7102 + CQI-9 all require
        // continuous recording per batch. We do NOT store the chart
        // in-app; just the link.
        [StringLength(500)]
        [Display(Name = "Time-Temp Chart URL")]
        public string? HeatTreatChartUrl { get; set; }

        // Witness coupon lot FK — for AS9100 hydrogen-embrittlement
        // bake records (ASTM F519) and aerospace lot genealogy. Stub
        // FK now; full WitnessCoupon table lands in PR #119.13c.
        public int? WitnessCouponLotId { get; set; }

        // Rack / fixture / basket location — NADCAP AC7102 6.x
        // requires recording how parts were arranged in the load.
        [StringLength(256)]
        public string? RackPositionNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    // Process-type discriminator. Covers the dominant special-process
    // categories per the research survey. Adding a new value (e.g.,
    // Cryogenic, ShotPeening, ZincPlating) is migration-free — just
    // an enum value bump.
    public enum ProcessType
    {
        HeatTreat = 0,
        Paint = 1,
        Plating = 2,
        Anodizing = 3,
        Ecoat = 4,
        Passivation = 5,
        Wash = 6,
        LeakTest = 7,
        PressureTest = 8,
        Coating = 9,
        Other = 99,
    }
}
