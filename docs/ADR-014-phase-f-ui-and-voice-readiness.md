# ADR-014 — Phase F UI Architecture + Voice-AI Readiness

**Status:** Proposed — awaiting Dean sign-off
**Date:** 2026-05-17
**Author:** Architecture
**Supersedes:** N/A
**Builds on:** ADR-012 (Unified WorkOrder + classification satellites), ADR-013 (ProductionOrder + polymorphic vision)
**Foreshadows:** Sprint 5 — Voice-First AI Co-Pilot (signature feature)

---

## Question

Sprint 3 / Phase E shipped 18 production schema tables across three polymorphic primitives (WorkOrder, ProductionBatch, MaterialStructure). Phase F (Sprint 4) now builds the UI — Razor pages, edit screens, list views, detail renderers.

**The constraint:** Sprint 5 will overlay a voice-first AI co-pilot that knows what screen you're on and can take real actions on your behalf (Dean's signature vision, captured in `memory/project_voice_ai_copilot_vision.md`). Voice will not be built for several months. **But the Razor pages we ship now must be structured so voice slots in cleanly — without a refactor.**

Two questions:

1. What's the **structural pattern** for every Razor page in Phase F so the voice layer can be added without rework?
2. What **data-model additions** must land in Phase F to support the voice-driven workflows we've already specified (the McMaster Carr PO example from 2026-05-17)?

---

## State of practice (research-validated)

Research pass on 2026-05-17 surveyed Microsoft Learn (Razor Pages, Resource-based Authorization, View Components, Tag Helpers, Distributed Caching, Audit logs for Copilot), Microsoft Purview CopilotInteraction schema, GitHub Copilot Enterprise audit fields, Stripe idempotency-keys reference, brandur.org idempotency pattern, Milan Jovanović + Anton DevTips Result-pattern guidance, Andrew Lock resource-based authorization, Telerik View-Components-vs-partials decision guide. Findings condensed below.

**Key validated patterns:**

- **`VoiceReadyPageModel` base class with virtual `BuildContextPayload()`** is the cleanest, lowest-dependency pattern for per-page AI context exposure. PageModel already has `HttpContext`, `User`, `RouteData`; the base class assembles common parts and the page overrides to add entity-specific context.
- **Plain `IXxxService` interfaces returning `Result<T>` with DTO-in/DTO-out** is the dominant .NET 9 idiom. MediatR adds reflection overhead, a license question, and indirection without value when plain interfaces suffice.
- **AuditLog extension columns mirror Microsoft Purview CopilotInteraction schema** (RecordType 65–69) and GitHub Copilot Enterprise audit fields. The human is always the principal `Actor`; AI is metadata.
- **Stripe-pattern idempotency keys, stored in Postgres** in the same database as the data being mutated, are the only durable answer for non-HTTP voice→service calls. Same-DB ACID is required; Redis fails the recovery property.
- **Resource-based `IAuthorizationService.AuthorizeAsync(user, resource, policy)`** is the choke point. AI executes as the invoking user's `ClaimsPrincipal` — never as its own service account.
- **View Components per subtype**, dispatched by discriminator inside one parent Razor page, is the maintainable polymorphic-detail-page pattern. Partials can't have their own DI; Tag Helpers are wrong scope; one mega-page with `@if` breaks past 4 variants.

---

## Decisions

### D1 — `VoiceReadyPageModel` base class

Every Phase F Razor page model inherits from a new `VoiceReadyPageModel : PageModel`. The base exposes a virtual `BuildContextPayload()` that returns a `VoiceContextPayload` DTO. Pages override to add their entity-specific fields.

```csharp
public abstract class VoiceReadyPageModel : PageModel
{
    public virtual VoiceContextPayload BuildContextPayload() => new()
    {
        Route        = HttpContext.Request.Path,
        UserId       = User.FindFirstValue(ClaimTypes.NameIdentifier),
        Roles        = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray(),
        TenantId     = User.FindFirstValue("tenant_id"),
        EntityType   = null,        // pages override
        EntityId     = null,        // pages override
        RelatedIds   = Array.Empty<string>(),
        FocusedField = HttpContext.Request.Query["focus"].ToString()
    };
}
```

A single `IAsyncPageFilter` calls `BuildContextPayload()` post-handler and writes it into `HttpContext.Items["voice.ctx"]`. A future `/_voice/context` endpoint reads from there.

**Migration note:** existing pages don't have to convert immediately. New Phase F pages inherit from `VoiceReadyPageModel`; legacy pages can be converted opportunistically when touched.

### D2 — Service-method-first action surface

Every mutation in Phase F lives in an `IXxxService` interface. Page `OnPost*` handlers are 3 lines:

