# Working in this repo with Claude Code

This file is the operating manual for any Claude Code session picking up CherryAI EAM.
Read it first. It is the single source of truth for *how we work in this repo*; the *what
the app is* lives in [`replit.md`](replit.md), and the *current state of the codebase* lives
in [`HANDOFF_STATUS.md`](HANDOFF_STATUS.md) and [`docs/audit-2026-05-07/`](docs/audit-2026-05-07/00_AUDIT_INDEX.md).

## How Claude communicates in this repo (token-efficiency rules)

Dean is paying for every token. Default to concise. These rules are mandatory unless
Dean explicitly asks for more detail.

**Response style:**
- Skip preamble. No "Great question!", "I'll help you with that", "Certainly!", "Let me…".
- Lead with the result, not the runway. Code/diff first, brief explanation after.
- Bullets over paragraphs when listing 3+ items.
- One-sentence acknowledgments only when needed ("On it.", "Got it.", "Doing great, Dean.").
- No "what I did" summaries after edits — Dean can see the diff. A summary is only
  warranted at the end of a multi-PR sequence or when the user explicitly asks.
- No verbose confirmation chains ("Now I'll…", "Next I'll…"). Just do the next step.

**File reading discipline:**
- Don't `Read` entire migration files (300+ lines of designer dump) to verify one index
  name — `Grep "IX_..."` with `-C 2` is enough.
- Don't re-read a file you just edited. Edit/Write would have errored if it failed.
- When auditing the codebase, prefer surgical Grep + Read with line offsets over
  whole-file dumps. Delegate broad investigations to the Agent tool so the raw
  output stays out of the main conversation.

**Tool result reuse:**
- CI polling: fetch `gh pr checks` once, sleep 60–90s between fetches, not 30–40s.
  Don't re-fetch identical "all pending" results for 4 rounds in a row.
- Codex GraphQL: fetch review threads once at green-CI time. Hold the thread IDs in
  the response; don't re-query.
- Build logs: trust `Build succeeded` the first time. Don't re-tail the same log
  after an osascript timeout if the command already completed.
- Replit screenshots: use `mcp__Claude_in_Chrome__javascript_tool` for DOM
  assertions ("did the write outcome render", count sections + buttons + testids)
  when text is enough. Reserve screenshots for layout/visual verification.

**Output volume:**
- After a successful ship: one short summary table + "next up" line. NOT a celebration
  paragraph + bullet list of every step + cumulative count + recap of cumulative count
  + another recap.
- Don't repeat what Dean just said back to him as confirmation.

**When verbose IS warranted:**
- Closing a Sprint / Wave / multi-PR sequence (proportional to the milestone).
- Codex bug analysis (need to quote the bug + show the fix).
- Strategic architecture decisions (Dean wants the reasoning).
- When Dean explicitly asks "explain X" or "walk me through Y".

## Pre-PR self-review (Superpowers-style)

Inspired by Superpowers' `subagent-driven-development` pattern. Codex caught 12
real bugs across Wave 1 (4 P1 + 8 P2) AFTER PRs opened, costing a second CI
round-trip every time. A pre-PR subagent review catches those bugs before the
PR opens.

**The rule:** For every non-trivial PR (entity/service/migration changes —
NOT docs-only, NOT typo fixes), before the `gh pr create` call, dispatch
the `code-reviewer` Agent subagent with:

- The diff scope (which files changed, why)
- The spec reference (which §-section of which research doc drives this)
- A specific ask: "Audit for spec-compliance bugs AND code-quality bugs.
  Flag any null-handling, FK direction, tenant-scope, idempotency, or
  enum-default issues. Return a punch list, severity-labeled (P1/P2/P3).
  Under 300 words."

If the subagent returns P1s: fix them. THEN open the PR.
If only P2/P3: judgment call — fix obvious ones, file the rest as known
trade-offs in the PR body.

**When to skip the pre-PR review:**
- Docs-only changes (CLAUDE.md, README, comment-only edits)
- Pure migration scaffolding (no service logic)
- Codex-fix follow-up commits (already reviewed once)

**Cost vs. savings:** A subagent review burns ~3-5K tokens. A wasted CI
round-trip burns 4-5 minutes + similar tokens for the re-fix commit. Break-even
at one P1 caught; net positive at two. Wave 1's record was 2.4 bugs/PR average.

