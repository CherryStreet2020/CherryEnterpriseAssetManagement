# Working in this repo with Claude Code

This file is the operating manual for any Claude Code session picking up CherryAI EAM.
Read it first. It is the single source of truth for *how we work in this repo*; the *what
the app is* lives in [`replit.md`](replit.md), and the *current state of the codebase* lives
in [`HANDOFF_STATUS.md`](HANDOFF_STATUS.md) and [`docs/audit-2026-05-07/`](docs/audit-2026-05-07/00_AUDIT_INDEX.md).

## The git workflow (non-negotiable)

CherryAI EAM runs on Replit. The rule is **one-way sync from GitHub to Replit**.

```
Claude Code (local)  ──commit──►  GitHub main  ──pull──►  Replit
```

- **Claude pushes to `main`. Replit only pulls.** No commits originate in Replit. No
  pushes from Replit. The Replit Git pane's "Push" button is off-limits.
- The local clone lives at
  `/Users/deandunagan/Documents/Claude/Projects/EnterpriseAssetManagament/CherryEnterpriseAssetManagement/`.
  All edits happen there.
- Identity for commits: `CherryStreet <dunagan.dean@gmail.com>` (configured per-repo,
  not globally). Don't commit under the Replit-bot identity.
- After a push, tell the user "pull on Replit" so the running app picks up the change.

If the user reports Replit and GitHub are out of sync, the diagnosis is almost always
that something committed in Replit. Resolution: pull from GitHub on Replit and reset
hard if Replit's tree has unpushed local commits. Confirm before doing anything
destructive on the Replit side.

## Branching

- Default to direct commits on `main` for small, isolated changes (single page, single
  service, doc updates).
- Use a short-lived feature branch + PR for any of: schema migration, auth changes,
  password hashing, anything that touches `Program.cs` middleware order, anything that
  removes or renames a public API surface, anything that touches `appsettings*.json`.
  Squash-merge to `main`.
- Never force-push `main`. Never rewrite published history.

## Commit messages

Conventional, imperative, short. Reference the audit task numbers when applicable.

```
Sprint 0 #1: finish FK migration on Purchasing/Details

- OnPostUpdateHeaderAsync: resolve FK from posted LookupValueId
- Status workflow buttons: same FK pattern as Items/Edit
- OnPostDuplicatePOAsync: copy FK columns, not legacy enums
```

## What to read before changing anything

| If you're touching... | Read |
|---|---|
| anything | [`replit.md`](replit.md), [`HANDOFF_STATUS.md`](HANDOFF_STATUS.md) |
| services, models, or DB | [`docs/audit-2026-05-07/03_DOMAIN_MODELS_AND_SCHEMA.md`](docs/audit-2026-05-07/03_DOMAIN_MODELS_AND_SCHEMA.md), [`docs/audit-2026-05-07/04_SERVICES_LAYER.md`](docs/audit-2026-05-07/04_SERVICES_LAYER.md) |
| Razor pages / UI | [`docs/audit-2026-05-07/05_PAGES_AND_UI.md`](docs/audit-2026-05-07/05_PAGES_AND_UI.md), [`docs/DataGridPremium.md`](docs/DataGridPremium.md) |
| controllers / API | [`docs/audit-2026-05-07/06_CONTROLLERS_AND_API.md`](docs/audit-2026-05-07/06_CONTROLLERS_AND_API.md) |
| middleware / startup | [`docs/audit-2026-05-07/02_APP_FOUNDATION.md`](docs/audit-2026-05-07/02_APP_FOUNDATION.md) |
| migrations / schema | [`docs/DatabaseMigrations.md`](docs/DatabaseMigrations.md), [`docs/DatabaseSchema.md`](docs/DatabaseSchema.md) |
| brand / UI tokens | [`docs/BrandGuardrails.md`](docs/BrandGuardrails.md) |
| competitive / strategy | [`docs/audit-2026-05-07/08_COMPETITIVE_GAP_ANALYSIS.md`](docs/audit-2026-05-07/08_COMPETITIVE_GAP_ANALYSIS.md), [`docs/audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md`](docs/audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md) |

## Architectural patterns to follow (don't break these)

1. **Lookup-driven dropdowns.** Every dropdown value is a `LookupValue` row keyed by
   `LookupTypeCode`. New entities get a `*LookupValueId` FK column. `ILookupService`
   has a 10-min in-memory cache. Never hardcode an enum in a UI dropdown again.
2. **Tenant context.** Never query an entity directly without going through
   `TenantContextMiddleware` / `CompanyHierarchyService`. Multi-tenant isolation lives
   at the middleware layer; bypassing it is a data-leak bug.
3. **Outbox pattern for integrations.** Business event + outbox row write happen in
   the **same** `SaveChangesAsync` transaction. The dispatcher background service ships
   them out. Never call a webhook directly from a request handler.
4. **UI partials.** New pages reuse `_ScreenHeader`, `_ModernLayout`, the premium
   DataGrid, `_TabNav`, `_FormField`, `_KpiStrip`. Don't reinvent.
5. **Period locking + concurrency.** Asset writes use the PG `xmin` row version.
   Financial writes check the period lock. Don't bypass either.

## Build / run

- `dotnet build` from the repo root. Targets `net9.0`.
- Local run: `dotnet run` — defaults to `http://localhost:5000`. Requires
  `DATABASE_URL` env var pointing at a Postgres (Neon).
- Migrations: `dotnet ef migrations add <Name>` then `dotnet ef database update`.
- 0 errors is the bar. Warnings are nullable-reference noise (CS8602/8619/8620);
  don't *introduce* new ones.

## What's deferred

The Sprint 0 list in [`docs/audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md`](docs/audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md)
is the production-hardening backlog. The top of that list:

1. Finish FK migration on `Pages/Purchasing/Details.cshtml.cs`
2. Backfill FK values for legacy rows
3. Re-enable auth (currently anonymous-fallback in dev)
4. Argon2id replacing SHA-256 in `Services/AuthService.cs`
5. Move plaintext secrets out of `appsettings.Development.json`
6. Add Swashbuckle for `/swagger`
7. Postgres advisory lock in `SeedGuardService`
8. Strongly-typed outbox payloads with versioned `IDomainEvent`

Pick from the top unless the user asks for something else.

## Things not to do

- Don't push from Replit.
- Don't rewrite or force-push `main`.
- Don't hardcode dropdown values — use `LookupService`.
- Don't query entities outside the tenant context.
- Don't write to a webhook endpoint synchronously — use the outbox.
- Don't add `// removed X` or `_unused` placeholders. Delete unused code.
- Don't add backwards-compat shims for code only this repo calls.
- Don't enable auth bypass even temporarily without a comment + tracking task.
- Don't commit secrets or `*.env`. The `.gitignore` covers this; don't override it.
- Don't delete `extracted/` or the original zip from outside the repo without asking;
  they're outside this repo and may be the user's reference copy.