```csharp
public async Task<IActionResult> OnPostCloseAsync(CloseWorkOrderCommand cmd)
{
    var r = await _woSvc.CloseAsync(cmd, User, HttpContext.RequestAborted);
    if (r.IsFailure) { ModelState.AddModelError("", r.Error); return Page(); }
    return RedirectToPage();
}
```

Service signature:

```csharp
public interface IWorkOrderService
{
    Task<Result<WorkOrderResult>> CloseAsync(
        CloseWorkOrderCommand cmd,
        ClaimsPrincipal user,
        CancellationToken ct);
}
```

**Rules:**

- DTO-in (`CloseWorkOrderCommand`), DTO-out (`WorkOrderResult`). Never positional primitives. DTOs become the JSON schema for the future MCP tool definition for free.
- `Result<T>` for expected failures (validation, business rule, permission). Exceptions only for unexpected (DB down).
- Pass `ClaimsPrincipal` and `CancellationToken` explicitly — don't inject `IHttpContextAccessor`. The MCP server calls the same method with a different principal.

### D3 — AuditLog extension for AI-on-behalf-of actions

Existing `AuditLog` table gets seven new nullable columns (NULL for direct user actions, populated for AI-mediated):

| Column | Type | Purpose |
|---|---|---|
| `ActorKind` | smallint enum (User=0, AiOnBehalfOf=1, System=2) | Distinguishes direct vs AI-mediated |
| `OnBehalfOfUserId` | uuid nullable | Human when ActorKind=AiOnBehalfOf |
| `AiSessionId` | uuid nullable | Multi-turn conversation correlation |
| `AiCommandText` | text nullable | Raw natural-language utterance |
| `AiModelVersion` | varchar(64) nullable | "claude-opus-4-7" |
| `AiToolName` | varchar(128) nullable | Service method or MCP tool called |
| `AiConfidence` | numeric(4,3) nullable | Model-supplied if available |

`Actor`/`Action`/`BeforeJson`/`AfterJson` remain unchanged. **The human is always the principal Actor**, matching how Microsoft Purview and GitHub Copilot Enterprise model AI actions for regulatory defensibility.

Apply existing `AuditService.LogAsync` rule (`feedback_audit_log_serialization.md`) — pass flat DTOs, not live EF entities.

### D4 — Idempotency keys

Stripe pattern, in Postgres, same database as the data being mutated. Required for the voice layer; useful for the UI too (double-click protection).

```sql
CREATE TABLE idempotency_keys (
    user_id           uuid        NOT NULL,
    key               uuid        NOT NULL,
    request_hash      bytea       NOT NULL,
    response_status   int         NULL,
    response_body     jsonb       NULL,
    locked_at         timestamptz NULL,
    completed_at      timestamptz NULL,
    expires_at        timestamptz NOT NULL DEFAULT now() + INTERVAL '24 hours',
    PRIMARY KEY (user_id, key)
);
```

`IdempotencyMediator` wraps service calls. `INSERT ... ON CONFLICT DO NOTHING` is the lock primitive. Voice client mints a UUID per utterance; same key + same payload → return cached response; same key + different payload → 409. TTL 24 hours.

**Apply to:** every mutation in Phase F that the voice layer will eventually trigger. Specifically: any "Create…", "Approve…", "Release…", "Close…", "Place PO…", "Submit Requisition…", etc.

### D5 — Resource-based authorization

Every service method enforces:

```csharp
var auth = await _authz.AuthorizeAsync(user, resource, "WorkOrder.Close");
if (!auth.Succeeded) return Result.Failure<...>("Forbidden");
```

`AuthorizationPolicies` registered in `Program.cs` per entity action. **AI never gets its own role or identity.** The MCP server validates the user's bearer token, materializes a `ClaimsPrincipal`, optionally adds an `ai_session_id` claim for traceability, and passes the principal into the service. Authorization checks the user's permissions, not the AI's.

This is the single highest-leverage decision in the ADR: **by routing every mutation through `AuthorizeAsync(user, resource, policy)`, voice can't escalate privileges. Ever.**

### D6 — Polymorphic detail-page rendering

For each polymorphic primitive (WorkOrder, ProductionBatch, MaterialStructure), one parent Razor page (`Details.cshtml`) dispatches to **View Components per subtype** based on the discriminator:

```cshtml
@switch (Model.Wo.Classification)
{
    case WorkOrderClassification.Cip:
        @await Component.InvokeAsync("CipSatellite", new { woId = Model.Wo.Id });
        break;
    case WorkOrderClassification.Quality:
        @await Component.InvokeAsync("QualitySatellite", new { woId = Model.Wo.Id });
        break;
    // … one per satellite
}
```

Each View Component has its own DI graph (can fetch its own data) and its own view file. Located at:

- `Components/CipSatelliteViewComponent.cs`
- `Views/Shared/Components/CipSatellite/Default.cshtml`

