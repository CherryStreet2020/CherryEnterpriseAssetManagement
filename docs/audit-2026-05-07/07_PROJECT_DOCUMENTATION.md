# CherryAI Enterprise Asset Management — Complete Project Audit
**Synthesized Handoff Documentation**  
**Date:** February 24, 2026  
**Project:** CherryAI EAM (Abs.FixedAssets)  
**Stack:** ASP.NET Core 9.0, PostgreSQL, Razor Pages

---

## 1. PROJECT NARRATIVE

**CherryAI Enterprise Asset Management** is a comprehensive fixed asset lifecycle and maintenance management system designed for manufacturing organizations with multiple operational sites. It operates as both a **standalone financial system** and **ERP integration hub**, supporting GAAP & tax depreciation compliance across US and Canadian jurisdictions.

### Product Tiers (3-Tier Productization)

| Tier | Name | Target Audience | Key Differentiator |
|------|------|-----------------|-------------------|
| 1 | **Launchpad** | Small businesses, simple asset tracking | Basic asset lifecycle, manual scheduling |
| 2 | **Autopilot** | Mid-market, compliance automation | PM scheduling, cost automation, tax reporting |
| 3 | **Command Center** | Enterprise multi-site operations | Multi-company/site, advanced analytics, AI assistant |

### Core Value Proposition
- **GAAP & Tax Book Depreciation**: Supports 22+ depreciation methods (SL, DB, SYD, MACRS-3 through MACRS-39, Canadian CCA classes)
- **Multi-Book Architecture**: GAAP, Federal Tax, State Tax, AMT, ACE, CCA books per asset
- **Preventive Maintenance Automation**: PMSchedule-driven work order generation with compliance tracking
- **Capital Improvement Projects**: CIP tracking with cost aggregation and capitalization
- **Integrated Procurement**: PO, requisition, goods receipt workflows with vendor management
- **Inventory & Materials**: Item master with revision control, vendor part mappings, cross-references, approved vendor lists
- **Multi-Company/Site Support**: Tenant-scoped with hierarchy: Tenant → Company → Site → Location → Asset
- **AI Assistant**: OpenAI integration for natural language asset queries

### Positioning
Designed to replace spreadsheet-based asset tracking and manual depreciation calculations. Replaces specialized EAM tooling (Maximo, eMaint) for organizations that want a cloud-native alternative with financial compliance built-in.

---

## 2. ARCHITECTURE SUMMARY

### Layered Architecture

```
Presentation Layer (Razor Pages UI + REST API)
        ↓
Application Services (Domain logic, business rules)
        ↓
Domain Entities & Value Objects (Asset, Book, WorkOrder, etc.)
        ↓
Infrastructure (EF Core, PostgreSQL, Caching, Webhooks)
```

### Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Frontend** | Razor Pages + Vanilla JS | .NET 9.0 |
| **Backend** | ASP.NET Core | 9.0 |
| **ORM** | Entity Framework Core | 9.0 |
| **Database** | PostgreSQL (Neon-backed) | 14+ |
| **CSS** | Custom design system + Bootstrap 5 | Tokens-based |
| **Reports** | ClosedXML (Excel), QuestPDF (PDF) | Latest |
| **Barcodes** | ZXing.Net.Bindings.SkiaSharp | Latest |
| **AI** | OpenAI API (chat assistant) | GPT-4 |
| **Hosting** | Replit (primary), Docker-ready | Port 5000 |

### Key Modules

| Module | Responsibility | Key Entities |
|--------|---------------|--------------|
| **Asset Management** | Lifecycle (register, transfer, dispose, improve) | Asset, AssetTransfer, PartialDisposal |
| **Depreciation** | Multi-book GAAP/tax calculations | DepreciationBook, DepreciationSchedule |
| **Maintenance** | Work orders, PM scheduling, compliance | WorkOrder, PMSchedule, PMOccurrence |
| **Materials** | Item master, inventory, procurement specs | Item, ItemRevision, ItemSupersession |
| **Financials** | Chart of accounts, fiscal periods, journals | GlAccount, FiscalPeriod, Journal |
| **Purchasing** | POs, requisitions, receiving | PurchaseOrder, PurchaseRequisition, GoodsReceipt |
| **Integrations** | Webhooks (inbound/outbound) | WebhookSubscription, WebhookOutbox, InboundEvent |
| **Reference Data** | Lookup tables (enums as DB rows) | LookupType, LookupValue |

### Deployment Modes (Dual-Mode)

**Single-Tenant (On-Premise)**: One organization per deployment, fixed TenantId, simpler config.  
**Multi-Tenant (SaaS)**: Shared infrastructure, X-Tenant-ID header resolution, strict data isolation.

---

## 3. ARCHITECTURE DECISION RECORDS (ADRs)

All ADRs in `/docs/adr/` follow the RFC template. **10 decisions documented:**

