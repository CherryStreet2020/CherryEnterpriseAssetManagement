using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Catalog
{
    // Sprint 2 PR #117.2 — Per-class sensor definitions. Replaces the
    // universal Temp/Vib/PSI hardcoded thresholds from PR #117/#117.1.
    //
    // A CNC machining center has 4-6 sensors:
    //   - Spindle bearing temperature (°C, 30-65)
    //   - Spindle vibration RMS (mm/s, 0.5-2.5)
    //   - Spindle load (% nameplate, 20-80)
    //   - Coolant pressure (bar, 4-12)
    // A welding power source has very different ones:
    //   - Arc voltage (V, 18-32)
    //   - Wire feed rate (in/min, 100-500)
    //   - Duty cycle (% per 10 min, 30-100)
    //   - Power supply temp (°C, 25-60)
    //
    // The AssetSensorService and the Plant Floor view both read from
    // SensorProfile to determine what to render, what range is "normal",
    // and at what threshold to flag IsOutOfSpec. No more hardcoded
    // switch statements per asset class — adding a new class is a row
    // insert in EQUIPMENT_CATALOG.md.
    public class SensorProfile
    {
        public int Id { get; set; }

        [Required]
        public int EquipmentClassId { get; set; }
        public EquipmentClass? EquipmentClass { get; set; }

        // Display label on the Plant Floor card ("Spindle Temp", "Arc Voltage").
        [Required, StringLength(80)]
        public string SensorName { get; set; } = string.Empty;

        // Reuses the existing AssetSensorReading.SensorReadingType enum.
        // (Temperature, Vibration, Pressure, Current, Speed, Flow, Power,
        // and extension values added in PR #117.2 for Voltage / Hours /
        // Cycles / Humidity / DutyCycle.)
        [Required]
        public SensorReadingType ReadingType { get; set; }

        [Required, StringLength(20)]
        public string Unit { get; set; } = string.Empty;

        // Normal operating envelope. Readings inside [NormalMin, NormalMax]
        // are "green". The seeder draws baseline values from this band.
        [Column(TypeName = "decimal(14,4)")]
        public decimal NormalMin { get; set; }

        [Column(TypeName = "decimal(14,4)")]
        public decimal NormalMax { get; set; }

        // Warning band — readings beyond this trigger yellow on the card.
        // Optional; if null, only Critical is used.
        [Column(TypeName = "decimal(14,4)")]
        public decimal? WarningThreshold { get; set; }

        // Critical threshold — readings beyond this set IsOutOfSpec=true and
        // contribute to HealthScore sensor-breach penalty.
        [Column(TypeName = "decimal(14,4)")]
        public decimal? CriticalThreshold { get; set; }

        // Whether the critical threshold is breached when value EXCEEDS it
        // (true, e.g. temperature) or FALLS BELOW it (false, e.g. coolant
        // pressure dropping is bad). Default: true (high-side breach).
        public bool BreachOnHighSide { get; set; } = true;

        // How often a real sensor would emit a reading. Seeder uses this
        // for baseline (typical = 60 minutes). Storyline assets get
        // higher resolution (15 minutes) over the last 7 days regardless.
        public int SampleRateMinutes { get; set; } = 60;

        // The 2-3 "primary" sensors per class show on the Plant Floor card
        // pills. Others surface on the asset detail view only.
        public bool IsPrimary { get; set; }

        // Display order within the class.
        public int DisplayOrder { get; set; } = 100;

        [StringLength(500)]
        public string? Notes { get; set; }

        public System.DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
        public System.DateTime UpdatedAt { get; set; } = System.DateTime.UtcNow;
    }
}
