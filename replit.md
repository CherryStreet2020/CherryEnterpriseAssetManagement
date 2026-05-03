# CherryAI Enterprise Asset Management

## Overview
CherryAI Enterprise Asset Management is an ASP.NET Core Razor Pages application for holding companies with multiple manufacturing subsidiaries. It provides comprehensive asset lifecycle management, including GAAP & tax book depreciation (US and Canadian compliance), maintenance tracking, and capital improvement project management. The project aims to be a leading solution in the fixed asset management market by integrating with ERP systems and offering advanced features for enterprise asset management.

## User Preferences
I prefer clear and concise information. When making changes, please explain the reasoning and potential impact. I value iterative development with frequent check-ins for major architectural decisions. Ensure the codebase remains clean, well-documented, and follows established ASP.NET Core conventions.

## System Architecture
The application is built on ASP.NET Core using Razor Pages, with PostgreSQL as the database and Entity Framework Core for ORM.

**UI/UX Decisions:**
- A modern, professional SAGE-like UI with a dark sidebar, breadcrumbs, and zero-modal inline UX, adhering to a "CherryAI Brand Theme" (Brand Red, Navy, Inter + JetBrains Mono fonts).
- Supports Dark and Light modes, persisted via local storage, respecting user preferences, and utilizing CSS custom properties.
- Features a "Luxury Surface System" with multi-layer shadows, 16px card radius, and premium KPI stat cards.
- Custom design system utilizing CSS variables and Design Tokens Architecture.
- Incorporates Premium DataGrid Controls for enhanced table functionality (search, sort, filter, export, client-side pagination).
- Implements a "Premium Asset Page Design System" with spec-card containers, color-coded accent dots, field grids, and boolean chips.
- Uses a UI Conformance System with shared partials and deterministic smoke tests.
- Features a "Modern Navigation Overhaul" with grouped sidebar, command palette (Ctrl+K), global search, and responsive design.
- All transactional entities use a unified header+detail workspace pattern with inline editing, eliminating popup modals.
- The application supports white-label branding, with specific branding implemented for "ABS Machining EAM."

