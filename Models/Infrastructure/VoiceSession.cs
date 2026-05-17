using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Infrastructure;

// ADR-014 D8 — Postgres-backed voice session state.
//
// Sprint 5 will write to this. Phase F infrastructure PR just ships
// the table + entity so the schema is in place.
//
// Why Postgres, not Session/Redis:
//   - Multi-device handoff: operator starts a voice flow on the iPad,
//     finishes on the desktop. Session-cookie state breaks this.
//   - Durability: voice session state surviving a worker restart
//     matters for long planning flows ("show me what's overdue" ->
//     review for 90 seconds -> "now approve them").
//   - Tenant isolation: jsonb state column with row-level visibility
//     is the simplest enforcement.
//   - Replit operates Postgres; no Redis provisioned.
//
// StateJson shape (illustrative, evolves with Sprint 5):
// {
//   "lastResult": { "entityType": "PurchaseOrder", "cartIds": [1,2,3] },
//   "turnHistory": [
//     { "turn": 1, "userText": "show me everything at McMaster", ... },
//     { "turn": 2, "userText": "place the PO", ... }
//   ]
// }
//
// Reference: ADR-014 §"Decisions" D8.
[Table("VoiceSessions")]
public class VoiceSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public int TenantId { get; set; }

    public int UserId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastTurnAt { get; set; } = DateTime.UtcNow;

    // jsonb — shape evolves with Sprint 5. Empty object on insert.
    [Column(TypeName = "jsonb")]
    public string StateJson { get; set; } = "{}";

    // Default 4 hours from creation; service layer extends on each turn.
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(4);
}
