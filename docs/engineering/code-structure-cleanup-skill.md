# Code Structure Cleanup — Discipline Pass After Every Feature

**Adopted:** 2026-05-24
**Status:** Standing engineering discipline · Sister-doc to ADR-025 (Service Layer Standard)
**Trigger phrases:** *"cleanup pass on X"*, *"refactor pass on X"*, *"extract services from X"*
**Source:** Adapted from Pawel Cell's agentic-engineering skills (Micky Podcast / David Ondrej / Michael Shimeles interviews)

---

## Why this exists

AI-built features ship working but often leave behind:

- Duplicated runtime mechanics (same `await _db.Foo.Where(...).ToListAsync()` shape in 4 PageModels).
- Repeated API calls (3 services each calling `EmailService` slightly differently).
- Repeated validation / parsing / transformation logic.
- Code that the **next** agent struggles to navigate.

The pattern works. The structure doesn't.

This is the dedicated pass that turns *working code* into *durable code*. Run it **after** the feature works, **never** before.

## How it composes with what we already have

| Layer | What it does | Where it lives |
|---|---|---|
| **ADR-025** (Service Layer Standard) | Decides WHAT should live in services vs PageModels | `docs/ADR-025-service-layer-standard.md` |
| **CHERRY025** analyzer | Enforces ADR-025 at compile time (catches AppDbContext in PageModels) | `Analyzers/Abs.FixedAssets.ControlPlaneAnalyzer/` |
| **This skill** | The explicit trigger to extract reusable mechanics after a feature ships | THIS doc |

ADR-025 + CHERRY025 prevent the worst leaks at compile time. This skill is the proactive cleanup that catches the soft duplication CHERRY025 can't see (e.g., two services that each implement their own `BuildAuditPayload(...)` shape).

## When to use

✅ **Use this pass:**
- After a feature works and tests pass.
- After a PRA-X / service-layer ship lands.
- When a code review flags "this looks repeated."
- Before kicking off Sprint 14+ Control Centers that compose multiple masters.

❌ **Don't use this pass:**
- During schema-only ships (PRA-4 through PRA-11 are entity + migration + DbContext only — there are no services yet to consolidate).
- As permission to redesign the app.
- As an excuse to rename everything (naming churn hurts review).
- Mixed with new-feature work (cleanup is a separate, scoped pass).

## What "service layer" means here

A service layer is a place for reusable **mechanics**:

- Sending an email.
- Streaming an AI response (e.g., voice-narration of an exception).
- Validating a webhook signature.
- Calling an external API (Voyage embeddings, Apollo enrich).
- Transforming a payload (camelCase ↔ PascalCase, JSONB shape extraction).
- Parsing / normalizing data (UOM conversions, address formatting).

The PageModel / route / action decides **what** should happen. The service handles **how** it happens.

> **Domain policy stays in the caller.** Service does mechanics. Don't pull "is this user allowed to do X" into the service — that's the PageModel's job.

## Cleanup-pass prompt template

Use this verbatim when triggering a cleanup pass:

```md
The <feature/PRA-X> ship is working. Run a code-structure cleanup pass.

Goal:
- Find duplicated runtime mechanics, repeated API calls, repeated parsing,
  repeated validation, or repeated business logic across the touched files.
- Move repeated mechanics into reusable service-layer functions/modules.
- Keep domain policy in the calling PageModel / endpoint / component.
- Do NOT change user-facing behavior.
- Keep the diff small.

Process:
1. Inspect the files touched by <feature>.
2. Identify repeated logic and name the duplication clearly.
3. Propose the smallest service-layer extraction.
4. Implement it.
5. Run `dotnet build` + relevant Playwright specs.
6. Summarize exactly what got simpler (delta in LOC / call sites / tests).
```

## Good outcome (real example we'll hit on PRA-7 + PRA-8 + PRA-10)

**Before cleanup pass:**
- `ProductionPostingService` resolves Posting GL by walking `PostingProfiles` → `ItemGroup` → `WarehouseMaster` defaults, with a 25-line resolver inline.
- `LaborPostingService` resolves Labor GL by walking the same chain, copy-pasted.
- `TaxPostingService` resolves Tax GL by walking `TaxRateMasters` → `TaxCodeMaster` → `TaxAuthority`, with another 25-line resolver inline.

