# 01 — Executive Summary

## What CherryAI EAM is

A multi-tenant, modern-stack Enterprise Asset Management (EAM) platform for manufacturing organizations, built on **ASP.NET Core 9.0 + EF Core 9 + PostgreSQL** with a custom design system. Three product tiers are already named in the README:

| Tier | Name | Target |
|---|---|---|
| 1 | CherryAI EAM **Launchpad** | Small businesses with basic asset tracking |
| 2 | CherryAI EAM **Autopilot** | Mid-market with automation & compliance needs |
| 3 | CherryAI EAM **Command Center** | Enterprise multi-site operations |

Positioning: **standalone financial system** that *also* integrates with ERP (SAP, Oracle, MS Dynamics), MES, and IoT/SCADA — a deliberate choice to be sellable to companies that don't want to rip out their ERP.

## What it can already do

**Asset accounting (the financial core).** Full GAAP and tax book depreciation across **22 depreciation methods**, **12 conventions**, US MACRS + Section 179 + bonus depreciation, Canadian CCA classes (16 classes seeded), multi-book per asset, FK-mapped GL accounts (asset / accumulated depreciation / expense), per-period depreciation runs, journal generation with period locking, fiscal calendar, exchange rates. This is unusually deep — most competitors require a separate finance system.

**Asset lifecycle.** Create → assign to site/location/cost center/department → MES/IoT enable → maintain → transfer (with audit trail) → improve (capitalize improvements) → dispose (partial or full, with reason + proceeds + GL entry). Asset hierarchy via `ParentAssetId`. Concurrency via PostgreSQL `xmin` row version on Assets.

**Maintenance & work orders.** PM template library, schedule generation (daily/weekly/monthly/quarterly/yearly), work request inbox, work-to-WO conversion, technician roster + skills + certifications, schedule board, closeout workflow with auto-generated summaries and lessons-learned capture, recurring failure detection, work order operations with execution fields. SmartAssistService analyzes plain-text requests and pre-populates failure code, action code, priority, asset, labor estimate.

**Procurement.** Multi-location requisitions → POs (with releases, multi-location distribution) → goods receipts → 3-way invoice match → AP. Procurement-grade items: AVL (approved vendor list), alternates, supersession, cross-references, vendor catalog enrichment, buyability scoring, EOQ/ROP stocking calculations, vendor catalog URLs, item images.

**Capital improvement projects (CIP).** Project header → cost accumulation by type → reconciliation → place in service → asset created with depreciation set up. Auto-cost posting service for ongoing projects. Cost trace queries for hierarchical breakdowns.

**Reporting (28 reports + custom builder).** Form 4562 (US), T2 Schedule 8 (Canada CCA), depreciation schedule + preview, chart of accounts, compliance, bulk export. Export formats: Excel, PDF, CSV, JSON.

**Integration platform.** Outbound: transactional outbox → background dispatcher → HMAC-signed webhooks with exponential backoff retry (8 attempts: 1, 5, 15, 60, 120, 240, 480, 960 minutes). Inbound: signature-verified receiver with idempotency keys, configurable mappings, background processor. Per-customer integration endpoints, webhook subscriptions, delivery logs.

**Multi-tenancy.** Tenant → company → site hierarchy with org node graph. `TenantContextMiddleware` resolves context per request. `OrgScopeMiddleware` handles cross-org navigation with permission checks. `CompanyHierarchyService` enforces visibility rules.

**Reference data system.** 76 lookup types seeded from JSON (`AssetType`, `POStatus`, `MaintenanceType`, `DepreciationMethod`, etc.). `ILookupService` with 10-min in-memory cache. FK-bound dropdown pattern: every entity has both legacy enum + FK column → on save, the FK is resolved and the enum is synced for backward compatibility.

**Modern UX layer.** `_ModernLayout` with sidebar nav, hero gradient `_ScreenHeader`, premium DataGrid (global search, multi-column sort, per-column filters, CSV/Excel export, column visibility), KPI cards with frosted-glass design, `_TabNav` for tabbed views, `_FormField` for standardized forms. CSS design tokens in `tokens.css` (9 families: brand, surface, border, text, semantic, typography, spacing, radius, shadows). Dark/light theme.

**Observability.** OpenTelemetry traces + metrics with OTLP exporter (conditional on `OTEL_EXPORTER_OTLP_ENDPOINT`). Cookie/Authorization headers redacted from traces. Slow-query interceptor logs anything over 500ms with full SQL + RequestId. Health checks (`/_live`, `/readyz`) with DB and Skia probes.

**Hardened production posture.** Per-request CSP nonce, `HttpOnly` cookies with `SameSite=Lax` and `SecurePolicy=Always` in prod, distributed PostgreSQL-backed login rate limiter (100/min per IP+username, atomic upsert), forwarded headers from Replit edge proxy.

## What it can't do yet (the dethroning gap list)

**Critical for enterprise wins:**

1. **Mobile-first technician execution** — no responsive-PWA-with-offline-cache work order UI. MaintainX's whole business is built on this.
2. **Real-time IoT/SCADA dashboards** — Asset model has IoT fields (`IoTEnabled`, `IoTDeviceId`, `IoTProtocol`, `IPAddress`, `MACAddress`, `LastIoTCommunication`, OEE: `CurrentAvailability/Performance/Quality/OEE`), but no live streaming UI. Maximo and Hexagon both have this.
3. **Predictive maintenance with ML** — vibration analysis, oil analysis, RUL (remaining useful life) estimation. Asset model has predictive thresholds; no engine yet.
4. **Calibration management workflow** — fields exist (`CalibrationRequired`, `CalibrationFrequencyDays`, `LastCalibrationDate`, `NextCalibrationDue`, `CalibrationVendor`, `CalibrationStatus`); no dedicated UI/workflow.
5. **Warranty management** — no dedicated entity at all. Customers track this in spreadsheets today.
6. **LOTO / safety permits** — regulatory must-have for industrial sites; not modeled.
7. **Contractor management** — insurance expiry, certification tracking, badge issuance. Not modeled.
8. **AR/QR field scanning** — Barcode service generates codes; no consuming UX for technicians.
9. **Document management beyond simple attachments** — no versioning, ACL, archival, OCR.
10. **Linear assets (pipelines, roads, conveyors)** — Hexagon HxGN's specialty. Asset model has `IsLinear` flag but no segment / station / KP modeling.

