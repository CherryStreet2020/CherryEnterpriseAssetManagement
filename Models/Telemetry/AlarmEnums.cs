namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — ISA-18.2 alarm management enums.
    //
    // ISA-18.2 (Management of Alarm Systems for the Process Industries,
    // ANSI/ISA-18.2-2016; EEMUA 191 is the European equivalent) defines
    // the lifecycle, priorities, shelving rules, suppression-by-mode,
    // and flood-handling for industrial alarms.
    //
    // Our SensorAlarm row models a single alarm instance from open
    // (out-of-spec sensor breach) through ack and clear. ShelvedUntil
    // and SuppressionMode follow the ISA-18.2 rules verbatim:
    //
    //   - Shelving REQUIRES an expiry — operators can't permanently
    //     hide alarms by accident. ShelvedUntil is non-null when
    //     State = Shelved.
    //   - SuppressionMode is context-based — alarms can be auto-hidden
    //     during Startup / Shutdown / Maintenance / Calibration so the
    //     operator isn't flooded during transient or planned states.

    public enum AlarmState : byte
    {
        Open = 0,            // out-of-spec condition active, not yet acknowledged
        Acknowledged = 1,    // operator has seen and is working on it
        Cleared = 2,         // condition resolved (sensor back in spec or manual clear)
        Shelved = 3,         // explicitly hidden by an operator until ShelvedUntil
        Suppressed = 4,      // context-suppressed (Startup/Shutdown/Maintenance/Calibration)
    }

    public enum AlarmPriority : byte
    {
        // ISA-18.2 four-level priority. Each level has a documented
        // Target Response Time (TRT) and operator-burden allocation.
        P1_Emergency = 1,    // immediate operator action; safety/environmental consequence
        P2_High      = 2,    // prompt action; significant consequence
        P3_Medium    = 3,    // routine response; moderate consequence
        P4_Low       = 4,    // informational; log-only
    }

    public enum AlarmSuppressionMode : byte
    {
        // Context modes during which lower-priority alarms can be
        // auto-suppressed to keep operator burden under ISA-18.2's
        // 6-alarms-per-10-minute manageable threshold.
        Startup = 0,
        Shutdown = 1,
        Maintenance = 2,
        Calibration = 3,
        ManualOverride = 4,
    }
}
