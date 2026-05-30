using System.Collections.Generic;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 — Hybrid Intent Router prototype seeds (PR #3).
//
// One source of truth for the intent prototype utterances that get
// embedded into the Embeddings table (EntityType="Intent",
// EntityId=(long)IntentKind, TenantId=0 — system-wide).
//
// The hybrid router falls through to vector cosine search when the
// keyword classifier returns Unknown. These prototypes are the index
// it searches against.
//
// DESIGN NOTES
// ============
// - Each prototype is a natural-language utterance a user might say.
// - Avoid keyword-clichés that the Phase 1 classifier already catches
//   (e.g. "find receipt X"). The prototypes earn their keep on the
//   PHRASINGS that the keyword path misses ("anything new at the dock",
//   "what's queued for receiving", "give me a status on RCPT-123").
// - Keep prototype count tight (~3 per IntentKind). Voyage charges per
//   token. 12 prototypes × ~10 tokens × $0.06/M = $0.0000072 to seed.
//   Adding language packs in Sprint 22 (fr-CA + es) is just appending.
// - Source text format is identical to the user's utterance (no
//   prefixing or templating). Asymmetric query/document embeddings
//   work because Voyage knows the input_type — we embed prototypes as
//   `document`, voice queries as `query`.

public sealed record IntentPrototype(IntentKind Kind, string Utterance);

public static class IntentPrototypes
{
    /// <summary>Pseudo TenantId used for system-wide intent prototypes.</summary>
    public const int SystemTenantId = 0;

    /// <summary>Tag used in the Embeddings.EntityType column.</summary>
    public const string EntityType = "Intent";

    public static readonly IReadOnlyList<IntentPrototype> All = new[]
    {
        // ExpectedArrivals — phrasings the keyword router misses or weakly catches.
        new IntentPrototype(IntentKind.ExpectedArrivals, "anything new at the dock today"),
        new IntentPrototype(IntentKind.ExpectedArrivals, "what's queued for receiving this morning"),
        new IntentPrototype(IntentKind.ExpectedArrivals, "give me the inbound list for the next shift"),

        // LookupReceipt — natural-key phrasings without "find" / "show me".
        new IntentPrototype(IntentKind.LookupReceipt, "any update on receipt RCPT-2026-1234"),
        new IntentPrototype(IntentKind.LookupReceipt, "status on heat number H-12345"),
        new IntentPrototype(IntentKind.LookupReceipt, "purchase order PO-555 details please"),

        // ExplainException — paraphrases of "why is X blocked".
        new IntentPrototype(IntentKind.ExplainException, "is something holding up this receipt"),
        new IntentPrototype(IntentKind.ExplainException, "tell me what flagged this shipment"),
        new IntentPrototype(IntentKind.ExplainException, "give me the reason this receipt is on hold"),

        // Help — meta utterances.
        new IntentPrototype(IntentKind.Help, "what commands does voice support"),
        new IntentPrototype(IntentKind.Help, "how do I use this voice feature"),
        new IntentPrototype(IntentKind.Help, "what should I say to you"),

        // ExplainChainOfCustody — Sprint 12D PR #5 / ADR-022 §D4. Phrasings
        // the keyword layer might miss; vector layer catches paraphrases.
        new IntentPrototype(IntentKind.ExplainChainOfCustody, "show me the chain of custody for this receipt"),
        new IntentPrototype(IntentKind.ExplainChainOfCustody, "where did this material come from"),
        new IntentPrototype(IntentKind.ExplainChainOfCustody, "trace this receipt back to its source"),
        new IntentPrototype(IntentKind.ExplainChainOfCustody, "who supplied this item"),
        new IntentPrototype(IntentKind.ExplainChainOfCustody, "walk me through the audit trail"),

        // ExplainChainTrace — Sprint 12.7 PR #3. Controller-side source-to-GL
        // chain walks. CFO motion: "why is NBV on asset 4231" or "drill down
        // on JE 47". The vector prototypes cover paraphrasings the keyword
        // layer might miss (e.g. business-fluent phrasings without the
        // verbs "trace" / "drill").
        new IntentPrototype(IntentKind.ExplainChainTrace, "why is the net book value so high on asset 4231"),
        new IntentPrototype(IntentKind.ExplainChainTrace, "break down how asset 1116 ended up on the books"),
        new IntentPrototype(IntentKind.ExplainChainTrace, "trace the depreciation expense for this machine"),
        new IntentPrototype(IntentKind.ExplainChainTrace, "what fed journal entry 47 in the general ledger"),
        new IntentPrototype(IntentKind.ExplainChainTrace, "where did this asset's cost come from"),
        new IntentPrototype(IntentKind.ExplainChainTrace, "walk me through the source to general ledger chain"),

        // ExplainMakeBuyDecision — B7 Wave D PR-2. Make-or-buy decision narration.
        // "why are we buying item 9395" / "why did we make part PN-1234". Vector
        // prototypes cover business-fluent paraphrasings the keyword layer misses.
        new IntentPrototype(IntentKind.ExplainMakeBuyDecision, "why are we buying this part instead of making it"),
        new IntentPrototype(IntentKind.ExplainMakeBuyDecision, "explain the make or buy decision for item 9395"),
        new IntentPrototype(IntentKind.ExplainMakeBuyDecision, "should we make or buy this bracket"),
        new IntentPrototype(IntentKind.ExplainMakeBuyDecision, "why did we decide to outsource this component"),
        new IntentPrototype(IntentKind.ExplainMakeBuyDecision, "what drove the make versus buy call on this part"),

        // CrystallizeJobToStandard — B7 Wave D PR-3 (CLOSES B7). Harvest a reusable
        // standard from a completed ETO job. Vector prototypes cover business-fluent
        // paraphrasings of "crystallize" the keyword layer might miss.
        new IntentPrototype(IntentKind.CrystallizeJobToStandard, "crystallize this job into a standard"),
        new IntentPrototype(IntentKind.CrystallizeJobToStandard, "harvest a reusable standard from order PRO-2026-00042"),
        new IntentPrototype(IntentKind.CrystallizeJobToStandard, "promote this finished job to a standard item"),
        new IntentPrototype(IntentKind.CrystallizeJobToStandard, "turn this one-off build into a reusable standard"),
        new IntentPrototype(IntentKind.CrystallizeJobToStandard, "save this job as a standard master"),

        // ProjectPromiseStatus — B9 Wave 1 PR-2. "Can we still hit the customer
        // promise?" verdict. Vector prototypes cover business-fluent paraphrasings.
        new IntentPrototype(IntentKind.ProjectPromiseStatus, "can we still hit the promise on project PRJ-001"),
        new IntentPrototype(IntentKind.ProjectPromiseStatus, "are we on track to deliver this project on time"),
        new IntentPrototype(IntentKind.ProjectPromiseStatus, "will we make the customer deadline on this program"),
        new IntentPrototype(IntentKind.ProjectPromiseStatus, "is the project going to ship on time"),
        new IntentPrototype(IntentKind.ProjectPromiseStatus, "what is the delivery risk on project DEMO-COO-PROJ-001"),

        // ShowProjectGraph — B9 Wave 1 PR-3 (CLOSES B9 Wave 1). Open the project
        // lifecycle graph. Vector prototypes cover paraphrasings of "show the graph".
        new IntentPrototype(IntentKind.ShowProjectGraph, "show me the project graph for PRJ-001"),
        new IntentPrototype(IntentKind.ShowProjectGraph, "graph project DEMO-COO-PROJ-001"),
        new IntentPrototype(IntentKind.ShowProjectGraph, "show the lifecycle for this project"),
        new IntentPrototype(IntentKind.ShowProjectGraph, "pull up the quote to cash graph for this program"),
        new IntentPrototype(IntentKind.ShowProjectGraph, "visualize the whole project pipeline end to end"),
    };
}
