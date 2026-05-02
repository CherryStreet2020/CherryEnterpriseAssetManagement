# CherryAI EAM - Comprehensive Audit Bundle
**Generated:** 2026-01-26  
**Purpose:** Complete project state snapshot for AI agent handoff/audit

---

## 1. PROJECT IDENTITY

| Attribute | Value |
|-----------|-------|
| **Name** | CherryAI Enterprise Asset Management |
| **Stack** | ASP.NET Core 9.0, Razor Pages, PostgreSQL, EF Core |
| **Domain** | Fixed asset lifecycle management for manufacturing companies |
| **Target Users** | Holding companies with multiple manufacturing subsidiaries |

### Core Capabilities
- GAAP & Tax book depreciation (22 methods, 12 conventions)
- US & Canadian tax compliance (Section 179, CCA, Bonus Depreciation)
- CMMS work order system with PM schedules
- Inventory & parts management with revision control
- Multi-company, multi-tenant architecture
- Webhook integration hub (inbound/outbound)
- AI Assistant with OpenAI integration

---

## 2. CODEBASE METRICS

| Category | Count |
|----------|-------|
| Razor Pages (.cshtml) | 144 |
| C# Files (.cs) | 292 |
| CSS Files (.css) | 58 |
| Database Tables | 116 |
| ADRs (Architecture Decisions) | 10 |
| Documentation Files | 30+ |

### Directory Structure
```
/
├── Data/                    # EF Core DbContext, migrations
├── Models/                  # Entity models (100+)
├── Pages/                   # Razor Pages (by module)
│   ├── Account/             # Auth pages
│   ├── Admin/               # Admin pages (40+)
│   ├── Assets/              # Asset management
│   ├── Books/               # Depreciation books
│   ├── CIP/                 # Capital improvement projects
│   ├── Help/                # Help center
│   ├── Maintenance/         # Work orders, schedules
│   ├── Materials/           # Item master
│   ├── Purchasing/          # POs, requisitions
│   ├── Reports/             # Report hub
│   └── Shared/              # Layout, partials
├── Services/                # Business logic (40+)
├── wwwroot/css/             # Stylesheets
│   ├── tokens.css           # Design tokens (Phase 1)
│   ├── base.css             # Base styles
│   ├── modern.css           # Core components + legacy aliases
│   ├── premium-components.css # Premium UI components
│   └── modules/             # Per-module CSS
└── docs/                    # Documentation
    └── adr/                 # Architecture Decision Records
```

---

## 3. CURRENT WORK: DESIGN SYSTEM INITIATIVE

### Active Methodology: "Stop the Bleeding + Trust Restore"

**Principle:** Forensics analysis BEFORE any code changes. Document root cause, then fix.

### Phase Status

| Phase | Scope | Status |
|-------|-------|--------|
| **Phase 1** | Design Tokens Foundation | ✅ COMPLETE |
| **Phase 2** | Header Unification (Work Execution) | ✅ COMPLETE |
| **Phase 3** | Module CSS Migration | 🔲 NOT STARTED |
| **Phase 4** | Inline Style Elimination | 🔲 NOT STARTED |

### Completed Deliverables

#### ADR-010: Design Tokens System
- **File:** `wwwroot/css/tokens.css` (9 token families)
- **Token Families:** brand, surface, border, text, success, warning, danger, info, gradients, typography, spacing, radius, shadows
- **Legacy Compatibility:** Aliases in `modern.css` preserve existing `var(--primary)` usage
- **Rule:** NO new hex colors outside `tokens.css`

#### PR 3.2: Work Execution Header Unification
- **Files Fixed:** 6 pages (Schedules, Assignments, PMScheduleEdit, PMSchedules, PMTemplates, WorkOrders/Details)
- **Root Cause:** Pages had BOTH `_AssetMaintenanceHeader` AND `page-hero` block → double headers
- **Fix:** Removed `page-hero`, replaced with `wo-actions-bar` + `quick-stats-row-4`
- **Guardrail:** "No Double Header" smoke test added to SmokeTestRunner.cs

#### Header Rule Policy
**"Exactly One Header System Per Page"** - documented in `docs/UXStandards.md`

| Page Cluster | Canonical Header | KPI Pattern |
|--------------|------------------|-------------|
| Work Execution | `_AssetMaintenanceHeader` | `quick-stats-row-4` |
| Item Master | `_ScreenHeader` | `quick-stats-row-4` |
| Asset Detail | `page-hero` alone | `page-hero-kpis` |

