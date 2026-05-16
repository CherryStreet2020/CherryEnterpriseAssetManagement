using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — Denormalized 1-row-per-(asset, type) read
    // cache that powers the Plant Floor view.
    //
    // The ingest writer (PR #118.2 / SensorIngestService) updates this
    // table atomically with every SensorEvent insert. Plant Floor
    // queries hit it as a single point-lookup join keyed by AssetId
    // (and optionally ReadingType when filtering one tile).
    //
    // Composite PK (AssetId, ReadingType) is declared in OnModelCreating.
    //
    // Replaces the legacy Asset.CurrentTemperature / CurrentVibration /
    // CurrentPressure denormalized columns from PR #117.1, which are
    // marked [Obsolete] in PR #118.3 and dropped in PR #118.5.
    [Table("AssetSensorLatest")]
    public class AssetSensorLatest
    {
        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public SensorReadingType ReadingType { get; set; }

        [Column(TypeName = "numeric(14,4)")]
        public decimal Value { get; set; }

        public UnitOfMeasure Unit { get; set; }

        public DateTime ReadingAt { get; set; }

        public DeviceHealthCode QualityCode { get; set; }

        public bool IsOutOfSpec { get; set; }

        // View-side classification stamped at write time by the ingest
        // service. Maps to the Plant Floor tile color: "ok" / "warn" /
        // "crit" / "muted". Stored (not computed at read) so the Plant
        // Floor page does zero math.
        [Required, StringLength(8)]
        public string Tone { get; set; } = "muted";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
