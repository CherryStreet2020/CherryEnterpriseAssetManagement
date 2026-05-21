using System.Threading;
using System.Threading.Tasks;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 — Hybrid Intent Router (PR #3).
//
// Two-layer voice intent classifier:
//   Layer 1: Keyword fast-path (IntentClassifier.Classify) — free,
//            microsecond latency, perfect for clear utterances.
//   Layer 2: Vector cosine search against intent prototypes embedded
//            in the Embeddings table — handles paraphrasing the keyword
//            layer would miss.
//
// Telemetry: every call records Source (Keyword/Vector/Fallback) and
// Confidence (1.0 for keyword, 1 - cosine_distance for vector) into
// AuditLog so we can tune thresholds against real usage later.
//
// ADR-025 compliance: this is an injected service, not a static method.
// VoiceInvokeEndpoint depends on IHybridIntentRouter, never on the
// IntentClassifier static class directly.

public enum RoutingSource
{
    /// <summary>Layer 1 keyword classifier matched.</summary>
    Keyword,

    /// <summary>Layer 1 returned Unknown, Layer 2 vector cosine match above threshold.</summary>
    Vector,

    /// <summary>Both layers came up empty.</summary>
    Fallback,
}

public sealed record RoutedIntent(
    ParsedIntent Intent,
    RoutingSource Source,
    double Confidence,
    string? Reason);

public interface IHybridIntentRouter
{
    /// <summary>
    /// Route a raw voice utterance to its IntentKind.
    /// </summary>
    /// <param name="utterance">Raw speech-to-text transcript.</param>
    /// <param name="tenantId">Caller's tenant ID. Used to scope the
    ///   vector search to (TenantId == caller || TenantId == 0).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<RoutedIntent> RouteAsync(string utterance, int tenantId, CancellationToken ct);
}
