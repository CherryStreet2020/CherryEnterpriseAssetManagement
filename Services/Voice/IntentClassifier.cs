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
    // Sprint 12D PR #5 / ADR-022 §D4 — chain-of-custody narration intent.
    // "trace this receipt", "where did it come from", "who supplied it",
    // "chain of custody for RCPT-XXX", "audit trail for this receipt".
    ExplainChainOfCustody,
    // Sprint 12.7 PR #3 — Controller-side source-to-GL chain trace.
    // The CFO motion. "why is NBV $1.2M on Asset 4231" / "trace asset 1116" /
    // "drill down on JE 47" / "where did the asset cost come from". Walks the
    // same chain the Controller Control Center's Drilldown tab renders, but
    // narrates each ChainStep through Cherry's voice. See
    // Services/Controller/ChainTraceService.cs + IControllerCockpitService.
    ExplainChainTrace,
    // B7 Wave D PR-2 — make-or-buy decision narration.
    // "why are we buying item 9395" / "why did we make part PN-1234" /
    // "explain the make or buy call on this bracket". Re-hydrates the item's
    // latest persisted MakeBuyDecision via IMakeBuyDecisionService.ExplainAsync
    // and narrates the verdict + rationale. See Services/Production.
    ExplainMakeBuyDecision,
}

public sealed record ParsedIntent(IntentKind Kind, string? NaturalKey);