**After cleanup pass:**
- Single `IGlAccountResolver` service with one well-tested method.
- `ProductionPostingService` + `LaborPostingService` + `TaxPostingService` each ~5 lines, calling the resolver.
- The resolver has a unit test suite covering the cascade order (PRA-7 ADR-019 documented).

## Pre-flight checks before kicking off cleanup

- [ ] **Is this PR too large for cleanup?** If so, split first.
- [ ] **Are there tests covering the current behavior?** If no, write a quick regression test BEFORE the refactor — without it, "behavior unchanged" is a guess.
- [ ] **Is there a clear duplication?** If you can't name it in one sentence, there isn't one yet.
- [ ] **Is anyone else touching the same files?** Coordinate or wait.

## Common pitfalls (we've already hit some)

| Pitfall | What it looks like | Mitigation |
|---|---|---|
| Refactoring the whole app | Cleanup PR ends up 1000+ LOC | Scope tied to one feature area only |
| Renaming everything | Diff is mostly rename noise | Keep names stable; cleanup is about structure, not branding |
| Mixing cleanup with new feature | "While I'm in there, let me also add X" | Two separate PRs, two separate stop conditions |
| Pretty code, same logic | Formatting-only changes | Service extraction is the goal; pretty is a side effect |
| Domain policy in the service | "Service is now deciding who's authorized" | Service does mechanics only; policy stays in the caller |

## Verification checklist (post-cleanup)

- [ ] User-facing behavior stayed the same (manual smoke + Playwright spec).
- [ ] Repeated mechanics actually reduced (count call sites before/after).
- [ ] Calling files became simpler (LOC down in callers).
- [ ] Relevant tests / typechecks ran clean.
- [ ] CHERRY025 analyzer is still green (no new AppDbContext-in-PageModel leaks).
- [ ] Diff stayed focused on the feature area (no rename churn, no unrelated services touched).

## When this skill will start paying off in our roadmap

| Sprint / Ship | Cleanup opportunity |
|---|---|
| **PRA-5b** (COA segment refactor) | After AccountingKey table lands, multiple JE-posting services will need to migrate from `AccountId` → `AccountingKeyId`. Likely 3-5 services with the same shape — perfect cleanup candidate. |
| **Sprint 13.5 PR #5e+ (MES events)** | DowntimeEvent + ScrapEvent + ReworkEvent + MaterialConsumption services will share an event-emission pattern. Extract a `IProductionEventEmitter` after the 4 services land. |
| **Sprint 13** (Full Purchasing CC) | When Receipt + Inspection + Auto-Issue services light up (Theme B3), they'll share a "warehouse-routing" pattern that maps to PRA-7's WarehouseMaster. Extract `IWarehouseRouter`. |
| **Sprint 14+** (Maintenance / Inventory / Shipping CCs) | These all compose PRA-7 PostingProfile + PRA-8 Department GL + PRA-10 TaxRate. Likely 3+ GL-resolution paths that should converge into `IGlAccountResolver`. |
| **Sprint 19+ (SalesOrder per ADR-027)** | OrderReleaseService.CreateProductionOrderForLineAsync will compose PriceListLine + DiscountSchema + RebateAgreement resolvers. Build the resolvers as services from day 1, then run a cleanup pass after the first 2-3 callers exist. |

## How to invoke

Either:

1. **Direct request:** Dean says *"cleanup pass on [feature]"* → Claude runs the pass.
2. **Implicit trigger:** After every PRA-X / feature ship that includes service code, Claude proactively asks: *"This shipped clean. Want me to run the structure cleanup pass now or defer?"*

The default for the rest of the Master Files Baseline cascade (schema-only) is **defer** — there are no services to clean. The trigger fires starting **PRA-5b** when services begin to accumulate.

## Cross-references

- ADR-025 — Service Layer Standard (the WHAT this skill enforces)
- `Analyzers/Abs.FixedAssets.ControlPlaneAnalyzer/` — CHERRY025 (the compile-time guardrail)
- Memory: `feedback_code_structure_cleanup_after_features.md` (the trigger note for future Claude sessions)
- Source: https://github.com/pawel-cell/micky-podcast-agentic-engineering/blob/main/skills/code-structure-cleanup/SKILL.md
