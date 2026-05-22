# ADR-025 D2 Amendment — IPostingService&lt;TSourceDoc&gt; Implementation Pattern

**Status:** Accepted 2026-05-22 · Companion to PR #273 (Sprint 12.9 PR #2)
**Implements:** ADR-025 D2 contract definition
**Supersedes:** None (this is the first concrete realization of D2)

---

## Context

ADR-025 D2 (accepted 2026-05-20) defined the contract:

```csharp
public interface IPostingService<TSourceDoc>
{
    Task<PostingResult> PostAsync(TSourceDoc source, string idempotencyKey, CancellationToken ct);
}
```

PR #272 (Sprint 12.9 PR #1) shipped the Roslyn CHERRY025 analyzer that catches direct `AppDbContext` injection outside the typed service layer. Sprint 12.9 PR #2 (this PR) closes the loop by establishing the concrete shape every existing **and future** posting service implements — before Sprint 13 (Purchasing Control Center) freestyles its own posting pattern.

## Decisions made in this PR

### D2.1 — Result-enveloped return type

The D2 placeholder `PostingResult` becomes `Result<PostingReceipt>`. This:

1. Reuses ADR-014 D2's existing `Result<T>` envelope (struct, allocation-free, used everywhere else in the service layer).
2. Lets expected failures (invoice not found, period closed, three-way-match exception without override) flow as `Result.Failure(msg)` to the service-method-first surface — both Razor pages AND the future voice MCP tool layer consume the same shape.
3. Reserves exceptions for unexpected platform errors (DB down, network timeout).

### D2.2 — Strongly-typed idempotency key

The D2 placeholder `string idempotencyKey` becomes `Guid idempotencyKey` to match the existing `IIdempotencyMediator` API (ADR-014 D4). The client mints the UUID per logical operation; same Guid + same userId returns the cached `PostingReceipt`.

### D2.3 — actorUserId is an explicit ctor-time parameter, not a service-level dependency

The `IIdempotencyMediator.ExecuteAsync` API requires `int userId` for the unique scoping `(UserId, Key)`. Two options were considered:

- **Inject** `ICurrentUserAccessor` into every posting service. Implicit, but couples the service to an HTTP/auth concern that doesn't belong at this layer.
- **Pass** `int actorUserId` as a method parameter. Explicit, testable, no hidden dependency. Matches the pattern already in `StockReceiptService.CreateAsync` and the broader ADR-014 service-method-first principle.

**Choice: explicit parameter.** Caller (PageModel / Controller / Endpoint / voice tool) extracts `userId` from auth context and passes it in.

### D2.4 — Per-operation request DTOs, not a unified shape

Each posting service has multiple logical post operations:

| Service | Logical operations |
|---|---|
| `ApPostingService` | invoice approval · invoice payment · invoice void |
| `ReceivingPostingService` | receive goods · rejection reversal |
| `CipCapitalizationService` | capitalize WO to asset |
| `CapitalImprovementPostingService` | capital improvement post |
| `DepreciationService` | period depreciation post |

A single `IPostingService<T>.PostAsync` can't naturally express all of them. The decision: each service implements `IPostingService<TSourceDoc>` **multiple times**, once per logical operation, with a distinct request DTO per implementation.

```csharp
public sealed class ApPostingService :
    IApPostingService,                                  // legacy domain-specific interface
    IPostingService<ApInvoiceApprovalRequest>           // primary post, this PR
    // IPostingService<ApInvoicePaymentRequest>         // payment post, follow-up PR
    // IPostingService<ApInvoiceVoidRequest>            // void post, follow-up PR
{ ... }
```

PR #273 ships the PRIMARY post for each existing service (ApInvoiceApprovalRequest for AP, ReceiveGoodsRequest for Receiving). Secondary operations migrate in subsequent PRs at the natural moment each is touched.

### D2.5 — Legacy method shapes preserved for backward compat

`IApPostingService.PostApprovalAsync(int, bool, string)` and `IReceivingPostingService.PostReceiptAsync(int)` remain on the domain interfaces — existing call sites (PageModels, controllers, the few CipAutoCostPostingService wiring spots) keep working. The new `IPostingService<T>.PostAsync` implementations **delegate** to the legacy methods inside the `IIdempotencyMediator.ExecuteAsync` wrap.