Adding a new satellite later (e.g., a future Maintenance Workflow satellite) is one View Component + one switch case — no parent-page refactor.

### D7 — `<voice-action>` Tag Helper

Buttons that should be voice-invocable wrap in a custom Tag Helper:

```cshtml
<voice-action
    service="IWorkOrderService"
    method="CloseAsync"
    policy="WorkOrder.Close"
    label="Close work order"
    entity-id="@Model.Wo.Id">
    <button type="submit" asp-page-handler="Close">Close</button>
</voice-action>
```

Compiles to `<div data-voice-service="…" data-voice-method="…" data-voice-policy="…">…`. The voice client enumerates these `data-*` attributes on-page to know what's invocable. **Server never trusts the attributes** — re-validates `policy` on inbound call. The helper is a *discoverability* surface for the voice layer, not a security boundary.

### D8 — `voice_sessions` Postgres table

For Sprint 5 multi-turn state:

```sql
CREATE TABLE voice_sessions (
    id              uuid PRIMARY KEY,
    tenant_id       uuid NOT NULL,
    user_id         uuid NOT NULL,
    started_at      timestamptz NOT NULL DEFAULT now(),
    last_turn_at    timestamptz NOT NULL DEFAULT now(),
    state_json      jsonb NOT NULL DEFAULT '{}'::jsonb,
    expires_at      timestamptz NOT NULL DEFAULT now() + INTERVAL '4 hours'
);
```

Same database as everything else — durable, queryable, tenant-isolatable. ASP.NET Core Session is cookie-tied and bad for multi-device. Redis adds operational surface Replit doesn't currently support. Postgres is the right place.

**Ship the schema in Phase F** (alongside `idempotency_keys`); Sprint 5 will write to it.

### D9 — `Vendor.SendPoMethod` data model add (Phase F scope)

From the McMaster Carr example (`project_voice_ai_copilot_vision.md`):

Add to existing `Vendor` table:

| Column | Type | Purpose |
|---|---|---|
| `SendPoMethod` | enum: Email / Api / Punchout / Edi850 / FaxToEmail / PortalUpload / Pickup | Channel to send PO |
| `PoChannelConfig` | jsonb | Per-channel config (email address, API endpoint, EDI VAN, portal creds) |

Plus a new service: `IPoDispatchService` with one implementation per channel (`EmailPoDispatcher`, `ApiPoDispatcher`, etc.) all behind one interface.

### D10 — `IPurchasingService.GetUnfulfilledPurchaseNeedsByVendorAsync`

Service method joining `Items` + `ItemInventory` + `ItemVendor` + open requisitions + open PO lines, returning a cart of items below reorder point sourced from the specified vendor. Used by:

- The Phase F "Reorder Suggestions" page UI
- The future voice command "show me everything I need to purchase at McMaster Carr"

Both consumers call the same service method, get the same DTO shape.

---

## Phase F screen scope + order

**First wave (foundational, no UX-redesign risk):**

1. `RegulatoryProfiles` admin CRUD — seed 4 default profiles (FDA 21 CFR 820, AS9100, NADCAP AC7102, IATF 16949) with default Gates payloads
2. `MaterialMasters` admin CRUD — supports the StockReceipt + CutListLine flows
3. `Vendor` edit screen — add `SendPoMethod` + `PoChannelConfig` fields per D9
4. `StockReceipts` create + list — receiving workflow; heat number + mill cert URL fields
5. `Remnants` list + edit — for the metal-fab shop floor

**Second wave (production entities):**

6. `MaterialStructures` admin (Bom subtype editor) — discrete BOM creation
7. `MaterialStructures` admin (Recipe subtype editor) — process recipe creation, including Phases child grid
8. `ProductionOrders` create + list + detail — uses View Components for JobShop satellite first; other subtypes added later
9. `CutListLines` create + assign-to-nest workflow

**Third wave (batching):**

10. `ProductionBatches` create + dispatch — JobShop / Nest creation
11. `ProcessBatches` create + dispatch — heat-treat / paint / plating
12. `ProductionBatchAllocations` review page — cost-split visibility

**Fourth wave (supporting flows):**

13. **Reorder Suggestions** page — calls `GetUnfulfilledPurchaseNeedsByVendorAsync`
14. **PO creation + dispatch** — uses `IPoDispatchService`
15. **MRB Disposition workflow UI** — fills the stub from PR #119.13a
16. **HeatTreatChart + TankChemistry + WitnessCoupon** real content tables (Phase E.2c work)

**Cross-cutting (every wave):**

- Every page inherits `VoiceReadyPageModel`
- Every mutation in `IXxxService` returning `Result<T>` with DTOs
- Every mutation wrapped in `IdempotencyMediator`
- Every mutation logs to `AuditLog` (with NULL AI columns for direct-user actions)
- Every action checked via `_authz.AuthorizeAsync(user, resource, policy)`
- Polymorphic detail pages use View Components per subtype
- Voice-invocable buttons wrap in `<voice-action>` Tag Helper

