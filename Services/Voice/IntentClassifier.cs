using System.Text.RegularExpressions;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 — Phase 1 keyword intent classifier.
//
// MOVED 2026-05-21 from Endpoints/VoiceInvokeEndpoint.cs so the new
// HybridIntentRouter (PR #3) can call into it from Services/Voice without
// reaching into the endpoint's internal namespace. Types are now public.
//
// This is the fast-path layer of the hybrid router:
//   - Keyword.Classify(utterance) → ParsedIntent
//   - Returns IntentKind.Unknown when no keyword matches
//
// The hybrid router (IHybridIntentRouter) calls this first. On Unknown,
// it falls through to vector cosine search against the Embeddings table.
// Both layers reuse this ExtractNaturalKey for natural-key extraction
// (RCPT-NNN, HEAT-NNN, etc.) since the regex is layer-agnostic.

public enum IntentKind
{
    Unknown = 0,
    ExpectedArrivals,
    LookupReceipt,
    ExplainException,
    Help,
}

public sealed record ParsedIntent(IntentKind Kind, string? NaturalKey);

public static class IntentClassifier
{
    // Match a receipt-ish natural key: RCPT-2026-1234, LOT-XYZ, SN-ABC123,
    // or any token of 4+ alphanumeric chars containing at least one digit.
    private static readonly Regex NaturalKeyPattern = new Regex(
        @"\b((?:RCPT|LOT|SN|ASN|PO|HEAT|REC)[-_][A-Za-z0-9\-]{2,30}|[A-Z]{2,5}\d{2,}\-?[A-Za-z0-9\-]{0,20}|\d{6,})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ParsedIntent Classify(string raw)
    {
        var s = raw.ToLowerInvariant();

        // HELP intents
        if (s.Contains("help") || s.Contains("what can you do") || s.Contains("how do i"))
        {
            return new ParsedIntent(IntentKind.Help, null);
        }

        // EXPLAIN intents — must come before LOOKUP so "explain receipt X" is
        // routed to explain, not lookup.
        if (s.Contains("explain") || s.Contains("why is") || s.Contains("why was") ||
            s.Contains("what's wrong") || s.Contains("what is wrong"))
        {
            var key = ExtractNaturalKey(raw);
            return new ParsedIntent(IntentKind.ExplainException, key);
        }

        // LOOKUP intents
        if (s.Contains("find") || s.Contains("look up") || s.Contains("lookup") ||
            s.Contains("show me") || s.Contains("pull up") || s.Contains("locate"))
        {
            var key = ExtractNaturalKey(raw);
            if (!string.IsNullOrEmpty(key))
            {
                return new ParsedIntent(IntentKind.LookupReceipt, key);
            }
        }

        // EXPECTED ARRIVALS intents
        if (s.Contains("arriv") || s.Contains("expected") || s.Contains("coming in") ||
            s.Contains("what's coming") || s.Contains("incoming") || s.Contains("on the way") ||
            s.Contains("what's on the dock") || s.Contains("dock today"))
        {
            return new ParsedIntent(IntentKind.ExpectedArrivals, null);
        }

        // Fallback: if the utterance contains a receipt-shaped natural key,
        // treat as a lookup intent.
        var fallbackKey = ExtractNaturalKey(raw);
        if (!string.IsNullOrEmpty(fallbackKey))
        {
            return new ParsedIntent(IntentKind.LookupReceipt, fallbackKey);
        }

        return new ParsedIntent(IntentKind.Unknown, null);
    }

    /// <summary>
    /// Extract a natural key (RCPT-NNN, HEAT-XXX, etc.) from a raw utterance.
    /// Public so the vector-fallback path in HybridIntentRouter can re-use it
    /// after vector routing settles the IntentKind.
    /// </summary>
    public static string? ExtractNaturalKey(string raw)
    {
        var m = NaturalKeyPattern.Match(raw);
        return m.Success ? m.Value : null;
    }
}
