namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — NAMUR NE 107 sensor self-diagnostics enum.
    //
    // NE 107 is the process-industry standard for device health states
    // emitted by intelligent field instruments. The OPC UA Companion
    // Specification PA-DIM (jointly owned by OPC Foundation, FieldComm
    // Group, NAMUR, ODVA, PROFIBUS/PROFINET, VDMA, and ZVEI) maps the
    // NE 107 namespace 1:1 onto the OPC UA DeviceHealth enumeration —
    // we store the same set of values verbatim so a Sparkplug B or
    // OPC UA gateway can write through to SensorEvent.QualityCode
    // without translation.
    //
    // Display tones (from the NAMUR NE 107 pictogram set):
    //   Good        → green   "process value is reliable"
    //   Uncertain   → yellow  "process value may be unreliable; consult
    //                          device diagnostics"
    //   Failure     → red     "value is invalid; device or process broken"
    //   Maintenance → blue    "value still valid, but device requires
    //                          maintenance soon"
    //   OutOfService→ grey    "device disabled or not reporting"
    //
    // SensorRollupMinute/Hour/Day exclude Failure and OutOfService from
    // AVG/MIN/MAX/STDDEV to avoid corrupting the time-series shape with
    // bad data; counts of each are preserved so dashboards can surface
    // data-quality issues.
    public enum DeviceHealthCode : byte
    {
        Good = 0,
        Uncertain = 1,
        Failure = 2,
        Maintenance = 3,
        OutOfService = 4,
    }
}