---

## Estimated scope

- **Foundational infrastructure** (`VoiceReadyPageModel`, `IdempotencyMediator`, `AuditLog` extension, `voice_sessions` + `idempotency_keys` tables, AuthorizationPolicies, `<voice-action>` Tag Helper): **1 PR** — must land first
- **First wave** (RegulatoryProfile + MaterialMaster + Vendor + StockReceipts + Remnants): ~3–4 PRs
- **Second wave** (Bom + Recipe editors + ProductionOrder + CutListLine): ~4–5 PRs
- **Third wave** (Batch + Allocation): ~3 PRs
- **Fourth wave** (PO + MRB + chart content): ~3–4 PRs

**Total Phase F: 14–17 PRs.** Sprint 4.

---

## Anti-patterns (do not do)

1. **Business logic in `OnPost*` handlers.** Voice can't reuse it.
2. **A separate "AI" service account or role.** Permission boundary collapses.
3. **Live EF entity graphs in AuditLog.** Cycles + bloat — covered in existing memory.
4. **ASP.NET Core Session for voice state.** Cookie-tied, single-device.
5. **MediatR-for-the-sake-of-it.** Plain interfaces are equivalent + simpler.
6. **One mega-page with `@if (Classification == X)` everywhere.** Breaks past 4 subtypes.
7. **Trusting `<voice-action>` Tag Helper attributes on the server.** Re-validate policy server-side.
8. **Server-hashed idempotency keys from `(user + command)`.** Voice paraphrasing breaks dedup; require client-minted UUID.
9. **Routing AI calls through the page HTTP lifecycle.** Couples voice to form-binding.

---

## Decisions still open (for Dean)

1. **Approve ADR-014?** (Default recommendation.)
2. **First-wave PR order** — start with the cross-cutting infrastructure PR, then RegulatoryProfile admin? Or invert (ship a visible win first)?
3. **AuditLog column add as a single migration**, or wait until first AI-mediated action ships?

   Recommendation: single migration in the infrastructure PR. Columns are NULL for direct-user actions; cost is one ALTER TABLE.

4. **Sprint 4 sequencing vs Sprint 2 follow-ups** — do MFA / SSO / full-RLS / Onboarding ship interleaved with Phase F UI, or as a separate Sprint 4.5? Some of these (MFA, SSO) plug into the auth claims that voice depends on.

   Recommendation: MFA + SSO are gated on a separate research pass; defer to Sprint 4 second-half or Sprint 4.5. Full-RLS and Onboarding can ride alongside Phase F since neither touches voice.

---

## References

- [Microsoft Learn — Razor Pages architecture (.NET 9)](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/?view=aspnetcore-9.0)
- [Microsoft Learn — Resource-based authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased)
- [Microsoft Learn — Policy-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies)
- [Microsoft Learn — View components](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/view-components)
- [Microsoft Learn — Author Tag Helpers](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/tag-helpers/authoring)
- [Microsoft Learn — Session and state management](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state)
- [Microsoft Learn — Distributed caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
- [Microsoft Learn — Audit logs for Copilot and AI applications (Purview)](https://learn.microsoft.com/en-us/purview/audit-copilot)
- [Microsoft Learn — CopilotInteraction schema (Office 365 Management API)](https://learn.microsoft.com/en-us/office/office-365-management-api/copilot-schema)
- [GitHub Docs — Reviewing audit logs for GitHub Copilot Enterprise](https://docs.github.com/en/copilot/how-tos/administer-copilot/manage-for-enterprise/review-audit-logs)
- [brandur.org — Implementing Stripe-like Idempotency Keys in Postgres](https://brandur.org/idempotency-keys)
- [Stripe Docs — Idempotent requests](https://docs.stripe.com/api/idempotent_requests)
- [Milan Jovanović — Result Pattern in .NET](https://www.milanjovanovic.tech/blog/functional-error-handling-in-dotnet-with-the-result-pattern)
- [Red Gate Simple Talk — The Result Pattern in ASP.NET Core](https://www.red-gate.com/simple-talk/development/dotnet-development/the-result-pattern-in-asp-net-core-minimal-apis/)
- [Andrew Lock — Resource-specific authorisation in ASP.NET Core](https://andrewlock.net/resource-specific-authorisation-in-asp-net-core/)
- [Telerik — Why You Should Use View Components, Not Partial Views](https://www.telerik.com/blogs/why-you-should-use-view-components-not-partial-views-aspnet-core)
- [The Reformed Programmer — Six things I learnt about Razor Pages](https://www.thereformedprogrammer.net/six-things-i-learnt-about-using-asp-net-cores-razor-pages/)
