namespace Abs.FixedAssets.Models;

// ADR-014 D3 — Discriminator for who initiated an AuditLog row.
//
// Default 0 = User means the existing AuditService.LogAsync calls
// (from before this PR) keep working unchanged. AI-mediated actions
// will set ActorKind = AiOnBehalfOf and populate the 6 other AI
// columns on AuditLog (OnBehalfOfUserId, AiSessionId, AiCommandText,
// AiModelVersion, AiToolName, AiConfidence).
//
// System is reserved for daemon / scheduled-job actions (e.g.,
// nightly depreciation post). Adding a 4th value later is migration-
// free; this enum is small and won't ossify.
//
// Reference: ADR-014 §"Decisions" D3 + Microsoft Purview
// CopilotInteraction schema convention (human is always the principal
// actor; AI is metadata).
public enum ActorKind
{
    User = 0,
    AiOnBehalfOf = 1,
    System = 2,
}