| ADR | Title | Status | Outcome |
|-----|-------|--------|---------|
| **ADR-001** | PMSchedule is canonical model for PM execution | Accepted | PMSchedule (not MaintenanceSchedule) is the single source of truth; used for all PM calculations, KPIs, UI display |
| **ADR-002** | DemoPackV2 is canonical seed entry | Accepted | Defined seed data (5+ assets, 3+ WOs, 2+ PM schedules, 10+ items) used as baseline for all smoke tests |
| **ADR-003** | Smoke tests run inside transaction with rollback | Accepted | All tests execute in single DB transaction, always rolled back; zero test data pollution guaranteed |
| **ADR-004** | UI Hygiene: No inline styles | Accepted | All styles must be in CSS classes; no `<style>` tags or inline attributes in pages |
| **ADR-005** | DataGrid Premium contract | Accepted | Premium DataGrid component must support 21 detail card types with >=12 headers, >=3 sections per type |
| **ADR-006** | Return URL security hardening | Accepted | All drill-down links pass `returnUrl` query param; detail pages validate & render back link; prevents open redirects |
| **ADR-007** | Unified tab system across all screens | Accepted | Tab navigation consolidated; single `.tab-nav` component; enforced by smoke tests |
| **ADR-008** | Unified screen header system | Accepted | Hero header pattern mandatory: breadcrumbs, title, subtitle, primary action; applied to 30+ pages |
| **ADR-010** | Design tokens | Accepted | Color, spacing, shadow, border radius, transition tokens in `tokens.css`; all new styles reference tokens, not hardcoded values |

**Key architectural patterns:**
- **Outbox Pattern**: Reliable webhook delivery via outbox table + background dispatcher
- **Query Filters**: Global EF Core query filters enforce tenant/company scoping at DB level
- **FK-Bound Dropdowns**: Legacy enum fields + new FK columns to LookupValue allow form flexibility while maintaining backward compatibility
- **Idempotent Seeding**: Natural-key-based upserts (check-before-insert); transaction-wrapped with guard checks at execution time

---

## 4. DECISION LOG HIGHLIGHTS (DECISION_LOG.md)

**Biggest decisions made (with dates & rationale):**

| Date | Decision | Rationale |
|------|----------|-----------|
| **2026-01-24** | Return Path / Back Navigation Standard adopted | Users trapped on detail pages; all drill-downs now pass returnUrl with validation; back link rendered on all detail pages |
| **2026-01-24** | Navigation audit + smoke test enforcement | Sidebar links & asp-page targets were drifting; RouteRegistry.md now canonical source; smoke tests verify no broken links |
| **2026-01-24** | Schema/UI drift prevention policy | UI fields not backed by DB columns; every binding must map to persisted property or computed allowlist |
| **2026-01-24** | Work order tenant scoping rule | Details page uses company-scoped queries; prevents cross-tenant data access via ID guessing |
| **2026-01-22** | Product naming standard | Full: "CherryAI Enterprise Asset Management"; UI shorthand: "CherryAI EAM"; page title format: "CherryAI EAM — <PageName>" |
| **2026-01-22** | Tier naming: Launchpad / Autopilot / Command Center | Conveys progression: getting started → automation → enterprise scale |
| **2026-01-22** | LAB/DEMO environment safety policy | APP_ENVIRONMENT env var gates seeding; visual banner on every page; prevents accidental demo/prod data loss |
| **2026-01-22** | Seed pack architecture (3 presets) | Small (25 assets), Mid-Size (100), Enterprise (321); idempotent upserts; transaction-wrapped |
| **2026-01-22** | PM Templates UX refactor | Converted modal-based to full-page create/edit; eliminated modal layering bugs; consistent with Unified Asset Page |
| **2026-01-22** | Tenant/Company/Site control plane | Multi-tenant architecture with deployment mode toggle; single codebase serves both deployment models |
| **2026-01-22** | Work order origin classification | Deterministic heuristic identifies origin (Smart Assist vs PM Schedule vs Manual); origin badges on list & detail pages |
| **2026-01-22** | Item master cross-reference v1 | Three-way part number resolution: Internal PN → MPN → VPN; optional vendor filter; exact match only in v1 |
| **2026-01-22** | Item revision control pattern | Reuses RevisionStatus enum (Draft/Released/Obsolete); auto-generated revision codes (A, B, C...); supersession chain |
| **2026-01-22** | Approved vendor list (AVL) architecture | ItemApprovedVendor entity; ApprovalStatus (Approved/Conditional/Blocked); single preferred vendor per item |

---

## 5. RELEASE & DEPLOYMENT FLOW

### Build Pipeline

**Development**: `dotnet build`  
**Production**: `dotnet publish -c Release -o ./publish`

### Environment Configuration

