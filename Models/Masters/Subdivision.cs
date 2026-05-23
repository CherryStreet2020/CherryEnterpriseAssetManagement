using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // Sprint 13.5 PRA-2 — Subdivision master (ISO 3166-2).
    //
    // State / province / territory / federal-district within a Country.
    // System-wide (NOT tenant-scoped). UNIQUE (CountryId, Code) — code is
    // the ISO 3166-2 subdivision code without the country prefix
    // (e.g. "CA" for California, not "US-CA"). The leading country prefix
    // can be reconstructed at the UI from Country.Alpha2.
    //
    // Seeded for US (50 states + DC + 5 territories), CA (10 provinces +
    // 3 territories), MX (32 states). Other countries seeded shallow
    // until a customer needs the depth.
    //
    // Source: ISO 3166-2:2020.
    [Table("Subdivisions")]
    public class Subdivision
    {
        public int Id { get; set; }

        public int CountryId { get; set; }
        public Country? Country { get; set; }

        // ISO 3166-2 subdivision code (no country prefix). E.g. "CA" for
        // California, "ON" for Ontario, "JAL" for Jalisco. UNIQUE per
        // CountryId.
        [Required, StringLength(8)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Subdivision type (per ISO 3166-2 category). Stored as enum so
        // UI/voice can disambiguate "state" vs "province" vs "territory"
        // for the right language. UI strings live in the
        // SubdivisionType enum's Display attributes.
        public SubdivisionType Type { get; set; } = SubdivisionType.State;

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum SubdivisionType : short
    {
        State = 0,
        Province = 1,
        Territory = 2,
        FederalDistrict = 3,
        AutonomousRegion = 4,
        Department = 5,
        Prefecture = 6,
        Other = 99
    }
}