**Important but lower-tier:**

11. Energy/utility consumption tracking
12. Sustainability/ESG reporting  
13. Reliability-Centered Maintenance (RCM) framework
14. FMEA / failure mode catalog (codes exist, framework doesn't)
15. Visual workflow/approval builder
16. Spare parts demand forecasting
17. Native mobile apps (iOS/Android — vs. just PWA)
18. BIM/CAD integration
19. AI copilot embedded in the technician's workflow (vs. a separate chat page)
20. OpenAPI/Swagger spec (no machine-readable API contract today)

## State of the codebase right now

**Build:** 0 errors. Warnings are nullable-reference cosmetic (CS8602, CS8619, CS8620).

**Mid-flight refactor:** FK-bound dropdown migration is ~95% done. The one remaining page is `Pages/Purchasing/Details.cshtml.cs` (Update Header, status workflow buttons, Duplicate PO). FK value backfill for existing rows is also pending.

**Deferred dev shortcuts:**
- Authentication is currently disabled in development (fallback policy allows anonymous).
- Password hashing is SHA-256 — needs to become Argon2id or bcrypt before any production customer touches the system.
- OpenAI API key is not configured by default; AiAssistantService will throw at runtime if called.
- Azure SQL connection string with plaintext password is sitting in `appsettings.Development.json` — should move to user-secrets.
- No OpenAPI/Swagger spec generated.

**Architectural strengths to preserve:**
- The lookup service architecture is the single most important pattern. Every dropdown lives there; never hardcode again.
- The outbox + dispatcher pattern is correctly implemented; new business events should write to the outbox in the same transaction as the business change.
- The TenantContext + middleware are well-factored; never scope a query manually — go through the context.
- The premium DataGrid + KPI strip + screen header partials are the UI baseline; new pages should reuse them.

## What makes this app potentially dangerous to incumbents

1. **Modern stack vs. legacy.** .NET 9 / C# 12 / PostgreSQL / Razor Pages 2026-grade. Maximo runs on WebSphere; HxGN EAM was Infor's old Java/Lawson stack; SAP EAM is ABAP. CherryAI's iteration speed is 5-10× faster on equivalent features.

2. **AI-native architecture from day one.** OpenAI client wired in, SmartAssistService primitive ready to be upgraded to Claude, conversational asset twin is feasible because the data is already structured.

3. **Multi-tenant from day one.** Maximo/SAP/Hexagon all bolted multi-tenancy on later. CherryAI was designed for SaaS from the first migration.

4. **Lookup-driven everything.** Customers can self-customize 76 reference data domains with no code changes. Maximo customers wait 3-9 months for SI partners to do this.

5. **Outbox + webhooks native.** SAP wants you to buy SAP PI/PO ($$). CherryAI ships with HMAC-signed webhooks and inbound receivers in the box.

6. **Premium UX as a wedge.** Screenshots of Maximo are an instant sales pitch for any modern alternative. CherryAI's design system is genuinely 2026-grade.

7. **Three-tier productization with credible SMB freemium possible.** No enterprise EAM has a usable Launchpad-grade tier. This opens the bottom of the market.

## The 90-day shape of victory

If Claude Code shipped these in the next 90 days, CherryAI would have a defensible "step ahead" against every named competitor:

| Sprint | Deliverable | Kill shot |
|---|---|---|
| **Sprint 1** | Finish FK migration; re-enable auth + Argon2; ship OpenAPI spec | Production readiness gate cleared |
| **Sprint 2** | PWA mobile work order execution with offline cache; QR scanning to open asset/WO | Eats MaintainX's wedge |
| **Sprint 3** | Voice-to-WO via Claude API; auto-fills failure/action codes, asset, priority, parts | Maximo can't do this; AI moat opens |
| **Sprint 4** | Real-time IoT dashboard for assets with `IoTEnabled=true`; OEE live tiles | Eats Hexagon's reliability lead |
| **Sprint 5** | Calibration management workflow (fields exist, ship the UI); LOTO permit module v1 | Closes the regulated-industries objection |
| **Sprint 6** | Conversational Asset Twin: chat with any asset, full record + IoT + manuals + history | Singular feature no incumbent has |

Every sprint above is detailed in `09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md`.

## Bottom line

CherryAI EAM is **not a prototype**. It's a 310,000-LOC, feature-complete, modern-stack EAM with a well-thought-out architecture, mid-flight on its last big refactor, with deeper depreciation/financial coverage than most competitors and a UI that's already ahead of every incumbent. The remaining work splits cleanly into:

- **Polish & harden** (finish FK migration, auth, password hashing, OpenAPI) — 2-3 weeks of focused engineering.
- **Disruptive expansion** (mobile + AI + IoT live + calibration + LOTO + warranty) — a 12-week roadmap that would credibly position the product as the AI-native alternative to Maximo for the next decade.

The visionary instinct that built this is right: the bones are there. The next move is to ship the wedge feature (mobile + voice-to-WO) that no incumbent can match, and then sell.
