using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — Unit-of-measure conversion table.
    //
    // SensorEvent.Value is stored in a canonical source-of-truth
    // unit. The display unit may differ per site / per tenant
    // (a German pharma plant prefers °C/Bar; a Texas plant prefers
    // °F/PSI). At render time the Plant Floor tile applies the
    // conversion using rows from this table.
    //
    // Conversions are affine: ConvertedValue = Multiplier * Value + Offset
    // For non-affine conversions (rare in process variables) we'd
    // add a Function discriminator; not needed today.
    //
    // UneceCode carries the UNECE Recommendation 20 three-letter code
    // (e.g. "CEL", "PSI", "RPM") that FDA submissions and partner
    // integrations expect on export.
    //
    // The seeder ships the standard set on first run. Tenants can
    // add custom conversions via /Admin/Lookups.
    [Table("UnitConversions")]
    public class UnitConversion
    {
        public int Id { get; set; }

        public UnitOfMeasure FromUnit { get; set; }

        public UnitOfMeasure ToUnit { get; set; }

        [Column(TypeName = "numeric(20,10)")]
        public decimal Multiplier { get; set; } = 1m;

        [Column(TypeName = "numeric(20,10)")]
        public decimal Offset { get; set; } = 0m;

        // UNECE Recommendation 20 code for the ToUnit. Used on export
        // (FDA submissions, OPC UA EU codes, partner JSON). Optional —
        // dimensionless units may not have a UNECE code.
        [StringLength(8)]
        public string? UneceCode { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool Active { get; set; } = true;
    }
}
