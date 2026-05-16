namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — IEC 62443 / ISA-99 security zone, expressed
    // via the Purdue Reference Model levels.
    //
    // Every SensorEvent carries the zone of its origin so a defense-in-
    // depth review can attribute every ingested reading to a controlled
    // network segment. IEC 62443's zones-and-conduits model uses Purdue
    // levels as the canonical segmentation; we tag at the ingest call.
    //
    //   L0Process       — physical sensors and actuators on the floor
    //   L1Control       — PLCs and DCS controllers driving the process
    //   L2Supervisory   — SCADA / HMI workstations supervising L1
    //   L3Operations    — MES / CMMS — Cherry's natural home
    //   (L4 ERP and L5 enterprise zones are outside sensor ingest scope)
    //
    // /api/v1/sensors/events accepts only L0-L3 source zones and
    // requires the caller to authenticate as a gateway entitled to
    // post on behalf of that zone. PR #122 (RLS) will add a
    // zone-scoped row-level policy so cross-zone reads can be
    // restricted per tenant compliance requirements.
    public enum PurdueZone : byte
    {
        L0Process = 0,
        L1Control = 1,
        L2Supervisory = 2,
        L3Operations = 3,
    }
}
