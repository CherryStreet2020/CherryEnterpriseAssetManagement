using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    // Sprint 2 PR #117.1 — Real sensor history table.
    //
    // Replaces the PR #117 shortcut that wrote directly to the cached
    // Asset.CurrentTemperature/Vibration/Pressure columns. Those columns
    // stay as a denormalized snapshot for fast Plant Floor renders, but
    // the source of truth is THIS table now. Every sensor sample lands
    // here first; AssetSensorService writes-then-updates the cache atom.
    //
    // Schema is deliberately lean. Each row is one scalar reading from
    // one sensor on one asset at one point in time. Composite index on
    // (AssetId, ReadingType, ReadingAt DESC) makes the "latest reading
    // per type" and 30-day sparkline queries fast.
    //
    // Retention: 30 days rolling by default; a future cleanup job will
    // prune older rows. The cached Asset.Current* columns are not subject
    // to retention.
    //
    // Source field: "demo", "iot:{deviceId}", "manual", or any future
    // ingestion channel.
    public enum SensorReadingType
    {
        Temperature = 0,    // °F
        Vibration = 1,      // mm/s RMS
        Pressure = 2,       // PSI
        Current = 3,        // Amps (reserved for motors/drives)
        Speed = 4,          // RPM (reserved for rotating equipment)
        Flow = 5,           // GPM (reserved for hydraulic/pneumatic)
        Power = 6           // kW (reserved for energy monitoring)
    }

    public class AssetSensorReading
    {
        public int Id { get; set; }

        [Required]
        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        [Required]
        public SensorReadingType ReadingType { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,4)")]
        public decimal Value { get; set; }

        [Required, StringLength(20)]
        public string Unit { get; set; } = string.Empty;

        [Required]
        public DateTime ReadingAt { get; set; }

        [Required, StringLength(100)]
        public string Source { get; set; } = "demo";

        // Optional: out-of-spec flag the ingestion service sets when the
        // reading is outside the asset's expected operating range. Used
        // by AssetHealthService to count recent breaches without
        // re-evaluating thresholds on every health calc.
        public bool IsOutOfSpec { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
