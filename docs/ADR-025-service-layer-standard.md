# ADR-025 — Service Layer Standard + Control Plane Gate

**Status:** Accepted 2026-05-20
**Sprint:** Strategic Absorption Item 9 (added 2026-05-20 from Blueprint v4 review)
**Companion:** `MASTER_PLAN.md` Priority 1.61 · `docs/strategic-absorption-2026-05-20.md`
**Authors:** Dean (driver, locked the pain from a prior app) · Claude (audit + draft)

---

## Context

During the 2026-05-20 absorption of the Fortune-100 Strategy Blueprint v4 review, Dean recalled a prior app where handwritten SQL scattered across pages — with no central control plane — cost weeks of rewrite. He asked: *"are we making the same mistake?"*

Claude ran a real audit on the CherryAI codebase the same day:

| Audit dimension | Count | Verdict |
|---|---|---|
| Raw SQL (`FromSqlRaw` / `ExecuteSqlRaw` / `SqlQueryRaw`) | **4 files** | ✅ Contained. EF Core gives parameterization by default |
| Razor PageModels injecting `AppDbContext` directly | **106 files** | 🚨 Smoking gun |
| PageModels calling `.SaveChangesAsync()` directly | **57 files, 173 total calls** | 🚨 |
| Worst offenders (5+ writes) | `WorkOrders/Details` (17), `Purchasing/Details` (12), `Materials/ItemEdit` (11), `Admin/Requisitions` (10), `Maintenance/Technicians/Profile` (7), `Maintenance/Details` (7), `Assets/Asset` (5) | Top-10 refactor backlog |
| Services that exist | **73** | ✅ Service layer is real |
| Posting services (financial spine) | **6+** (`ApPostingService`, `ReceivingPostingService`, `CipCapitalizationService`, `CapitalImprovementPostingService`, `DepreciationService`, `PeriodGuard`) | ✅ Financial spine properly centralized |
| Uses of `IdempotencyMediator` (ADR-014 D4) | **11 files** | 🟡 Only new ADR-014 work uses it; 95 legacy pages don't |

**Verdict:** Same path-of-drift Dean experienced before, but less severe. The financial spine is properly serviced — journal entries flow through one place, period locks are enforced through one place. But 57 Pages writing directly to the database can bypass `PeriodGuard` / `AuditService` / `IdempotencyMediator` if a developer forgets to invoke them. The risk compounds every new sprint unless we stop the bleeding now.

---

## Decision

We adopt a **Service Layer Standard** for every operational mutation, enforced by a CI gate that catches NEW violations without forcing a 30-day stop-and-refactor.

### D1 — Service Layer Standard (the rule)

**Every operational mutation MUST flow through a domain Service.**

- New PageModels MUST inject the relevant `IFooService`, not `AppDbContext`
- PageModels MAY read directly via `AppDbContext` for thin display projections
- PageModels MAY NOT call `_db.SaveChangesAsync()` / `_context.SaveChangesAsync()` for operational mutations
- Read-only admin CRUD pages (e.g. lookup tables, simple admin lists with no business logic) may use `AppDbContext` directly when marked with the explicit allow-comment (see D4)

This applies to NEW PageModels. The 106 legacy PageModels are grandfathered until refactored under D5.

### D2 — Posting Service Interface Contract

Every posting service (one that creates `JournalEntry` rows or mutates inventory ledgers) MUST guarantee:

1. **Idempotency key** — accept an `idempotencyKey` parameter; reject duplicate replays
2. **Period guard** — call `PeriodGuard.EnsurePeriodOpen(...)` before posting
3. **Balanced JE validation** — debits == credits, asserted before commit
4. **Source document reference** — every `JournalLine` carries `SourceModule`, `SourceDocumentId`, `SourceDocumentNo`, `SourceLineId` (where applicable)
5. **Audit event** — `AuditService.LogAsync(...)` called with the flat-DTO pattern from `feedback_audit_log_serialization`
6. **Outbox event** — for downstream-relevant mutations, write to `OutboxEvent` in the same transaction

The interface contract:

```csharp
public interface IPostingService<TSourceDoc>
{
    Task<PostingResult> PostAsync(
        TSourceDoc source,
        string idempotencyKey,
        CancellationToken ct);
}

public sealed record PostingResult(
    Guid JournalEntryId,
    int LinesPosted,
    bool WasReplay,    // true if idempotency key matched an existing posting
    string? AuditEventId);
```

The 6+ existing posting services (`ApPostingService`, `ReceivingPostingService`, `CipCapitalizationService`, `CapitalImprovementPostingService`, `DepreciationService`, `PeriodGuard`) will adopt this interface as they're touched. New posting services (Sprint 13 Purchasing, Sprint 14 Maintenance work-order posting, etc.) MUST implement it from day one.

### D3 — Why no big-bang refactor

The blueprint's Phase 0 recommendation says *"30 days truth audit before any feature work."* We reject that for our context:

- 14 days to the June 3 EVS demo — we cannot stop shipping
- 106-page refactor is the exact trap that killed the prior app
- The CI gate (D4) stops the bleeding without freezing the team

Instead: **stop the bleeding + refactor the worst 10 over 5 sprints as side-work.**

### D4 — CI Control-Plane Gate

A GitHub Actions workflow runs on every PR. It greps the diff for newly-added files matching `Pages/**/*.cshtml.cs` and fails the build if any new file:

- Contains `AppDbContext` injection (`AppDbContext _db` / `AppDbContext _context`) AND
- Does NOT contain the explicit allow-comment `// PRAGMA: control-plane-exempt`

**Failure message:** clear, actionable, with remediation hint pointing to this ADR.

**Allow-comment usage:** the rare legitimate admin CRUD page (lookup tables, simple admin lists, etc.) can opt out by placing `// PRAGMA: control-plane-exempt` near the top of the file. Code review polices the use of the pragma.

Modified pages (legacy ones that already inject `AppDbContext`) are NOT flagged — they're grandfathered. The gate's job is to stop drift from getting worse, not to flag pre-existing patterns.

### D5 — Top-10 Refactor Backlog

The 10 highest-risk PageModels (5+ direct `SaveChangesAsync` calls) get refactored to call their domain `IFooService` over the next ~5 sprints, at ~2 per sprint as side-work:

| Rank | PageModel | Writes | Target service |
|---|---|---|---|
| 1 | `Pages/WorkOrders/Details.cshtml.cs` | 17 | `IWorkOrderService` (extend `MaintenanceService` or split out) |
| 2 | `Pages/Purchasing/Details.cshtml.cs` | 12 | `IPurchaseOrderService` (new) |
| 3 | `Pages/Materials/ItemEdit.cshtml.cs` | 11 | extend existing `MaterialMasterService` |
| 4 | `Pages/Admin/Requisitions.cshtml.cs` | 10 | `IRequisitionService` (new) |
| 5 | `Pages/Maintenance/Technicians/Profile.cshtml.cs` | 7 | `ITechnicianService` (new) |
| 6 | `Pages/Maintenance/Details.cshtml.cs` | 7 | extend `MaintenanceService` |
| 7 | `Pages/Assets/Asset.cshtml.cs` | 5 | extend existing asset service |
| 8 | `Pages/AccountsPayable/Details.cshtml.cs` | 4 | extend `ApPostingService` |
| 9 | `Pages/Admin/Webhooks/Index.cshtml.cs` | 4 | `IWebhookAdminService` (new) |
| 10 | `Pages/CIP/CostDetails.cshtml.cs` | 2 | extend `CipCostService` |

Sprint 14 Maintenance is when items 1, 5, 6 land naturally. Sprint 13 Purchasing handles item 2. The rest fold into Sprint 12.5 cleanup or whichever Control Center cockpit touches them.

---

## Consequences

### Positive

- **Stops drift today.** No new PageModel can bypass the service layer without explicit acknowledgement (the PRAGMA)
- **Doesn't block feature work.** Sprint 12C / 12D / EVS June 3 deadline unaffected
- **Compounds with Item 2 (Tool Registry).** Both establish "every mutation goes through one of a small number of standard interfaces" — AI agents and PageModels go through the same gate
- **Compounds with Item 8 (RLS automated tests).** Service layer is the C# control plane; RLS is the DB control plane; together they enforce tenant safety end-to-end
- **Enables Item 5 (Recommendation outcome tracking).** Outcomes can only be measured if every mutation flows through the same hook
- **Recoverable.** A misfiring CI gate can be relaxed (add a PRAGMA, adjust the grep pattern) without rolling back the standard

### Negative

- **Mild friction for new PageModels.** Developers must inject a Service even for simple forms — but this is the point
- **Top-10 refactor takes 5 sprints.** Long tail. Mitigated by ordering most-painful-first (`WorkOrders/Details` at 17 writes is also the page Joe/EVS will see)
- **PRAGMA is escape hatch.** If abused, defeats the purpose. Code review enforces sparing use

### Risks

- **False positives on the CI gate.** If a legitimate admin CRUD page is added without the PRAGMA, the gate fails the PR. Resolution: add the PRAGMA + note in PR review
- **Pages might "split" responsibilities.** A PageModel might inject both `IFooService` (for the write path) AND `AppDbContext` (for the read projection) — this is acceptable per D1; the gate only fires on direct `SaveChangesAsync` calls

---

## Implementation in this PR

1. This ADR document — `docs/ADR-025-service-layer-standard.md`
2. `.github/workflows/control-plane.yml` — CI workflow that runs on PR
3. `scripts/check-control-plane.sh` — the grep script invoked by the workflow

**Not in this PR (deferred to first refactor PR):**

- `IPostingService<TSourceDoc>` interface definition (lands in the WorkOrders/Details → IWorkOrderService refactor as part of Sprint 14)
- Migration of existing posting services to implement the interface (incremental, as touched)

---

## Cross-references

- `docs/strategic-absorption-2026-05-20.md` — Item 9 full detail with audit data
- `MASTER_PLAN.md` Priority 1.61 — sprint-level absorption summary
- ADR-014 — Voice + Auth + Idempotency (D4: IdempotencyMediator, the existing primitive this standard formalizes)
- ADR-018 — Cockpit-First Pattern (cockpits inject services, not DbContext — already conforming)
- ADR-020 — Postgres-as-AI-Native-OS (the data layer this standard sits on top of)
- ADR-024 (planned) — Tool Registry & Recommendation Lifecycle (Item 2 — AI agents go through the same gate)
- Memory: `project_strategic_absorption_2026_05_20.md`
- `feedback_audit_log_serialization` — flat-DTO pattern referenced by D2.5

---

## Decision log

- **2026-05-20:** Dean asked the prior-app pain question. Claude audited. Item 9 added to strategic absorption. ADR drafted same day.
- **2026-05-20:** Accepted by Dean. Shipped as docs+CI-only PR.