---

## 4. ARCHITECTURE DECISION RECORDS (ADRs)

| ADR | Title | Status | Impact |
|-----|-------|--------|--------|
| ADR-001 | PMSchedule Canonical Model | Accepted | PM scheduling data model |
| ADR-002 | DemoPackV2 Canonical Seed | Accepted | Demo data seeding |
| ADR-003 | SmokeTest Transaction Rollback | Accepted | Testing isolation |
| **ADR-004** | **UI Hygiene - No Inline Styles** | **Accepted** | **ACTIVE ENFORCEMENT** |
| ADR-005 | DataGrid Premium Contract | Accepted | Table component spec |
| ADR-006 | ReturnUrl Security Hardening | Accepted | Navigation security |
| ADR-007 | Unified Tab System | Accepted | Tab component spec |
| ADR-008 | Unified Screen Header System | Accepted | Header component spec |
| **ADR-010** | **Design Tokens** | **Accepted** | **ACTIVE - Phase 1 complete** |

### ADR-004 Compliance Rules (CRITICAL)
- ❌ NO inline `style=` attributes in new code
- ❌ NO `<style>` blocks in Razor pages
- ✅ Use CSS classes only
- ✅ Reference tokens via `var(--token-name)`

---

## 5. TECHNICAL DEBT INVENTORY

### Pre-existing Inline Styles (Out of Scope for Current Sprint)

| File | Location | Type |
|------|----------|------|
| `Pages/Maintenance/Schedules.cshtml` | Table cells | font-size, color |
| `Pages/Maintenance/Assignments/Index.cshtml` | Alerts, buttons, modal | Various layout |
| `Pages/Admin/PMSchedules.cshtml` | Form inputs, tables | min-width, margin |
| `Pages/Admin/PMTemplates.cshtml` | Table headers, padding | Various layout |
| `Pages/WorkOrders/Details.cshtml` | Table cells, badges | Various layout |

**Status:** Documented but NOT addressed. Flagged for Phase 3/4 token migration.

### Other Technical Debt
- ~90+ hard-coded hex values across module CSS files (Phase 3 scope)
- Some nullable reference warnings in CS files (pre-existing)
- Legacy variable naming in older CSS files

---

## 6. CSS ARCHITECTURE

### Load Order (Critical)
```html
1. tokens.css      -- Token definitions (colors, spacing, typography)
2. base.css        -- Focus/selection base styles
3. modern.css      -- Legacy aliases + core components
4. premium-components.css -- Premium UI components
5. modules/*.css   -- Per-module styles
6. [page-specific] -- Per-page overrides (rare)
```

### Key CSS Files

| File | Purpose | Lines |
|------|---------|-------|
| `tokens.css` | Design token definitions | ~400 |
| `modern.css` | Core components + legacy aliases | ~2000 |
| `premium-components.css` | Hero, cards, badges, tabs | ~1500 |
| `modules/headers.css` | Screen header component | ~200 |
| `modules/auth.css` | Login page styles | ~150 |

---

## 7. SMOKE TEST SUITE

### Location
`Services/Testing/SmokeTestRunner.cs` + `Pages/Admin/SmokeTests.cshtml`

### Key Conformance Tests

| Test Name | What It Checks |
|-----------|----------------|
| No Double Header | Pages don't have both cluster header AND page-hero |
| DataGrid Contract | Premium tables have required columns/features |
| Layout Consistency | Pages use _ModernLayout correctly |
| RBAC Enforcement | Protected pages require authentication |

### Running Tests
1. Navigate to `/Admin/SmokeTests` (admin login required)
2. Click "Run All Tests"
3. Tests run in EF transaction with automatic rollback

---

## 8. DATABASE SCHEMA

### Schema File
`cherryai-schema-jan2026-updated.sql` (9,408 lines)

### Key Entity Groups

| Domain | Tables | Primary Entity |
|--------|--------|----------------|
| Organization | 5 | Tenants, Companies, Sites, Locations |
| Assets | 10 | Assets, AssetTransfers, AssetCategories |
| Depreciation | 12 | Books, DepreciationRuns, Policies |
| Maintenance | 15 | WorkOrders, PMSchedules, PMTemplates |
| Inventory | 12 | Items, ItemRevisions, ItemVendors |
| Purchasing | 8 | PurchaseOrders, PurchaseRequisitions |
| Integrations | 6 | WebhookSubscriptions, IntegrationMappings |