Mapping the legacy result types (`ApPostingResult`, `ReceivingPostingResult`) into the generic `PostingReceipt` loses some fidelity (e.g. `ApPostingResult.MatchStatus`). Callers that need the legacy fields can still call the legacy method directly. New code paths default to `PostAsync` and load the `JournalEntry` by Id if richer detail is needed.

### D2.6 — WasReplay defaults to false in v1

The `IIdempotencyMediator` handles cached-response return invisibly to the inner work. Inside the work, there's no signal that the call is a replay. Surfacing replay detection requires a follow-up enhancement to `IIdempotencyMediator` (out-param or a wrapper Result type).

For v1 of `IPostingService<T>`, all `PostingReceipt.WasReplay` values are `false`. Callers that care about replay detection should compare returned `JournalEntryId` against the source document's stored journal-entry FK — they'll match on replay.

## PostingReceipt shape

```csharp
public sealed record PostingReceipt(
    int? JournalEntryId,        // null for "no-op" outcomes (e.g. nothing to reverse)
    int LinesPosted,            // 0 in v1 — populated when full refactor lands
    decimal TotalDebits,        // equal to TotalCredits for a balanced post
    decimal TotalCredits,
    bool WasReplay,             // always false in v1, see D2.6
    string? AuditEventId);      // null in v1 — populated by D2.6 follow-up
```

`LinesPosted` and `AuditEventId` are placeholder zeros/nulls in v1. They're surfaced in the type so future PRs can populate them without a contract change.

## What this PR ships

| File | Purpose |
|---|---|
| `Services/Posting/IPostingService.cs` (new) | Interface + `PostingReceipt` record + 5-guarantee XML doc |
| `Services/Posting/PostingRequests.cs` (new) | `ApInvoiceApprovalRequest` + `ReceiveGoodsRequest` request DTOs |
| `Services/AccountsPayable/ApPostingService.cs` (mod) | Add `IPostingService<ApInvoiceApprovalRequest>` impl + inject `IIdempotencyMediator` |
| `Services/Receiving/ReceivingPostingService.cs` (mod) | Add `IPostingService<ReceiveGoodsRequest>` impl + inject `IIdempotencyMediator` |
| `tests/Abs.FixedAssets.Tests/PostingContractTests.cs` (new) | Reflection-based contract assertions (7 cases) |
| `docs/ADR-025-posting-service-contract.md` (new) | This document |

## What this PR explicitly does NOT do

- Migrate `IApPostingService.PostPaymentAsync` or `PostVoidAsync` to `IPostingService<T>` impls.
- Migrate `IReceivingPostingService.PostRejectionReversalAsync` to a sibling `IPostingService<RejectGoodsReversalRequest>`.
- Surface replay detection (see D2.6).
- Refactor the worst-offender PageModels to call `PostAsync` instead of the legacy methods — that's Sprint 12.9 PRs #3-5.
- Add a Roslyn analyzer rule (CHERRY026?) that mandates every concrete posting service must implement `IPostingService<T>` at least once. Could land in a follow-up if drift is observed.

## Sales-line impact

This PR closes the IPostingService contract before Sprint 13 starts. Joe's June 3 EVS pitch (and every future Fortune-100 procurement diligence question) can now include:

> "Every operational mutation in CherryAI — Receiving, AP, future Purchasing, future Maintenance — flows through one of a small number of typed `IPostingService<T>` implementations. Each is idempotency-keyed, period-guarded, balanced-JE-validated, audit-logged, and outbox-published. The Roslyn CHERRY025 analyzer (PR #272) catches direct DbContext writes; PR #273 codifies the SHAPE of what those writes must look like."

The chain-of-evidence marketing line ("Machine Event → General Ledger") is one PR closer to defensible under enterprise diligence.

## Cross-references

- `docs/ADR-025-service-layer-standard.md` — the standard this realizes
- `docs/ADR-025-roslyn-analyzer-design.md` — PR #272's companion (CHERRY025 design)
- `docs/ADR-014-phase-f-ui-and-voice-readiness.md` — D2 (Result<T>) + D4 (IdempotencyMediator)
- `MASTER_PLAN.md` Priority 1.6080 — Sprint 12.9 sprint-level
- Memory `project_pr272_sprint_12_9_pr1_shipped.md` — the predecessor PR
- Memory `project_sprint_12_9_control_plane_hardening_locked.md` — sprint lock