**Technical Implementations & Feature Specifications:**
- **Organizational Hierarchy:** Supports Organization → Company → Site → Location → Asset structure.
- **Asset Management:** Comprehensive tracking of asset lifecycle.
- **Depreciation:** Multi-book architecture with 22 methods, 12 conventions, and US/Canadian tax engines, including enhanced handling for MidQuarter, ModifiedHalfYear, and MACRS.
- **Enterprise Master Files:** Includes Chart of Accounts and extensive system configuration.
- **User Authentication & RBAC:** Cookie-based authentication with `Admin`, `Accountant`, and `Viewer` roles.
- **Multi-Tenant Isolation:** Enforces company and site-scoped data access for all operations.
- **Audit Trail & Period Locking:** Tracks changes and manages accounting periods, preventing postings to closed periods.
- **Flexible Deployment Mode:** Configurable Financial Mode (Standalone or ERP Integration) with module activation.
- **Vendor & Purchase Management:** Complete vendor master file, POs, goods receiving, and AP with 3-way PO matching, featuring a redesigned dedicated vendor management workflow.
- **Work Order System:** Enhanced maintenance events with labor tracking and approval, including safety badging.
- **Smart Assist Work Requests:** Self-service work requests with rule-based auto-generation of work orders.
- **Adaptive Navigation:** Dynamic sidebar based on Financial Mode.
- **Capital Improvement Projects (CIP):** End-to-end costing, traceability, and capitalization workflow.
- **Bulk Operations:** Supports bulk asset transfers, status changes, and partial disposals.
- **AI Assistant:** Enterprise-grade natural language assistant.
- **Universal Attachment System:** Allows attachments to assets and related entities.
- **Multi-Company Support:** Manages multiple companies with currency selection and a robust company hierarchy service for access control, using a single scope selector in the sidebar.
- **Fiscal Calendar System:** Full `FiscalYear` and `FiscalPeriod` tracking.
- **Inventory & Parts Management:** Enhanced Item Master with PM templates, meter readings, kits, work order parts tracking, purchase requisitions, revision control, cross-referencing, and procurement-grade features (AVL, alternate parts).
- **Meter Reading History:** Full meter reading history system on Asset detail page.
- **Module Guard Coverage:** `IModuleGuardService` enforced on all module pages to control access based on module activation.
- **Barcode System:** Generation and scanning API.
- **Mobile API Preparation:** REST endpoints for mobile app integration.
- **MES Integration:** Supports Work Center assignment, Production Line/Cell tracking, and OEE.
- **IoT Integration:** Full IoT device connectivity and SCADA integration.
- **Calibration Tracking:** Complete calibration management.
- **Safety & Compliance:** Safety classifications and regulatory requirements (LOTO, Confined Space, OSHA).
- **Predictive Maintenance:** Full PdM support with sensor readings, thresholds, and ML-driven predictions.
- **Webhooks Integration Hub:** Outbox-based webhook delivery with configurable subscriptions and retry logic.
- **Inbound Webhooks Integration:** Receiver for inbound webhooks with signature verification and background processing.
- **Integration Endpoints Management:** Admin UI for managing integration endpoints and health metrics.
- **Integration ID Mapping:** Admin UI for mapping external system IDs to internal entities.
- **PM Execution Loop:** Preventive maintenance scheduler for auto-generating work orders.
- **PM Template Revision Control:** In-app revision control for PM Templates.
- **Item Revision Management:** Comprehensive system for managing item revisions.
- **Smart Mode Architecture:** System adapts behavior based on Single/Multi-Company mode.
- **Lookup Table Infrastructure:** Database-driven reference data system with admin UI, caching, and audit logging.
- **Enterprise Detail API:** Unified API for fetching detailed entity information across various types.
- **Machine Specifications:** `MachineSpecification` entity (one-to-one with Asset) for CNC machine technical data, integrated into Asset detail page with view/edit modes and unit conversions.
- **Data Management:** Unified `/Admin/DataManagement` page consolidating Import Wizard + Export into a tabbed UI, supporting 15 entity types in FK-dependency phases with premium branded static .xlsx templates and server-side validation.
- **Technician Enrichment:** Enriched `Technician` model with detailed employee, skill, and certification information, accessible via dedicated profile pages and an index with advanced filtering.
- **Fixed Assets Production Hardening:** Implemented production readiness features including robust report scoping, advanced depreciation engine logic, period locking via `IPeriodGuard`, flexible GL account lookups, strict validation for asset fields, unique indexing, and an idempotent `DepreciationBackfillService` with an Admin UI for recomputing historical depreciation.
- **Test Coverage:** Playwright test suite organized into 7 registered validation commands (`auth`, `nav`, `smoke`, `ui`, `flows`, `fa`, `reports`) covering ~30 specs end-to-end. The `smoke` suite includes `tests/smoke_health_probes.spec.js` which locks in the Phase 1 contract: `/healthz` and `/readyz` are reachable anonymously (no cookie, no tenant header) — proves no middleware regressed the exemptions; `/readyz` returns the JSON envelope with both `db` and `skia` checks Healthy in dev; and the request-id middleware always emits `X-Request-Id` (echoing inbound or falling back to `TraceIdentifier`). 145 smoke tests total. Every orphan spec is wired into a suite (no untracked tests). The `ui` suite includes `barcode_ui.spec.js` which exercises `/Admin/Barcodes` and asserts real PNGs (200 + `image/png` + magic bytes + decoded width/height) from `/api/barcode/generate/{itemId}` and `/api/barcode/label/{itemId}` — no 503-as-success backstops. All suites run with `workers=1` and a per-test timeout of 120s. `tests/_globalSetup.js` (wired via `playwright.config.js → globalSetup`) waits up to 90s for the Web Server, then warms a representative set of Razor pages (`/Account/Login`, `/`, `/Assets`, `/Assets/Locations`, `/Admin`, `/Admin/Barcodes`, `/Reports`, `/Inventory`) once per suite invocation. This eliminates first-nav stalls when the platform's review-time validator fans out all 7 suites in parallel against one shared dotnet process. Live verification on production (`https://f-ixed-assets-project-1-25-26-750-pm.replit.app`) confirms `/api/barcode/generate/9243` returns a 1728-byte 300×100 RGBA PNG and `/api/barcode/label/9243` returns a 6185-byte 400×200 RGBA PNG — proof that SkiaSharp's native lib loads under autoscale.
- **Production Observability (Phase 1):** Health and readiness endpoints expose process state to the autoscale router and to operators. `GET /healthz` (anonymous, ~10ms) is liveness — runs no checks, returns 200 `Healthy` whenever the process is responsive. `GET /readyz` (anonymous, ~20ms) is readiness — runs the checks tagged `ready` and returns a JSON envelope with per-check status, duration, description, and exception message. Two checks are wired: `db` (`AppDbContext.Database.CanConnectAsync`) and `skia` (on-disk probe for `libSkiaSharp.so` — never instantiates a SkiaSharp type, mirroring `BarcodeService`'s pattern, so a missing native lib reports Degraded instead of crashing the process). `Middleware/RequestIdMiddleware.cs` reads inbound `X-Request-Id` (or falls back to `HttpContext.TraceIdentifier`), echoes it on the response, and pushes `RequestId` / `RequestPath` / `RequestMethod` into the `ILogger` scope so every log line emitted during the request includes them. `appsettings.Production.json` configures the built-in `Microsoft.Extensions.Logging.Console` JSON formatter (single-line, UTC ISO-8601 timestamps, scopes included) so production stdout is structured JSON consumable by `fetch_deployment_logs` and any downstream log aggregator. Zero new NuGet packages — all built into the ASP.NET Core Web SDK.
- **Concurrency Hardening (Task #13):** Three changes keep the dotnet process stable under parallel test load (and concurrent production users): (1) `QuerySplittingBehavior.SplitQuery` is configured globally on `AddDbContext` so any multi-`Include` query auto-splits instead of generating a Cartesian-product join — individual queries can opt back into single-query via `.AsSingleQuery()` when a join is genuinely cheaper; (2) the Npgsql connection string sets `Maximum Pool Size=200;Timeout=30;Command Timeout=60` so the per-request pool can absorb bursts and slow queries can't pin a connection forever; (3) `TenantContextMiddleware.ResolveSingleTenantAsync` memoizes the resolved `(tenantId, companyId)` pair in a static cache (semaphore-guarded, only cached after a fully successful resolution) so single-tenant requests no longer hit the DB twice on every call.

## External Dependencies
- **PostgreSQL:** Primary database.
- **Entity Framework Core:** ORM for database interaction.
- **OpenAI Integration:** Used for the AI Assistant feature.
- **ClosedXML:** For generating Excel report exports.
- **QuestPDF:** For generating PDF report exports.
- **Tom Select (CDN v2.3.1):** Searchable typeahead for `<select>` elements.
- **SkiaSharp + ZXing.Net.Bindings.SkiaSharp:** Barcode and label PNG rendering. The Linux native library is supplied by `SkiaSharp.NativeAssets.Linux.NoDependencies` (statically linked, no extra Nix packages required). `BarcodeService` probes for `libSkiaSharp.so` on disk before touching any SkiaSharp type and returns HTTP 503 from `BarcodeApiController` if it is missing — never let a SkiaSharp object be partially constructed or its GC finalizer will crash the process.