public static class IntentClassifier
{
    // Match a receipt-ish natural key: RCPT-2026-1234, LOT-XYZ, SN-ABC123,
    // or any token of 4+ alphanumeric chars containing at least one digit.
    private static readonly Regex NaturalKeyPattern = new Regex(
        @"\b((?:RCPT|LOT|SN|ASN|PO|HEAT|REC)[-_][A-Za-z0-9\-]{2,30}|[A-Z]{2,5}\d{2,}\-?[A-Za-z0-9\-]{0,20}|\d{6,})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Sprint 12.7 PR #3 — Controller-side entity reference extractor.
    //
    // Recognises the loose grammar the Controller Drilldown tab accepts via
    // ChainTraceService.ParseEntityRef: ASSET-N / ASSET N / ASSET:N / JE-N /
    // PO-N / INV-N / WO-N, plus the bare-integer form when the rest of the
    // utterance has already established asset / je context (e.g. "trace
    // asset 4231" → "asset 4231" matches the prefixed branch; "drill down on
    // 1116" — without a prefix — falls through to the receipt-flavored
    // ExtractNaturalKey OR returns just "1116" via the bare-integer branch).
    //
    // Not anchored ^...$ on purpose — finds the FIRST such reference inside
    // free-text speech, so the handler can resolve it without making the
    // user repeat the noun.
    private static readonly Regex ControllerEntityPattern = new Regex(
        @"\b(asset|je|journal\s*entry|journal|po|purchase\s*order|purchase|inv|invoice|wo|work\s*order|work)\s*[-:#]?\s*(\d{1,9})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Bare-integer fallback for chain trace: when the prefix branch above
    // misses but the utterance has chain-trace keywords + a plain number,
    // we assume Asset (matches ChainTraceService.ParseEntityRef's default).
    private static readonly Regex BareIntegerPattern = new Regex(
        @"\b(\d{1,9})\b", RegexOptions.Compiled);

    // B7 Wave D PR-2 — make-or-buy item reference extractor. Recognises
    // "item 9395", "part PN-1234", "part number 9395", "component X", "sku Y".
    // The captured token is an item Id or part number that
    // IMakeBuyDecisionService.ExplainLatestForItemAsync resolves.
    private static readonly Regex MakeBuyItemPattern = new Regex(
        @"\b(?:item|part\s*number|part\s*no\.?|part|component|sku)\s*[-:#]?\s*([A-Za-z0-9][A-Za-z0-9\-]{0,39})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ParsedIntent Classify(string raw)
    {
        var s = raw.ToLowerInvariant();

        // HELP intents
        if (s.Contains("help") || s.Contains("what can you do") || s.Contains("how do i"))
        {
            return new ParsedIntent(IntentKind.Help, null);
        }

        // CHAIN-OF-CUSTODY intents — must come before EXPLAIN so "trace
        // this receipt" / "chain of custody" routes here, not to ExplainException.
        // Sprint 12D PR #5 / ADR-022 §D4.
        if (s.Contains("chain of custody") || s.Contains("chain-of-custody") ||
            s.Contains("trace this receipt") || s.Contains("trace receipt") ||
            s.Contains("trace back") ||
            (s.Contains("where did") && (s.Contains("come from") || s.Contains("ship from")) && !ContainsControllerSubject(s)) ||
            s.Contains("who supplied") || s.Contains("who shipped") ||
            s.Contains("audit trail") || s.Contains("provenance") ||
            (s.Contains("how did") && s.Contains("get here")))
        {
            var key = ExtractNaturalKey(raw);
            return new ParsedIntent(IntentKind.ExplainChainOfCustody, key);
        }

        // MAKE-OR-BUY intents (B7 Wave D PR-2) — must come before CHAIN-TRACE
        // and EXPLAIN so "why are we buying item 9395" / "explain the make-or-buy
        // call on part PN-1234" route to the make-or-buy narrator, not to the
        // controller chain walker or the receipt-side ExplainException handler.
        // Guarded on a make/buy subject so generic "why is..." / "explain..."
        // utterances still fall through to their existing intents.
        if (IsMakeBuyQuery(s))
        {
            var key = ExtractMakeBuyItemRef(raw);
            return new ParsedIntent(IntentKind.ExplainMakeBuyDecision, key);
        }

        // CHAIN-TRACE intents (Sprint 12.7 PR #3 — CFO motion) — must come
        // before generic EXPLAIN so "why is NBV on asset 4231" / "trace
        // asset 1116" / "drill down on JE 47" / "where did the asset cost
        // come from" route to the Controller chain walker, not to the
        // receipt-side ExplainException handler.
        //
        // Two firing patterns:
        //   (1) An explicit trace verb + controller subject: "trace asset
        //       1116", "drilldown on je 47", "walk the chain for asset 4231",
        //       "show the source-to-gl chain", "explain asset 1116".
        //   (2) A diagnostic verb ("why is/was", "explain") combined with a
        //       controller subject ("nbv" / "net book value" / "asset" /
        //       "depreciation" / "journal entry" / "je-" / "asset-").
        if (HasChainTraceVerb(s) || (HasDiagnosticVerb(s) && ContainsControllerSubject(s)))
        {
            var key = ExtractControllerEntityRef(raw)
                      ?? (ContainsControllerSubject(s) ? ExtractBareIntegerWithContext(raw) : null);
            return new ParsedIntent(IntentKind.ExplainChainTrace, key);
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

    /// <summary>
    /// Sprint 12.7 PR #3 — Extract the first Controller-side entity reference
    /// (ASSET-N / JE-N / PO-N / INV-N / WO-N) from a raw utterance. Returns
    /// a string in the form "asset 4231" / "je 47" that
    /// ChainTraceService.ParseEntityRef will accept verbatim.
    /// </summary>
    public static string? ExtractControllerEntityRef(string raw)
    {
        var m = ControllerEntityPattern.Match(raw);
        if (!m.Success) return null;
        var kind = m.Groups[1].Value.Trim().ToLowerInvariant();
        var id = m.Groups[2].Value;
        // Normalize multi-word kinds ("journal entry", "purchase order", "work order")
        // to the single-word canonical that ChainTraceService.ParseEntityRef expects.
        var canonical = kind switch
        {
            var k when k.StartsWith("journal") => "je",
            var k when k.StartsWith("purchase") => "po",
            var k when k.StartsWith("work")     => "wo",
            _                                    => kind,
        };
        return $"{canonical} {id}";
    }

    /// <summary>
    /// Sprint 12.7 PR #3 — When the utterance contains chain-trace context
    /// (e.g. "asset", "nbv", "depreciation") but no explicit prefix, fall
    /// back to the first plain integer. ChainTraceService.ParseEntityRef
    /// defaults a bare integer to Asset, which matches the most common
    /// controller phrasing.
    /// </summary>
    private static string? ExtractBareIntegerWithContext(string raw)
    {
        var m = BareIntegerPattern.Match(raw);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>
    /// B7 Wave D PR-2 — extract the item reference (item Id or part number) from a
    /// make-or-buy utterance. Tries the "item/part X" grammar first, then a
    /// receipt-shaped natural key (PN-1234), then a bare integer.
    /// Public so the vector-fallback path can re-use it after vector routing.
    /// </summary>
    public static string? ExtractMakeBuyItemRef(string raw)
    {
        // Walk EVERY explicit "item/part X" match and return the first token that
        // looks like a real identifier (carries a digit or hyphen). Filler such as
        // "this part instead of …" produces an early match whose token ("instead")
        // is not an identifier — skip it and keep scanning, so
        // "buying this part instead of making item 9395" still resolves 9395.
        var explicitMatches = MakeBuyItemPattern.Matches(raw);
        if (explicitMatches.Count > 0)
        {
            foreach (Match em in explicitMatches)
            {
                var token = em.Groups[1].Value;
                if (LooksLikeItemId(token)) return token;
            }
            // Explicit "item/part" grammar was used but NONE of the captured tokens
            // is a real identifier ⇒ return null so the handler asks "which item?".
            // Do NOT fall through to the bare-integer scan, or "buying this part
            // instead of making 500 units" would wrongly grab "500" as the item id.
            return null;
        }
        // No explicit "item/part" grammar at all — try a receipt-shaped natural key
        // (PN-1234), then a bare integer ("why are we buying 9395").
        var nk = ExtractNaturalKey(raw);
        if (!string.IsNullOrEmpty(nk)) return nk;
        var bi = BareIntegerPattern.Match(raw);
        return bi.Success ? bi.Groups[1].Value : null;
    }

    // True when a captured make-or-buy token looks like a real item id / part
    // number (carries a digit or hyphen) rather than filler ("instead", "this").
    private static bool LooksLikeItemId(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (token.IndexOf('-') >= 0) return true;
        foreach (var ch in token) if (char.IsDigit(ch)) return true;
        return false;
    }

    // B7 Wave D PR-2 — true when the utterance is a make-or-buy "why" question.
    // Strong signal: explicit "make or buy" phrasing. Weak signal: a diagnostic
    // verb paired with a make/buy verb ("why are we buying this", "should we make
    // or build part X"). Guarded so plain "why is..." / "explain..." (no make/buy
    // verb) fall through to ExplainChainTrace / ExplainException.
    private static bool IsMakeBuyQuery(string s)
    {
        if (s.Contains("make or buy") || s.Contains("make-or-buy") ||
            s.Contains("make vs buy") || s.Contains("make versus buy") ||
            s.Contains("buy or make") || s.Contains("buy vs make") ||
            s.Contains("buy or build") || s.Contains("make or source"))
            return true;

        var hasVerb = s.Contains("why") || s.Contains("should we") ||
                      s.Contains("did we") || s.Contains("are we") ||
                      s.Contains("explain");
        var hasMakeBuy = s.Contains("buying") || s.Contains("making") ||
                         s.Contains("buy this") || s.Contains("make this") ||
                         s.Contains("buy it") || s.Contains("make it") ||
                         s.Contains("we buy") || s.Contains("we make") ||
                         s.Contains("to buy") || s.Contains("to make");
        return hasVerb && hasMakeBuy;
    }

    private static bool HasChainTraceVerb(string s) =>
        s.Contains("trace asset") || s.Contains("trace the asset") ||
        s.Contains("trace this asset") || s.Contains("trace je") ||
        s.Contains("trace journal") || s.Contains("trace the journal") ||
        s.Contains("drilldown on") || s.Contains("drill down on") ||
        s.Contains("drill down into") || s.Contains("drill into") ||
        s.Contains("chain trace") || s.Contains("source to gl") ||
        s.Contains("source-to-gl") || s.Contains("walk the chain") ||
        s.Contains("show the chain") || s.Contains("show me the chain") ||
        s.Contains("walk me through") && ContainsControllerSubject(s);

    private static bool HasDiagnosticVerb(string s) =>
        s.Contains("why is") || s.Contains("why's") || s.Contains("why was") ||
        s.Contains("where did") || s.Contains("explain") ||
        s.Contains("walk me through") || s.Contains("break down");

    private static bool ContainsControllerSubject(string s) =>
        s.Contains("nbv") || s.Contains("net book value") ||
        s.Contains("asset") || s.Contains("depreciation") ||
        s.Contains("accumulated dep") || s.Contains("capital project") ||
        s.Contains("journal entry") || s.Contains("journal-entry") ||
        s.Contains("je-") || s.Contains("je ") || s.Contains("je#") ||
        s.Contains("asset-") || s.Contains("asset#") ||
        s.Contains("cip ") || s.Contains("the cost") ||
        s.Contains("the gl") || s.Contains("the general ledger");
}