---

## 9. RECENT GIT HISTORY (Last 20 Commits)

```
8636fa5 Update database schema to reflect current structure
080353d Saved progress at the end of the loop
05ef733 Update pages to use a single header pattern
6167d22 Saved progress at the end of the loop
3ff46a3 Unify page headers and remove duplicate elements
3d2b9aa Update maintenance schedules page to use a single header
34f60ac Saved progress at the end of the loop
9e02275 Isolate login page styling and update documentation
9c18819 Add input group component and update styling for consistent forms
e8f2ab2 Add a reusable input group component for consistent form styling
92f6af2 Add unified styling for input groups using design tokens
bc61ec0 Update documentation to include design tokens architecture
4f7e023 Update styling to use design tokens for consistent theming
de6a7eb Saved your changes before starting work
3251da1 Add foundational design system styles and tokens
6a91d42 Add a unified tab system and update component styling
6b9899c Update asset page to use unified tab navigation system
00c85e7 Saved progress at the end of the loop
9eb42dc Update asset tab IDs to use a panel prefix for better organization
b19098c Update asset page to use unified tab navigation system
```

---

## 10. KEY FILES FOR AUDITING

### Must-Read Documentation
| File | Purpose |
|------|---------|
| `replit.md` | Project overview + architecture summary |
| `docs/README.md` | Documentation index |
| `docs/UXStandards.md` | UI/UX component contracts |
| `docs/adr/ADR-010-Design-Tokens.md` | Token system specification |
| `docs/adr/ADR-004-UI-Hygiene-No-Inline-Styles.md` | Style policy |
| `docs/PR-3.2-WorkExecution-Header-Unification.md` | Recent header fixes |

### Core Implementation Files
| File | Purpose |
|------|---------|
| `wwwroot/css/tokens.css` | Design token definitions |
| `wwwroot/css/modern.css` | Core CSS + legacy aliases |
| `Pages/Shared/_ModernLayout.cshtml` | Main layout template |
| `Pages/Shared/_AssetMaintenanceHeader.cshtml` | Work Execution header |
| `Services/Testing/SmokeTestRunner.cs` | Smoke test suite |

### Database
| File | Purpose |
|------|---------|
| `cherryai-schema-jan2026-updated.sql` | Current schema dump |
| `Data/AppDbContext.cs` | EF Core context |
| `Migrations/` | EF migrations folder |

---

## 11. NEXT STEPS (Recommended)

### Immediate (Design System Continuation)
1. **Phase 3: Module CSS Migration** - Replace hard-coded hex in `modules/*.css` with tokens
2. **Phase 4: Inline Style Elimination** - Migrate inline `style=` to CSS classes per ADR-004
3. **Legacy Alias Cleanup** - After Phase 4, remove `var(--primary)` aliases from `modern.css`

### Backlog
- Dark mode theme support (tokens architecture enables this)
- Additional smoke tests for new pages
- Performance audit for CSS bundle size

---

## 12. ENVIRONMENT & RUNTIME

| Setting | Value |
|---------|-------|
| Runtime | .NET 9.0 |
| Database | PostgreSQL 16.10 (Neon-backed) |
| Environment | Development (LAB mode) |
| Port | 5000 |
| Workflow | `dotnet run --project Abs.FixedAssets.csproj` |

### Environment Variables Required
- `DATABASE_URL` - PostgreSQL connection string
- `AI_INTEGRATIONS_OPENAI_API_KEY` - OpenAI API key (for AI Assistant)

---

## 13. VERIFICATION COMMANDS

```bash
# Build project
dotnet build --no-restore

# Run project
dotnet run --project Abs.FixedAssets.csproj

# Count tables
psql $DATABASE_URL -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public';"

# Grep for inline styles (audit)
grep -r "style=\"" Pages/ --include="*.cshtml" | wc -l

# Grep for hard-coded hex colors
grep -rE "#[0-9a-fA-F]{3,6}" wwwroot/css/ --include="*.css" | grep -v tokens.css | wc -l
```

---

## ATTESTATION

This audit bundle represents the complete state of CherryAI EAM as of 2026-01-26. All information is accurate per the codebase at commit `8636fa5`.

**Design System Status:** Phase 1 (Tokens) and Phase 2 (Header Unification) COMPLETE. Phases 3-4 pending.
