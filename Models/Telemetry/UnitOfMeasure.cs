namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — Typed Units of Measure per ISO 80000 (the
    // international standard for quantities and units) and UNECE
    // Recommendation 20 (Codes for Units of Measure used in
    // International Trade — the source of the three-letter codes like
    // "CEL" for degrees Celsius, "PSI" for pounds per square inch).
    //
    // SensorEvent.Unit and AssetSensorLatest.Unit use this enum instead
    // of a free-form string so:
    //   - cross-asset comparisons are correct (no "PSI" vs "psi" vs
    //     "lbf/in2" string-comparison bugs)
    //   - the UnitConversion table provides authoritative conversions
    //     when a customer's display preference differs from the stored
    //     source-of-truth unit
    //   - FDA submissions and partner integrations can emit the UNECE
    //     code verbatim (UnitConversion.UneceCode)
    //
    // Numbering reserves ranges so future additions don't reshuffle:
    //   100-199  Temperature
    //   200-299  Pressure
    //   300-399  Rotational
    //   400-499  Length / displacement
    //   500-599  Vibration / motion
    //   600-699  Flow
    //   700-799  Electrical
    //   800-899  Reserved (mass / chemical / radiation — add as needed)
    //   900-999  Dimensionless / counts / categorical
    public enum UnitOfMeasure : short
    {
        // ---- Temperature (100s) ----
        DegreesCelsius     = 100,
        DegreesFahrenheit  = 101,
        Kelvin             = 102,

        // ---- Pressure (200s) ----
        PSI                = 200,   // pounds per square inch
        Bar                = 201,
        KiloPascal         = 202,
        MegaPascal         = 203,
        InchesOfWater      = 204,

        // ---- Rotational (300s) ----
        RPM                = 300,   // revolutions per minute
        RadiansPerSecond   = 301,

        // ---- Length (400s) ----
        Millimeters        = 400,
        Meters             = 401,
        Inches             = 402,
        Feet               = 403,

        // ---- Vibration (500s) ----
        // ISO 10816 velocity (mm/s RMS) is the dominant unit for
        // industrial vibration severity; gravity-force (g) is used
        // for acceleration-based diagnostics.
        MillimetersPerSecond = 500,
        InchesPerSecond      = 501,
        GravityForce         = 502,
        MetersPerSecondSquared = 503,

        // ---- Flow (600s) ----
        LitersPerMinute      = 600,
        CubicMetersPerHour   = 601,
        GallonsPerMinute     = 602,
        CubicFeetPerMinute   = 603,

        // ---- Electrical (700s) ----
        Volts                = 700,
        Amperes              = 701,
        Watts                = 702,
        KiloWatts            = 703,
        KiloWattHours        = 704,
        PowerFactor          = 705,
        HertzAC              = 706,

        // ---- Dimensionless / counts (900s) ----
        Percent              = 900,
        Ratio                = 901,
        Count                = 902,
        Boolean              = 903,
        Decibels             = 904,
        PartsPerMillion      = 905,
    }
}