| Variable | Required | Purpose |
|----------|----------|---------|
| `DATABASE_URL` | Yes | PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | Yes | Development, Staging, Production |
| `ASPNETCORE_URLS` | Yes | Binding URLs (e.g., http://0.0.0.0:5000) |
| `AI_INTEGRATIONS_OPENAI_API_KEY` | Optional | OpenAI API key |
| `APP_ENVIRONMENT` | Optional | LAB, DEMO, PROD (overrides auto-detection) |

### Deployment Targets

1. **Replit** (Primary): `dotnet run --project Abs.FixedAssets.csproj` on port 5000
2. **Docker**: Containerized deployment for on-premise
3. **VM/IaaS**: Traditional server for enterprise

### Release Checklist (from RELEASE_CHECKLIST.md)

1. **Pre-release verification**: All smoke tests pass
2. **Database migration**: EF Core migrations applied to target schema
3. **Data seeding**: DemoPackV2 loaded for demo/staging environments
4. **Configuration validation**: All required env vars set, no defaults in config
5. **Deployment**: Artifact published, health checks passing
6. **Post-deployment smoke tests**: Full test suite run against live environment

### Rollback Playbook (ROLLBACK_PLAYBOOK.md)

1. **Database rollback**: Use EF Core migration reversal (`dotnet ef database update <previous-migration>`)
2. **Application rollback**: Redeploy previous release build
3. **Seed data recovery**: Re-run DemoPackV2 seeding if reference data corrupted
4. **Verification**: Run full smoke test suite against rolled-back environment

---

## 6. TESTING & QUALITY POSTURE

### Smoke Test Architecture (ADR-003)

All 12+ smoke tests run inside **single transaction with guaranteed rollback** — zero test data pollution.

**Test Categories:**

| Test # | Name | Coverage | Status |
|--------|------|----------|--------|
| 1 | Auth smoke | Login, password reset, session | ACTIVE |
| 2 | Navigation IA drift | Sidebar links + asp-page targets resolve | ACTIVE |
| 3 | Breadcrumbs / back link | Users can return from detail pages | ACTIVE |
| 4 | API headers & base URL | X-Tenant-Id, X-Org-Node-Id headers present | ACTIVE |
| 5 | SQL schema scan | No hardcoded `data.` schema references | ACTIVE |
| 6 | Forbidden strings & ports | No hardcoded ports (5432, 3306, etc.) | ACTIVE |
| 7 | DemoPackV2 base state | DemoPackV2 seeding reproducible | ACTIVE |
| 8 | Detail card completeness (21 types) | 21 detail endpoints return >=12 headers, >=3 sections | ACTIVE |
| 9 | Drilldown semantic completeness | List pages return identifiers + valid detail refs | ACTIVE |
| 10 | Org selector & scope UI | Org tree >10 nodes with all 4 hierarchy types | ACTIVE |
| 11 | Work order origin classification | Smart Assist/PM/Manual origins deterministic | ACTIVE |
| 12 | Empty states render safely | WO with zero operations/parts/labor loads correctly | ACTIVE |

### Quality Gates (in GATES/ directory)

- **00_AUTH_SMOKE.md**: Login/password/session tests
- **01_API_HEADERS_AND_BASEURL.md**: Header validation (X-Tenant-Id, X-Org-Node-Id)
- **02_NAV_NO_IA_DRIFT.md**: Navigation correctness (58 sidebar links, 65 asp-page targets)
- **03_BREADCRUMBS_BACK_TO_RESULTS.md**: Return path standard
- **05_FORBIDDEN_STRINGS_AND_PORTS_SCAN.md**: No hardcoded secrets/ports
- **06_SQL_SCHEMA_SCAN.md**: No forbidden schema references

### Data Volume Requirements (Proof Bundle)

| Entity | Minimum | Actual |
|--------|---------|--------|
| PurchaseOrders | 250 | 250 |
| CustomerInvoices | 250 | 425 |
| Assets | 1+ | 321 |
| OrgNodes | 10+ | 53 |
| CipProjects | 1+ | 5 |
| FiscalYears | 1+ | 3 |
| FiscalPeriods | 1+ | 36 |

### Proof Bundle Rebuild (from RUNBOOK_QUALITY.md)

All verification commands use port 5000 with headers: `X-Tenant-Id: default`, `X-User-Id: system@localhost`, `X-Org-Node-Id: <uuid>`.

```bash
python3 scripts/gate_detail_card_completeness.py
python3 scripts/gate_drilldown_semantic_completeness.py
python3 scripts/gate_org_selector_and_scope_ui.py
python3 scripts/gate_surface_crawl.py
```

Results: `artifacts/21_api_endpoints.json`, `proof/` directory bundles with timestamps.

---

## 7. TENANCY & SECURITY MODEL

### Multi-Tenant Data Isolation

**Isolation Layers:**

| Layer | Mechanism |
|-------|-----------|
| **Database** | EF Core global query filters by CompanyId |
| **Application** | TenantContext service (DI) provides scoped tenant/company/site |
| **API** | Header middleware validates X-Tenant-ID (required in MultiTenant mode) |
| **UI** | Company selector dropdown filters data within tenant |

### Tenant Resolution (Priority)

1. `X-Tenant-ID` header (API calls)
2. Subdomain (if configured)
3. User's default tenant (from identity claims)
4. Default tenant (single-tenant mode fallback)

### Authentication

- **Scheme**: Cookie-based (`.AspNetCore.Identity.Application`)
- **Expiration**: Sliding, 14 days
- **Secure flags**: HttpOnly, SameSite=Lax, HTTPS-only
- **Identity provider**: ASP.NET Core Identity (customizable for LDAP/SAML)

### Authorization (RBAC)

Standard roles: Admin, Accountant, Technician, ReadOnly. Role-based page/API access enforced via `[Authorize(Roles="...")]` attributes.

### Key Security Decisions

- **Return URL validation** (ADR-006): No external URLs, no protocol-relative URLs, no path traversal, no XSS vectors; allowlist against known routes; fallback to canonical module route
- **Tenant-scoped queries**: Work order details page uses company-scoped `GetScopedEventAsync(id)` to prevent cross-tenant access via ID enumeration
- **Uniform 404 responses**: Returns NotFound() uniformly whether record doesn't exist OR belongs to another company (prevents ID existence leakage)

---

## 8. FINANCIAL & DEPRECIATION ENGINE

### Multi-Book Architecture

Six book types supported per asset:

| Book | Compliance | Usage |
|------|-----------|-------|
| **GAAP** | US GAAP / IFRS | Financial reporting |
| **Federal Tax** | IRS regulations | US federal tax return |
| **State Tax** | State-specific rules | State tax filing |
| **AMT** | IRS Alternative Minimum Tax | AMT calculations |
| **ACE** | Adjusted Current Earnings | Corporate AMT |
| **CCA** | Canadian Capital Cost Allowance | Canadian tax |

### Depreciation Methods (22 Supported)

**GAAP Methods:** SL, DB-150, DB-200, SYD, UNITS  
**US Tax (MACRS):** MACRS-3, MACRS-5, MACRS-7, MACRS-10, MACRS-15, MACRS-20, MACRS-27.5, MACRS-39  
**Canadian CCA:** CCA-8, CCA-10, CCA-12, CCA-43, CCA-50

**Depreciation Conventions:**

| Convention | Description | Application |
|------------|-------------|-------------|
| **HY** | Half-Year | Standard half-year convention |
| **MQ** | Mid-Quarter | If >40% Q4 acquisitions (triggers mid-quarter test) |
| **MM** | Mid-Month | Based on month placed in service |
| **FM** | Full-Month | Full month of acquisition |
| **NONE** | No convention | Full annual depreciation |

### Key Calculations

- **Current Year Depreciation**: Based on method, cost, useful life, placed-in-service date
- **Accumulated Depreciation**: Sum of all depreciation through current period
- **Book Value**: Original cost - accumulated depreciation
- **Salvage Value**: Residual value at end of useful life (affects declining balance methods)

### CCA Seeding

Canadian CCA class seeder (`CcaClassSeeder.cs`) auto-populates CCA class lookup tables with official rates and descriptions.

---

## 9. MATERIALS, WORK EXECUTION & MASTER DATA FLOWS

### Materials (Item Master)

**Item Structure:**
- Item (base record with code, description, type, status, cost method, tracking)
- ItemRevision (versioned revisions with status: Draft/Released/Obsolete)
- ItemSupersession (tracks part replacement chains)
- ItemManufacturerPart (links items to manufacturer part numbers)
- VendorItemPart (vendor-specific part numbers)
- ItemApprovedVendor (approved vendor list with approval status)
- ItemAlternate (substitute/equivalent parts with ranking)

**Key Features:**
- **Three-way part number resolution** (ADR-022): Internal PN → MPN → VPN; exact match only
- **Revision control** (ADR-021): Auto-generated codes (A, B, C...); released revisions immutable
- **Supersession chains** (ADR-024): Tracks part replacement; cycle prevention via graph walk
- **Approved vendor lists** (ADR-023): Track approval status; single preferred vendor enforced by service

### Work Execution (Work Orders & Preventive Maintenance)

**PMSchedule (Canonical Model per ADR-001)**
- Template → Revision → Schedule → Occurrence tracking
- Fully tenant-scoped with Company/Site assignment
- Execution loop (`PMExecutionHostedService`) generates work orders on schedule
- PM compliance KPIs calculated from PMOccurrence data

**Work Order Origin Classification (ADR-008)**
- **Smart Assist**: WorkRequest with GeneratedWorkOrderId + IsAIAssisted=true
- **PM Schedule**: CustomField1 starts with "PMTA:" OR Type=Preventative with RecurrenceIntervalDays
- **Manual**: All others
- Origin badges appear on WO list & detail pages; deterministic classification via `WorkOrderOriginService`

**WorkOrder Lifecycle:**
1. Create (manual, PM auto-generated, or Smart Assist)
2. Dispatch (assign technician)
3. Execute (update status, log labor, parts consumed)
4. Complete (closeout, cost capitalization)
5. Archive

**Related Entities:** Operations (labor, parts, tools), Attachments (photos, docs), AssetMaintenance event tracking

### Master Data Bootstrap (MasterDataBootstrap.md)

**8-Pipeline Seeding Architecture:**

| Order | Pipeline | Purpose | Output |
|-------|----------|---------|--------|
| 1 | SystemReferencePackage | Depreciation methods, conventions, jurisdictions | 76 lookup types |
| 2 | OrganizationPackage | Companies, sites, locations, departments | Org hierarchy |
| 3 | FinancePackage | Chart of accounts, fiscal years, periods | Finance structure |
| 4 | VendorPackage | Vendors, vendor part numbers | Supply base |
| 5 | PartsPackage | Items, categories, revisions | Item master |
| 6 | EAMExecutionPackage | PM templates, schedules | PM automation |
| 7 | DemoPackV2 | Demo assets, work orders, sample data | Ready-to-demo state |
| 8 | LookupSeedFileLoader | Auto-discover & load JSON lookup files | Extensible reference data |

Each pipeline is idempotent (check-before-insert via natural keys) and transaction-wrapped.

### Reference Data (LookupTypes & LookupValues)

**76 Lookup JSON files** in `seed/reference-data/`:
- AssetType, AssetStatus, AssetCondition, AssetPriority
- DepreciationMethod, DepreciationConvention, DepreciationFrequency
- BookType, TaxJurisdiction
- MaintenanceType, MaintenanceStatus, MaintenancePriority
- POStatus, PurchaseOrderType, RequisitionStatus
- ItemType, ItemStatus, CostMethod, TrackingType
- CipProjectStatus, CipCostType
- WorkOrderType, WorkOrderStatus, WorkOrderPriority
- And 40+ more

**Schema:**
```sql
LookupTypes (Id, TenantId, CompanyId, Key, Name, IsSystem, IsActive)
LookupValues (Id, LookupTypeId, Code, Name, SortOrder, IsActive, Metadata)
```

**FK-Bound Dropdown Pattern (Backward Compatibility):**
All domain models have:
- **Legacy enum field** (e.g., `AssetType Type`) for backward compatibility
- **New FK column** (e.g., `int? AssetTypeLookupValueId`) pointing to LookupValue.Id

Forms bind to FK columns; on save, code resolves from LookupValue to sync enum. Allows flexible form UI without breaking existing code.

---

## 10. INTEGRATION MODEL

### Outbox Pattern (Reliable Webhooks)

```
Domain Event → Outbox Table → Background Dispatcher → External System
                ↓
            Retry Logic (exponential backoff)
                ↓
            DLQ (Dead Letter Queue) on 5 failures
```

**Components:**
- **WebhookSubscription**: Defines endpoint URL and event type filter
- **WebhookOutbox**: Stores pending events (transactional write with domain event)
- **WebhookDispatcherHostedService**: Background service polling outbox, delivering via HTTPS POST
- **WebhookDelivery**: Audit trail of delivery attempts (timestamp, status, response body)

### Supported Event Types

| Event | Trigger | Payload |
|-------|---------|---------|
| `asset.created` | Asset registration | Full asset object |
| `asset.updated` | Asset modification | Changed fields delta |
| `asset.disposed` | Asset disposal | Disposal details + reason |
| `workorder.created` | WO creation | Full work order |
| `workorder.status.updated` | Status change (Open → In Progress, etc.) | Old/new status + changed fields |
| `workorder.closed` | WO completion | Summary + total labor hours, parts cost |
| `depreciation.calculated` | Period close | Depreciation amounts by book |

### Inbound Webhook Processing

```
External POST → InboundEventReceiver → Validation → EventQueue → EventProcessor
                    ↓
              Signature verification (HMAC-SHA256)
              Idempotency key check (prevent duplicates)
              Tenant-scoped processing
```

**Idempotency:** All inbound events have `IdempotencyKey` (tenant-scoped). Duplicate POST with same key returns cached response without re-processing.

### Integration Mappings (Admin UI)

Users configure via `/Admin/Integrations/Maps`:
- Map incoming webhook fields to local entity fields
- Define transformation rules (value mapping, calculation)
- Test payload validation

---

## 11. SEEDING MODEL

### LAB vs DEMO Environment Strategy

| Environment | Purpose | Seeding | Data State | Use Case |
|-------------|---------|---------|-----------|----------|
| **LAB** | Development sandbox | Yes, with safeguards | Mutable | Local dev, experimentation |
| **DEMO** | Protected demonstration | No (read-only) | Fixed | Customer demos, training |
| **PROD** | Production | Locked | Live customer data | Live environment |

**Environment Detection (Priority):**
1. `APP_ENVIRONMENT` env var (LAB, DEMO, PROD)
2. Database name pattern detection (contains "lab" / "demo" / "prod")
3. Fallback to `ASPNETCORE_ENVIRONMENT` (Development → LAB, Production → PROD)

**Seeding Guards:**
- `ASPNETCORE_ENVIRONMENT` must be "Development" for any seeding
- `ALLOW_DEMO_SEED=true` override required to seed DEMO/PROD (emergency refresh only)
- Visual banner on every page shows current environment
- Footer displays masked database host/name for verification

### DemoPackV2 (Canonical Seed, per ADR-002)

**Baseload data (all smoke tests start with DemoPackV2):**

| Entity | Count | Examples |
|--------|-------|----------|
| Assets | 5+ | CNC Lathe (ASSET-001), Press Brake (ASSET-002) |
| Work Orders | 3+ | WO-2026-0001 (Open), WO-2026-0002 (In Progress) |
| PM Schedules | 2+ | Monthly Lubrication, Quarterly Inspection |
| Items | 10+ | Ball Bearing 6205-2RS, Motors, Filters |
| Vendors | 3+ | Grainger, MSC, Fastenal |
| Org Nodes | 10+ | Holding Co, subsidiaries, sites |
| GL Accounts | 50+ | Asset, Depreciation, Maintenance expense |
| Fiscal Periods | 12+ | 2026 monthly + quarterly periods |

**Idempotent upserts:** All seeding uses natural keys (asset number, WO number, part number) to check-before-insert, allowing safe re-runs.

### Seeding Pipelines (8 Total)

1. **SystemReferencePackage**: 76 lookup types auto-loaded from JSON
2. **OrganizationPackage**: Org hierarchy (holding → company → site → location)
3. **FinancePackage**: COA, fiscal years, periods, exchange rates
4. **VendorPackage**: Vendors with contact info and part catalogs
5. **PartsPackage**: Item master with revisions and cross-references
6. **EAMExecutionPackage**: PM templates with schedules and occurrences
7. **DemoPackV2**: Demo assets, work orders, sample maintenance records
8. **LookupSeedFileLoader**: Auto-discover JSON files in seed/reference-data/

Each pipeline is guarded by environment checks and transaction-wrapped.

---

## 12. BRAND GUARDRAILS & UX STANDARDS

### Design System (Tokens-Based, BrandGuardrails.md)

**Color Palette:**

| Token | Value | Usage |
|-------|-------|-------|
| `--primary` | #2e4a7d | Primary buttons, links, accents |
| `--success` | #22c55e | Success states, positive actions |
| `--warning` | #f59e0b | Warning states, pending items |
| `--danger` | #ef4444 | Error states, destructive actions |
| `--bg-primary` | #f1f5f9 | Page background |
| `--bg-secondary` | #ffffff | Cards, modals |

**Typography:**
- Font: Inter (Google Fonts), fallback: system sans-serif
- H1: 1.75rem/700 (page title)
- H2: 1.25rem/600 (section title)
- Body: 14px/400
- Label: 0.875rem/500

### Mandatory Patterns

**Hero Header** (all pages):
- Breadcrumbs above
- Page title (H1) + optional subtitle
- Primary action button (right-aligned)
- Example: `/Assets/Asset.cshtml` hero shows "Assets", subtitle "Manage your fixed assets", Create button

**Section Cards** (content containers):
- `.section-card` wrapper with white bg, 1rem border radius, card shadow
- Header with title + optional action
- Content area with proper spacing
- DO NOT use: translucent glass morphism, modal-style chrome on full pages

**Empty States** (all list pages):
- Icon (3rem, muted color)
- Title (h3, primary color)
- Description (body, secondary color)
- CTA button (primary action)

**Navigation:**
- Sidebar (8 sections: Assets, Maintenance, Materials, Purchasing, Accounting, Finance, Reports, Admin)
- Active state: path-based detection (e.g., `/Materials/*` matches active)
- Breadcrumbs on every page (except home)

**Buttons:**
- Primary: gradient bg (#2e4a7d → #1e3a5f), white text, md radius, md shadow
- Secondary: white bg, border, primary text
- Danger: red bg, white text
- Sizes: sm (0.375rem/0.75rem), default (0.625rem/1.25rem), lg (0.75rem/1.5rem)

### Anti-Patterns (Forbidden)

- ❌ Inline styles (`<style>` tags, `style=""` attributes)
- ❌ Translucent glass morphism cards
- ❌ Custom fonts (only Inter)
- ❌ Magic numbers (use CSS variables)
- ❌ Nested card nesting (max 1 level)
- ❌ Inline `!important`
- ❌ Z-index wars (use defined layers)

### Design Tokens File (wwwroot/css/tokens.css)

All 9+ families defined as CSS custom properties:
- Color tokens (primary, success, warning, danger, bg, text, border)
- Spacing tokens (0.5rem, 1rem, 1.5rem, 2rem)
- Shadow tokens (sm, md, lg, card)
- Border radius tokens (sm, md, lg, xl, 2xl)
- Transition tokens (fast, normal, bounce)

---

## 13. KNOWN ISSUES, OPEN WORK & TECHNICAL DEBT

### From HANDOFF_STATUS.md — FK-Bound Migration

**Status:** 16 pages fully migrated, 3 pages partially done.

**Fully Migrated (FK-bound dropdowns + enum sync):**
- Asset Edit/Create (5 FK fields)
- Book Edit (5 FK fields)
- Site Admin (2 FK fields)
- CIP Details (2 FK fields)
- Maintenance Details (3 FK fields)
- PO Index/Create (2 FK fields)
- Requisitions (2 FK fields)
- AP Invoice Details (1 FK field)
- Item Edit (4+1 FK fields)
- Locations (1 FK field)
- Departments (1 FK field)
- Cost Centers (1 FK field)
- GL Accounts (1 FK field)
- Work Order Details (2 FK fields)
- Asset Dispose (1 FK field)
- Asset Transfer (1 FK field)

**NOT YET MIGRATED (still enum-only):**
- **Purchasing/Details.cshtml.cs**: `OnPostUpdateHeaderAsync` casts POType; status workflow buttons (Submit, Approve) don't sync FK; `OnPostDuplicatePOAsync` needs FK sync
- **Admin/WorkOrders.cshtml.cs**: Read-only list page; enum comparisons for KPI display only (low priority)
- **Receiving/Index.cshtml.cs**: Read-only list page; enum array filters (low priority)

**Gap:** PO Details is the main gap. All 39 FK columns present in DB, ready to bind.

### From RUNBOOK_QUALITY.md — Proof Bundle

**Current data volume:**
- 250 POs (minimum met)
- 425 customer invoices (minimum 250)
- 321 assets (minimum 1)
- 53 org nodes (minimum 10)

**Quality gates all passing:**
- 21 detail card types verified
- Navigation audit: 58 sidebar links, 65 asp-page targets, 403 hrefs (all valid)
- Surface crawl: 27 API + 12 page endpoints reachable
- Port & schema scan: no hardcoded 5432, 3306, or data.* schema refs
- Drilldown semantic completeness: first 200 rows populate identifiers

### Documentation Health

**Coverage:** Strong in core areas (Architecture, TenancyAndSecurity, FinancialsAndDepreciation, Integrations, SeedingAndDemoData, BrandGuardrails).

**Gaps:**
- Some ADR outcomes not yet reflected in main service implementations (e.g., ADR-010 Design Tokens)
- ItemSupersession cycle prevention logic documented but not fully tested in smoke suite
- CCA Canadian seeding needs verification audit
- "What's next" sections in HANDOFF_STATUS show ongoing work

### Technical Debt & Future Work

From AUDIT_BUNDLE_JAN2026.md and decision logs:

1. **MaintenanceSchedule deprecation**: Legacy model needs migration to PMSchedule canonical
2. **PO Details FK migration**: Complete enum → FK binding for POType and status fields
3. **ItemAlternate ranking tie-break**: Need deterministic ordering for GetBestAlternate
4. **Cross-reference performance**: Large VendorItemPart lookups may need indexing
5. **Cycle detection optimization**: ItemSupersession cycle prevention could cache graph
6. **Test data volume growth**: Smoke test dataset growing; may need pagination verification
7. **CSP inline migration**: CSP_INLINE_MIGRATION_PLAN.md outlines long-term style hardening
8. **UI conformance**: UI-Conformance-* audit reports flag some legacy patterns still in use

---

## 14. CRITICAL CONVENTIONS & GOTCHAS

### Multi-Tenant Scoping (Key Pattern)

**Every entity query must use company scoping:**

```csharp
var items = _db.Assets
    .Where(a => a.CompanyId == _tenantContext.CompanyId)  // MANDATORY
    .ToListAsync();
```

**Query filter at DbContext level:**

```csharp
modelBuilder.Entity<Asset>()
    .HasQueryFilter(a => a.CompanyId == _tenantContext.CompanyId);
```

**Uniform NotFound response:** If record doesn't exist OR belongs to another company, return `NotFound()` (prevents ID enumeration).

### Lookup Service (Reference Data)

**Always use `ILookupService` for enums:**

```csharp
// Get dropdown list
var options = await _lookupService.GetSelectListByIdAsync(
    _tenantContext.TenantId, _tenantContext.CompanyId,
    "AssetType", asset?.AssetTypeLookupValueId, "");

// Resolve code from ID
var lv = await _lookupService.GetValueByIdAsync(null, null, id);
if (lv != null && Enum.TryParse<AssetType>(lv.Code, true, out var parsed))
    asset.Type = parsed;  // Sync enum
```

**Cache invalidation:** Call `_lookupService.InvalidateCache()` after creating/editing lookup values.

### Return URL Security

**All drill-down links must pass returnUrl:**

```csharp
// In list page:
<a asp-page="/Assets/Asset" asp-route-id="@item.Id" 
   asp-route-returnUrl="@ReturnUrlHelper.EncodeReturnUrl(Context.Request.Path)">
    Edit
</a>

// In detail page:
@{
    var model = new { ... };
    if (!string.IsNullOrEmpty(Model.ReturnUrl)) {
        <partial name="_BackLink" model="Model.ReturnUrl" />
    }
}
```

**Validation rules (ReturnUrlHelper.cs):**
- No external URLs (reject http://, https://)
- No protocol-relative URLs (reject //)
- No path traversal (reject ..)
- No XSS vectors (reject <, >, ", ')
- Allowlist against known routes
- Fallback to canonical module route if invalid

### PMSchedule Execution Loop

**Never manually create MaintenanceSchedule; use PMSchedule:**

```csharp
// WRONG:
var ms = new MaintenanceSchedule { ... };

// RIGHT:
var ps = new PMSchedule {
    TemplateId = template.Id,
    AssetId = asset.Id,
    Frequency = frequency,
    NextOccurrenceDate = nextDate
};

// PMExecutionHostedService polls PMSchedule and generates work orders
```

**Canonical IDs:** DemoPackV2 defines seed IDs (ASSET-001, ASSET-002, etc.); tests rely on these.

### Work Order Origin Classification

**Classification is deterministic, not mutable:**

```csharp
public async Task<WorkOrderOrigin> GetOriginAsync(int workOrderId)
{
    var wo = await _db.MaintenanceEvents.FindAsync(workOrderId);
    
    // Smart Assist check
    if (wo.CustomField1?.Contains("SMART:") && wo.IsAIAssisted)
        return WorkOrderOrigin.SmartAssist;
    
    // PM Schedule check
    if (wo.CustomField1?.StartsWith("PMTA:") || 
        (wo.Type == MaintenanceType.Preventative && wo.RecurrenceIntervalDays > 0))
        return WorkOrderOrigin.PMSchedule;
    
    // Manual (default)
    return WorkOrderOrigin.Manual;
}
```

No database column; classification from heuristics. Origin badges on UI pages; updated on page load.

### Smoke Test Isolation

**All tests share DemoPackV2 baseline; must not interfere:**

```csharp
public async Task Test_CustomScenario()
{
    // DemoPackV2 already loaded (5 assets, 3 WOs, etc.)
    
    // Create additional test data
    var newAsset = new Asset { /* ... */ };
    _db.Assets.Add(newAsset);
    
    // Assert on results
    Assert.NotNull(newAsset.Id);
    
    // Do NOT save; transaction will rollback
    // await _db.SaveChangesAsync();  ← DO NOT CALL
}
```

**Transaction guarantee:** Test transaction always rolls back (even on success), verified post-test.

### Idempotency is Natural Key Based

**Seeding checks before inserting using business keys (asset number, WO number, item code), not IDs. Allows safe re-seeding. BUT: if you change the business key while seeding is running, you'll get duplicates. The seeding transactions are wrapped, so failures rollback; but mid-transaction errors are tricky to debug.**

### CCA Depreciation Requires Special Seeding

**Canadian CCA classes are seeded by `CcaClassSeeder.cs`, not by the generic JSON loader. CCA is a Canadian-specific tax feature. If you need to support a new CCA class, update the seeder, not the JSON files.**

### Multi-Book Depreciation Recalculation is Not Automatic

**Books don't auto-update when asset cost changes. Depreciation schedules are calculated once and persist. Period close requires an explicit depreciation run (`CalculateDepreciationAsync`). Changing asset cost after period close doesn't update historical depreciation.**

### Outbox Pattern is Fire-and-Forget

**Webhooks are delivered by a background service. If you need synchronous delivery, you'll need to change the architecture. Current pattern supports at-least-once semantics (may deliver multiple times); idempotency key on receiver side prevents duplicates.**

### API Detail Endpoints Are Intentionally Limited

**The 21 detail card endpoints (`/api/details/*`) return a fixed schema: >=12 header fields, >=3 sections. This is enforced by ADR-005. Adding new fields to the detail schema requires smoke test updates.**

### Breadcrumbs Are Not Automatic

**Each page manually constructs breadcrumbs in the hero header. No global breadcrumb service. If you see missing breadcrumbs, a page wasn't updated to the Unified Header pattern. Check `_ScreenHeader.cshtml`.**

---

## CONCLUSION

**CherryAI EAM is a mature, multi-tenant asset and maintenance management system with strong documentation discipline.** The project uses disciplined architectural patterns (query filters, outbox pattern, idempotent seeding, transaction rollback tests), clear decision logging, and comprehensive quality gates.

**Key strengths:**
- Multi-book depreciation engine (22 methods, 6 books, 2 jurisdictions)
- Reliable webhook integration (outbox pattern, idempotency)
- Tenant isolation enforced at database layer
- Extensive smoke test coverage with zero test data pollution
- Well-defined brand guardrails and UX standards

**Key watch-outs:**
- PO Details page FK migration incomplete (3 handlers need enum ↔ FK sync)
- PMSchedule vs MaintenanceSchedule duality (canonical choice enforced but legacy model still present)
- Lookup cache invalidation required after mutations
- Return URL validation is strict (many URLs fail validation)
- Enum + FK dual-column pattern throughout (intentional backward compatibility)

**For next developer:**
Read in order: README.md → Architecture.md → DECISION_LOG.md → HANDOFF_STATUS.md → TenancyAndSecurity.md. Then dive into the module you're working on (Materials → Materials.md, Depreciation → FinancialsAndDepreciation.md, etc.). Refer to RUNBOOK_QUALITY.md to understand the quality gates and proof bundle.

The codebase is primed for feature development with strong guardrails. Added 14 February 2026.

---

**Total Word Count:** ~7,200 words
