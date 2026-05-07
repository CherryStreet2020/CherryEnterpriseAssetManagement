# CherryAI EAM — Hyper-Detailed Audit Bundle

**Audit Date:** 2026-05-07
**Auditor:** Claude (Cowork mode)
**Source:** `FIxedAssetsProject-1-25-26-750pm.zip` (the dropped repo)
**Audience:** Claude Code agent picking up this project to ship the next chapter
**Mission:** Give Claude Code everything it needs — what we have, what we're missing, and how we dethrone IBM Maximo, SAP EAM, Hexagon HxGN EAM, IFS Cloud, MaintainX, and Accruent.

---

## How to read this bundle

Open the files in order. Each part is self-contained but they build on each other.

| # | File | What's in it | Words |
|---|------|--------------|-------|
| 00 | `00_AUDIT_INDEX.md` | This file — orientation, headline numbers, how to use the audit | — |
| 01 | `01_EXECUTIVE_SUMMARY.md` | One-page readout: what the app is, what state it's in, top 10 things to know | ~2,500 |
| 02 | `02_APP_FOUNDATION.md` | Program.cs, DI, middleware, hosting, auth, multi-tenancy, observability, rate limiting, AI wiring | ~5,000 |
| 03 | `03_DOMAIN_MODELS_AND_SCHEMA.md` | All 66 entities, FK/lookup migration status, indexes, multi-tenant scoping, schema gaps | ~7,500 |
| 04 | `04_SERVICES_LAYER.md` | All 50 services, deep-dives on Lookup/Depreciation/Maintenance/Webhook/Seeding/AI engines | ~7,000 |
| 05 | `05_PAGES_AND_UI.md` | All 410 razor pages, design system, workflows (asset/PM/procurement/CIP/depreciation), UI gaps | ~7,000 |
| 06 | `06_CONTROLLERS_AND_API.md` | All 10 controllers, API surface, webhook security, auth, surface gaps | ~5,700 |
| 07 | `07_PROJECT_DOCUMENTATION.md` | Synthesis of 64 docs: architecture, ADRs, decisions, runbooks, brand guardrails | ~5,000 |
| 08 | `08_COMPETITIVE_GAP_ANALYSIS.md` | vs. Maximo/SAP/Hexagon/IFS/MaintainX/Accruent — feature matrix, advantages, gaps, "dethrone" plays | ~12,000 |
| 09 | `09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md` | Top-priority bets to widen the lead, ordered for the next quarter | ~3,500 |

**Total: ~55,000 words / ~120 printed pages.** This is the full picture.

---

## Headline numbers

| Metric | Count |
|---|---|
| C# source files | 375 |
| Razor pages (.cshtml) | 410 |
| Razor page models (.cshtml.cs) | 132 |
| Domain models | 66 |
| Services | 50 |
| Controllers | 10 |
| EF Core migrations | 62 |
| Module subdirectories under `/Pages` | 21 |
| Lookup types seeded from JSON | 76 |
| Documentation files in `/docs` | 64 |
| Architecture Decision Records | 10 |
| Total C# lines of code (excl. obj/bin) | ~310,000 |

---

## Tech stack at a glance

- **Backend:** ASP.NET Core 9.0 (Razor Pages), C# 12, EF Core 9.0, PostgreSQL (Neon-backed via Replit)
- **Reporting:** ClosedXML (Excel), QuestPDF (PDF)
- **Barcodes:** ZXing.Net.Bindings.SkiaSharp
- **Observability:** OpenTelemetry (traces + metrics, OTLP exporter, AspNetCore + HttpClient + EF Core instrumentation)
- **AI:** OpenAI integration (key configurable, currently empty by default)
- **Hosting:** Replit Autoscale, port 5000
- **Frontend:** Custom design tokens (tokens.css) + cherryai-theme.css + Bootstrap 5 base, premium DataGrid, KPI cards, dark/light themes
- **Brand colors:** Red `#cf3339`, Navy `#081e3a`

---

## What state is the app in?

**Build status:** 0 errors. Warnings are nullable-reference cosmetic noise (CS8602/8619/8620).

**Functional status:** Feature-complete for the v1 scope. The app is mid-migration on a major architectural shift — moving every dropdown from hardcoded enums to FK-bound `LookupValue` references. ~16 of ~19 critical pages are migrated; **Purchasing/Details.cshtml.cs** is the main holdout. Backfill of FK values for legacy rows is also pending.

**Production readiness:** Mostly there. Outstanding items: re-enable authentication (currently disabled in dev), upgrade password hashing from SHA-256 to Argon2/bcrypt, configure OpenAI key, ship Swagger/OpenAPI, harden the seed guard with a distributed lock.

---

## The three things Claude Code should do first

1. **Finish the FK-bound dropdown migration.** Specifically `Pages/Purchasing/Details.cshtml.cs` — `OnPostUpdateHeaderAsync`, `OnPostDuplicatePOAsync`, and the status workflow buttons all need the FK pattern. Then run a backfill that walks every table with a `*LookupValueId` FK column, finds rows where the FK is null but the legacy enum has a value, and resolves the FK by matching the enum name to the LookupValue Code.

2. **Re-enable authentication and switch password hashing to Argon2.** The auth pipeline is wired up correctly (cookie auth, 3 roles, AdminOnly/AccountantOrAdmin/AllUsers policies) but the fallback policy is allowing anonymous in dev. SHA-256 in `AuthService` needs to become Argon2id with a per-user salt before any prod customer touches it.

3. **Pick the first disruptive play from the roadmap (Section 09)** and ship it. The recommended first move is the **AI-Native Voice-to-Work-Order** flow on mobile — every other competitor still requires technicians to type into a keyboard or tap through a 5-step form. CherryAI already has SmartAssistService for keyword inference; swapping the keyword regex for Claude API calls and wiring it to a PWA is a 2-3 week build that is genuinely impossible for Maximo/SAP/Hexagon to copy quickly because their stacks are 15 years old.

---

## How to navigate the audit

- **If you're touching code:** start with 02 (foundation) → 03 (models) → 04 (services). The patterns there govern everything.
- **If you're adding a feature:** read 05 (pages) for the existing UI conventions, then 03 for where to add entities, then 04 for the service patterns.
- **If you're integrating:** read 06 (controllers/API) and the integrations section of 04.
- **If you're talking to investors or customers:** read 01 (exec summary), 08 (competitive analysis), 09 (roadmap).
- **If you're doing devops:** read the deployment, observability, and rollback sections of 07.

---

*Generated by an autonomous code audit. No source files were modified. The only outputs are the markdown files in this `AUDIT/` directory.*
