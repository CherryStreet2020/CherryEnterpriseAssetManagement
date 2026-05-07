# Working in this repo with Claude Code

This file is the operating manual for any Claude Code session picking up CherryAI EAM.
Read it first. It is the single source of truth for *how we work in this repo*; the *what
the app is* lives in [`replit.md`](replit.md), and the *current state of the codebase* lives
in [`HANDOFF_STATUS.md`](HANDOFF_STATUS.md) and [`docs/audit-2026-05-07/`](docs/audit-2026-05-07/00_AUDIT_INDEX.md).

## The git workflow (non-negotiable)

CherryAI EAM runs on Replit. **One-way sync from GitHub to Replit.** Every change
ships through a pull request with green CI before it touches `main`.

```
Claude Code (local)  ──feature branch──►  PR  ──CI green──►  squash-merge to main
                                                                       │
                                                          Replit ◄──pull──┘
```

### The non-negotiables

- **Replit never pushes.** No commits originate in Replit. The Replit Git pane's
  "Push" button is off-limits. After a merge, tell the user to `git pull origin main`
  in the Replit shell.
- **No direct pushes to `main`.** Even from this local clone. The `pre-push` hook
  enforces this — see `.githooks/pre-push`.
- **Every change goes through a PR.** Even one-line doc fixes. Workflow per change:
  ```
  git checkout -b <type>/<short-name>
  # … edit, commit (commit-msg hook validates), push …
  gh pr create --fill
  # … wait for the `build` workflow to go green …
  gh pr merge --squash --delete-branch
  ```
- **Squash merge only.** The repo is configured to disallow merge commits and rebase
  merges. Each PR becomes one commit on `main`.
- **No force-push.** No history rewrites on `main`.
- **Commit identity:** `CherryStreet <dunagan.dean@gmail.com>` (configured per-repo,
  not globally). Don't commit under the Replit-bot identity.

### Why client-side, not GitHub branch protection

GitHub's branch protection and Rulesets require a paid plan on private repos. We
enforce the same discipline on the client: a `pre-push` hook blocks direct pushes
to `main`, a `commit-msg` hook enforces conventional commits, and a `pre-commit`
hook scans staged changes for accidental secrets. The hooks are versioned in
`.githooks/` and activated via `core.hooksPath` — see "Setup on a fresh clone."

### If Replit and GitHub drift

The cause is almost always something committed in Replit. Resolution:
```
# in Replit shell
git fetch origin
git reset --hard origin/main
```
**Confirm with the user before running any destructive command on the Replit side.**

## Setup on a fresh clone

```
git clone https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement.git
cd CherryEnterpriseAssetManagement
./scripts/setup-dev.sh   # configures core.hooksPath, chmod +x .githooks/*
git config user.name  "Your Name"
git config user.email "your-email@example.com"
```

Optional but recommended:
```
brew install gitleaks   # the pre-commit hook auto-uses gitleaks if present
```

## Branching

- One branch per PR. Naming: `<type>/<short-kebab-name>` where type is `feat`,
  `fix`, `docs`, `chore`, `refactor`, `test`, `build`, `ci`, or `perf`.
  Examples: `feat/voice-to-wo`, `fix/auth-cookie-secure`, `chore/argon2-hashing`.
- Branch life is short. Open the PR, get it green, merge, delete. Don't let a
  branch live longer than ~24 hours unless it's a documented WIP.
- Never reuse a merged branch name. Squash-merge configures GitHub to delete the
  remote branch on merge; delete the local branch too.

## Commit messages

The `commit-msg` hook enforces this. First line must match either:
```
<type>(<scope>): <subject>
Sprint <N> #<task>: <subject>
```

Valid types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`,
`build`, `ci`, `perf`. Subject in imperative voice, ≤72 chars.

Body (optional, after blank line) explains *why* and lists what changed. Reference
the audit task numbers when applicable.

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
is the production-hardening backlog. **The audit was wrong on items #1
and #3 — see `## Audit corrections` below.** The current state:

| # | Audit task | Current state |
|---|---|---|
| 1 | Finish FK migration on `Pages/Purchasing/Details.cshtml.cs` | ✅ Already migrated (audit was outdated). Real holdouts shipped via PR #2 + PR #10. See `docs/FK_MIGRATION_STATUS.md` for what remains. |
| 2 | Backfill FK values for legacy rows | Open. The cross-seed alignment chain (PRs #5–7) covered seed/enum drift; legacy-row backfill for `*LookupValueId` columns where they exist on entities is still TBD. |
| 3 | Re-enable auth | ✅ Was already enabled. `Program.cs:581` calls `MapRazorPages().RequireAuthorization()`. |
| 4 | Argon2id replacing SHA-256 | ✅ Shipped (this PR — `feat/argon2id-password-hashing`). Backward-compat verify; rolling re-hash on next login. |
| 5 | Move plaintext secrets out of `appsettings.Development.json` | Open. |
| 6 | Add Swashbuckle for `/swagger` | Open. |
| 7 | Postgres advisory lock in `SeedGuardService` | Open. |
| 8 | Strongly-typed outbox payloads with versioned `IDomainEvent` | Open. |

Plus the schema-touching FK migrations tracked in [`docs/FK_MIGRATION_STATUS.md`](docs/FK_MIGRATION_STATUS.md) (`InventoryList` and `WorkRequest`).

Pick from the open list unless the user asks for something else.

## Audit corrections

The 2026-05-07 audit got two things wrong, found during execution:

- **Sprint 0 #1 was overstated.** The audit named `Pages/Purchasing/Details.cshtml.cs`
  as the FK-migration holdout. Direct inspection found that page fully migrated.
  The real holdouts were `Pages/Assets/Dispose.cshtml.cs`, `Pages/Materials/ItemEdit.cshtml.cs`,
  and `Pages/Maintenance/ScheduleBoard.cshtml.cs`. Plus a chain of seed/enum drift
  (5 lookup types) that had to be fixed before the page edits were safe.
- **Sprint 0 #3 was wrong.** Auth is and was already enforced via
  `MapRazorPages().RequireAuthorization()`. The "anonymous fallback" claim was
  incorrect. The real production-readiness gap was the password hashing (Sprint 0 #4),
  including a latent bug where `Pages/Admin/Users.cshtml.cs` had its own private
  `HashPassword` using **unsalted** SHA-256, while `AuthService` used SHA-256 with
  a fixed app-wide salt — meaning admin-created users could not log in.

## Things not to do

- Don't push from Replit.
- Don't push directly to `main`. (The `pre-push` hook will block you anyway.)
- Don't bypass the hooks (`--no-verify`) without a stated reason and a follow-up.
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