## Parallel PRs via git worktrees

Inspired by Superpowers' `using-git-worktrees` skill. We currently sit idle
4-5 min × multiple PRs/session waiting on CI. Worktrees let us prep PR #N+1
while PR #N is in CI.

**The pattern:**
```
git worktree add ../<repo>-pr<N+1> -b <next-branch> main
cd ../<repo>-pr<N+1>
# … work on PR #N+1 here while PR #N is in CI in the main clone …
```

**When to use:**
- Multi-PR sessions (3+ PRs planned). Skip for single-PR work.
- After a PR is pushed and CI is running — that's the cue to switch to the
  worktree and start the next one.

**Discipline:**
- One worktree per PR. Delete with `git worktree remove ../<repo>-prN`
  after the PR merges.
- Never push from a worktree to `main` directly (the pre-push hook applies
  to all worktrees anyway).
- The ship harness (`.ship/run.sh`) works from any worktree as long as
  `.ship-config.sh` is present at the repo root of that worktree.

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
| 4 | Argon2id replacing SHA-256 | ✅ Shipped via [#11](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/11). Backward-compat verify; rolling re-hash on next login. Also fixed a latent bug where `Pages/Admin/Users.cshtml.cs` used a different (unsalted) SHA-256 than `AuthService` and broke admin-created logins. |
| 5 | Move plaintext secrets out of `appsettings.Development.json` | ✅ Shipped via [#12](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/12). The Azure SQL connection string with embedded password was dead-code fallback (Replit always sets `PGHOST` etc.). **Note: the password `ABS12345!` is still in git history; rotate it on Azure if that server is still active.** |
| 6 | Add Swashbuckle for `/swagger` | ✅ Shipped via [#13](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/13). On in Development; in any other env, opt in via `ENABLE_SWAGGER=true`. UI at `/swagger`, JSON spec at `/swagger/v1/swagger.json`. Cookie auth modeled in the spec. |
| 7 | Postgres advisory lock in `SeedGuardService` | ✅ Shipped via [#14](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/14). `TryAcquireSeedLockAsync` uses `pg_try_advisory_lock(4815162342)`; the entire startup seed block in `Program.cs` runs only if the lock is acquired. Concurrent app instances skip the seed work cleanly. |
| 8 | Strongly-typed outbox payloads with versioned `IDomainEvent` | ✅ Shipped via [#29](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/29) (design), [#30](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/30) (Phase 1 — `IDomainEvent` + `DomainEventRegistry` + `PayloadVersion` column), [#31](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/31) (Phase 2 — 5 call sites migrated to typed enqueue), [#32](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/32) (Phase 3 — auto-generated `/Admin/Webhooks/Catalog` page), [#33](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/33) (Phase 5 — legacy untyped overloads removed). Phase 4 (V1→V2 parallel-emit tooling) stays deferred until a payload actually needs to evolve. See [`docs/design/OUTBOX_TYPED_PAYLOADS.md`](docs/design/OUTBOX_TYPED_PAYLOADS.md). |

Plus the schema-touching FK migrations tracked in [`docs/FK_MIGRATION_STATUS.md`](docs/FK_MIGRATION_STATUS.md) (`InventoryList` and `WorkRequest`).

**Sprint 0 production-hardening is complete except #2 (legacy-row FK backfill).** Code-review followups continue in [`docs/CODE_REVIEW_FOLLOWUPS.md`](docs/CODE_REVIEW_FOLLOWUPS.md).

**Sprint 0.5 (in progress)** — A 2026-05-08 structural audit ([`docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md`](docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md)) found 8 Severity-1 and 11 Severity-2 flaws in the financial-plumbing backbone (PO → Receipt → Invoice → GL → Asset/CIP is largely disconnected; PM lifecycle has an ID-namespace collision; WO ActualCost is hand-typed not rolled up; receiving doesn't move inventory). Sprint 0.5 addresses every S1 and S2 before pivoting to product-roadmap work. The audit doc lists ordered work items 1–20.

After Sprint 0.5 lands, the next strategic gear is product-roadmap work — see [`docs/audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md`](docs/audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md) Sprints 1–3.

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
