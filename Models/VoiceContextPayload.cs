using System;

namespace Abs.FixedAssets.Models;

// ADR-014 D1 — Per-page context payload exposed to the voice-AI layer.
//
// Every Razor page that inherits from VoiceReadyPageModel can override
// BuildContextPayload() to populate this DTO. The IVoiceContextEmitter
// page filter calls BuildContextPayload() post-handler and writes the
// result into HttpContext.Items["voice.ctx"] for the future /_voice/context
// endpoint or MCP server to read.
//
// What goes in here:
//   - Route + user + role + tenant are auto-filled by the base class
//   - EntityType + EntityId + RelatedIds are page-specific (e.g., a
//     WorkOrders/Details.cshtml page populates these from its loaded
//     entity)
//   - FocusedField is for "the operator was typing in the Description
//     field when they hit the mic" — drives smarter intent classification
//
// What does NOT go in here:
//   - Full entity contents (the AI fetches what it needs via separate
//     tool calls — this is a *pointer*, not a document)
//   - User PII beyond UserId + Roles (privacy minimization)
//   - Secrets, tokens, credentials
//
// Reference: ADR-014 §"Decisions" D1.
public sealed class VoiceContextPayload
{
    public string Route { get; init; } = "";

    // Stringly-typed to avoid forcing all FK shapes (int vs uuid vs
    // composite) through this DTO. The AI receives whatever the
    // entity's natural identifier is.
    public string? UserId { get; init; }
    public string[] Roles { get; init; } = Array.Empty<string>();
    public string? TenantId { get; init; }

    // What entity is being viewed. Pages override.
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }

    // IDs of related entities currently visible on the page (BOM line
    // IDs, allocation IDs, etc.). Drives "this BOM line" disambiguation
    // when the operator says "raise the quantity on the third row."
    public string[] RelatedIds { get; init; } = Array.Empty<string>();

    // Set from the ?focus= query string when the in-page voice client
    // attaches the currently-focused field name.
    public string? FocusedField { get; init; }

    // Tab / pane within the page, if relevant. Free-text 64 chars.
    public string? Tab { get; init; }

    // When the payload was built. Useful for staleness detection in
    // multi-turn conversation state.
    public DateTime BuiltAt { get; init; } = DateTime.UtcNow;
}
