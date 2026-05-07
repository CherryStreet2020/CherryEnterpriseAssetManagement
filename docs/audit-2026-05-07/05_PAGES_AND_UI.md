# 05 — Pages & UI Inventory

**Total pages:** 410 .cshtml files across 21 module subdirectories
**Total page models:** 132 .cshtml.cs files
**Design system:** Custom design tokens + premium component library
**Layout master:** `_ModernLayout.cshtml`

---

## 1. Module Inventory

### AI (4 pages)
Core AI assistant functionality.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/AI` | AI chat assistant interface; multi-turn conversation with asset/WO context |
| (3 partials) | `/AI/*` | Chat UI, context injection, response formatting |

**Workflow:** User → natural language query → OpenAI/Claude API → asset/WO suggestions/automations.
**Status:** functional but bare; SmartAssistService is keyword-only today (see services audit).

---

### API (7 pages)
REST API documentation, webhook testing, integration hub.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/API` | API documentation, key management, webhook explorer |
| Import.cshtml | `/API/Import` | Bulk data import via REST (CSV/JSON upload) |
| (5 partials) | `/API/*` | Actions, context, KPI displays |

---

### Account (3 pages)
Authentication & user account management.

| Page | Route | Purpose |
|---|---|---|
| Login.cshtml | `/Account/Login` | Cookie-based login form |
| Logout.cshtml | `/Account/Logout` | Sign-out POST handler |
| AccessDenied.cshtml | `/Account/AccessDenied` | 403 page |

---

### AccountsPayable (9 pages)
Vendor invoice matching, approval, payment.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/AccountsPayable` | Invoice list (Draft, Submitted, Matched, Paid) |
| Create.cshtml | `/AccountsPayable/Create` | New invoice (manual or from goods receipt) |
| Details.cshtml | `/AccountsPayable/Details/{id}` | Invoice detail, 3-way match (PO/receipt/invoice), approve/reject |
| (6 partials) | — | Actions, context, KPI strips |

**Workflow:** Goods receipt → match invoice → verify amounts → approve payment → post to GL.

---

### Assets (21 pages)
Core asset lifecycle: creation, modification, transfer, disposal.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/Assets` | Asset register (searchable grid, filter by status) |
| Asset.cshtml | `/Assets/Asset/{id}` | Asset detail/edit; tabs for financials, maintenance, history |
| Schedule.cshtml | `/Assets/Schedule/{id}` | PM schedules tied to asset |
| Transfer.cshtml | `/Assets/Transfer/{id}` | Inter-location/department transfer (audit trail) |
| Dispose.cshtml | `/Assets/Dispose/{id}` | Disposal workflow (reason, proceeds, date) |
| Improve.cshtml | `/Assets/Improve/{id}` | Capitalize improvements (increase cost/useful life) |
| Delete.cshtml | `/Assets/Delete/{id}` | Soft delete confirmation (admin only) |
| (14 partials) | — | Hero headers, KPI cards (NBV, acquisition cost, etc.) |

**FK migration status:** **fully migrated.** Asset, Dispose, Transfer pages are FK-bound.
**Workflow:** Create → assign → schedule PM → maintain → transfer → improve → dispose.

---

### Books (14 pages)
Depreciation books, GL account mapping, period close.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/Books` | List depreciation books (GAAP, Tax, IFRS, Statutory) |
| Create.cshtml | `/Books/Create` | New book setup (method, convention) |
| Edit.cshtml | `/Books/Edit/{id}` | Update book parameters |
| Details.cshtml | `/Books/Details/{id}` | Book detail; tabs for settings, GL mappings, period close |
| GlAccounts.cshtml | `/Books/GlAccounts` | Map GL accounts to books |
| Delete.cshtml | `/Books/Delete/{id}` | Soft delete (requires no active assets) |
| (8 partials) | — | Context for book type, status, asset count |

**FK migration status:** **fully migrated.**
**Workflow:** Create → set GL mappings → create depreciation schedule → generate journals at period end.

---

### BulkOperations (7 pages)
Batch asset modifications.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/BulkOperations` | Operation launcher (select operation type, asset filter) |
| Details.cshtml | `/BulkOperations/Details/{id}` | Review results, redo/undo, export summary |

---

### CCA (7 pages)
Canadian Capital Cost Allowance tax compliance.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/CCA` | CCA class summary (Classes 1-50, cumulative balance) |
| ClassReport.cshtml | `/CCA/ClassReport/{classId}` | Detailed class report (additions, disposals, deductions, BA pool) |

**Workflow:** Asset classified → pool cost → apply rate → CCA deduction for tax return.

---

### CIP (19 pages)
Capital Improvement Projects: budget, cost accumulation, place-in-service.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/CIP` | Project list (filter by status) |
| Details.cshtml | `/CIP/Details/{id}` | Project detail: budget, status, location, PM, GL account |
| Costs.cshtml | `/CIP/Costs` | Cost tracker (table by type, amount, date) |
| CostDetails.cshtml | `/CIP/Costs/CostDetails/{id}` | Individual cost entry detail/edit |
| CostTypeDetails.cshtml | `/CIP/Costs/CostTypeDetails/{typeId}` | Rollup by cost type |
| PartyDrilldown.cshtml | `/CIP/PartyDrilldown` | Cost analytics by party (chart + table) |
| (13 partials) | — | Project header KPIs (budget vs spent), cost matrix |

**FK migration status:** **fully migrated.**
**Workflow:** Create CIP project (with GL account, budget) → add costs → monitor → close & capitalize as asset → asset auto-created with depreciation set up.

---

### Help (16 pages)
Help center, task library, topic-specific guidance.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/Help` | Help hub |
| Topic.cshtml | `/Help/Topic?id={topic}` | Context-sensitive help |
| Tasks.cshtml | `/Help/Tasks` | Task library (step-by-step guides) |
| Implementation.cshtml | `/Help/Implementation` | Implementation checklist, setup guide |

---

### Inventory (8 pages)
Parts inventory: stock levels, warehouses, transactions.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/Inventory` | Warehouse/bin inventory grid |
| List.cshtml | `/Inventory/List` | Alternative inventory list view |

---

### Journals (11 pages)
GL journal entries: manual, system-generated, depreciation runs.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/Journals` | Journal entries list |
| Generate.cshtml | `/Journals/Generate` | System journal generation (depreciation, asset cap/disposal) |
| Details.cshtml | `/Journals/Details/{id}` | Journal entry detail (lines, posting date, GL accounts, amounts) |

**Workflow:** Manual → create journal → post to GL; System → depreciation run → auto-generate → post.

---

### Maintenance (31 pages)
Work order and preventive maintenance lifecycle. **Largest functional module.**

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/Maintenance` | WO list (status, KPIs: backlog, compliance %) |
| Create.cshtml | `/Maintenance/Create` | New WO (asset, technician, type, priority) |
| Details.cshtml | `/Maintenance/Details/{id}` | WO detail: tasks, labor, parts, closeout |
| WorkRequests/Index.cshtml | `/Maintenance/WorkRequests` | Work request inbox (technician submissions) |
| WorkRequests/Details.cshtml | — | Work request detail with conversion-to-WO dialog |
| Schedules.cshtml | `/Maintenance/Schedules` | PM schedule list |
| Assignments.cshtml | `/Maintenance/Assignments` | PM assignment tracker |
| ScheduleBoard.cshtml | `/Maintenance/ScheduleBoard` | Gantt/calendar view |
| Technicians/Index.cshtml | `/Maintenance/Technicians` | Technician roster |
| Technicians/Profile.cshtml | — | Technician detail (assigned WOs, skills) |

**FK migration status:** **fully migrated.**
**Workflow:** Asset → PM template → schedule → system generates WOs → assign technician → execute → closeout (labor, parts, lessons learned) → recurring failure detection.

---

### Materials (13 pages)
Items/parts master: catalog, specs, vendor cross-reference.

| Page | Route | Purpose |
|---|---|---|
| Items.cshtml | `/Materials/Items` | Item master list |
| Create.cshtml | `/Materials/Create` | New item entry |
| Edit.cshtml | `/Materials/Edit/{id}` | Item master edit |
| ItemEdit.cshtml | `/Materials/ItemEdit/{id}` | Alternative detail page (tabs: basic, vendors, specs, inventory) |

**FK migration status:** **fully migrated.** Includes RevisionStatus FK pattern.

---

### Purchasing (10 pages)
Purchase orders: creation, receipt, invoice matching, approval.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/Purchasing` | PO list |
| Create.cshtml | `/Purchasing/Create` | New PO form |
| **Details.cshtml** | `/Purchasing/Details/{id}` | PO detail — **⚠️ NOT YET FK-MIGRATED** |
| Requisitions.cshtml | `/Purchasing/Requisitions` | Purchase requisitions |

**⚠️ Critical TODO:** `Pages/Purchasing/Details.cshtml.cs` needs FK migration:
- `OnPostUpdateHeaderAsync` uses `(POType)poType` cast — needs FK pattern
- Status workflow buttons (Submit, Approve, etc.) use `POStatus.Draft` etc. without syncing FK
- `OnPostDuplicatePOAsync` needs to copy `POTypeLookupValueId` and set `StatusLookupValueId` for Draft

**Workflow:** Work order → auto-requisition → user creates PO → submit → approve → receive → match invoice → pay.

---

### Receiving (12 pages)
Goods receipt: inspection, three-way matching, discrepancies.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/Receiving` | Goods receipt list (read-only — uses enum filter, low priority for FK migration) |
| Receive.cshtml | `/Receiving/Receive/{poId}` | Receipt entry (scan items or manual) |
| Inspect.cshtml | `/Receiving/Inspect/{receiptId}` | Quality inspection (pass/reject/conditional) |
| History.cshtml | `/Receiving/History` | Receipt history |
| Details.cshtml | `/Receiving/Details/{id}` | Receipt detail |

**Workflow:** PO → expect receipt → receive (scan or manual) → inspect → match invoice → post to GL.

---

### Reports (28 pages)
Financial, compliance, operational reporting.

#### Primary report pages
| Report | Route | Purpose | Output |
|---|---|---|---|
| ReportHub.cshtml | `/Reports/ReportHub` | Report catalog | — |
| Builder.cshtml | `/Reports/Builder` | Custom report builder (drag-drop columns, filters) | — |
| **DepreciationSchedule** | `/Reports/DepreciationSchedule` | Depreciation by asset, book, period | Excel, PDF, CSV |
| **DepreciationPreview** | `/Reports/DepreciationPreview` | Preview before period close | PDF, CSV |
| ChartOfAccounts | `/Reports/ChartOfAccounts` | GL account listing with balances | Excel, CSV |
| **Form4562** | `/Reports/Form4562` | US IRS Form 4562 (depr/amort, bonus, Sec 179) | PDF, Excel |
| **T2Schedule8** | `/Reports/T2Schedule8` | Canadian T2 Schedule 8 (CCA by class) | PDF, Excel |
| Compliance | `/Reports/Compliance` | Audit readiness checklist | PDF |
| Export (Bulk) | `/Reports/Export` | Multi-module export | Excel, CSV, JSON |
| Index | `/Reports/Index` | Report landing page | — |

**Formats:** Excel (ClosedXML), PDF (QuestPDF), CSV, JSON.
**Workflow:** Select report → set filters (date, type, book, location) → preview → export.

---

### UsTax (4 pages)
US-specific tax reporting.

| Page | Route | Purpose |
|---|---|---|
| Index.cshtml | `/UsTax` | US tax module hub (Form 4562, bonus depreciation, Section 179 elections) |

---

### WorkOrders (4 pages)
Work order detail (cross-ref to Maintenance module).

| Page | Route | Purpose |
|---|---|---|
| Details.cshtml | `/WorkOrders/Details/{id}` | WO detail (tasks, labor, parts, closeout) |

**FK migration status:** **fully migrated.** Uses `OperationType`, `OperationStatus` lookup keys.

---

### Admin (158 pages — largest module by far)

Logically grouped into:

#### Master Data (28 pages)
- **Sites & Locations:** Sites.cshtml (FK-migrated), Locations.cshtml (FK-migrated)
- **Organizational:** Companies, Company, Departments (FK-migrated), CostCenters (FK-migrated)
- **GL Accounts:** GlAccounts.cshtml (FK-migrated)

#### User & Security (3 pages)
- Users (Admin/Accountant/Viewer roster), Tenants, Approvals (PO approval levels, invoice approval workflows)

#### Lookups & Reference Data (3 pages)
- Lookups (76 lookup type editor)
- Lookups/EditValues (add/edit/delete codes, sort order)
- ExchangeRates (multi-currency support)

#### Inventory & Materials (6 pages)
- Items, ItemCategories, Kits, StockLevels, Vendors, Manufacturers

#### Maintenance Setup (5 pages)
- Technicians, PMTemplates, PMTemplateEdit, PMSchedules, PMScheduleEdit

#### Asset Setup (3 pages)
- AssetCategories, Barcodes (asset tag management), WorkOrders (defaults)

#### Finance & Books (2 pages)
- Books (referenced from `/Books/`), Requisitions (rules + approval levels)

#### Data Management (7 pages)
- DataManagement, Import, ImportWizard, Export, SeedData, DemoData, DataImport

#### Operational (5 pages)
- Index (admin dashboard), AuditLog (transaction trail), SystemSettings, EnvironmentStatus, SmokeTests

#### Integrations & Webhooks (6 pages)
- Webhooks/, Webhooks/Index, Webhooks/Deliveries (webhook subscriptions, inbound processor, delivery logs)
- Integrations/, Integrations/Index, Integrations/Maps (integration hub, field mapping)
- Outbox/, Outbox/Index (events awaiting webhook dispatch, retry controls)

#### Diagnostic & Maintenance (3 pages)
- Diagnostics, CcaBackfill, DepreciationBackfill, JournalBackfill

---

### Shared (15 pages — partials & layouts)

| File | Purpose |
|---|---|
| **_ModernLayout.cshtml** | Master layout (sidebar nav, header, footer; theme toggle, org/site selector) |
| **_ScreenHeader.cshtml** | Hero header partial (title, subtitle, status badge, KPI strip, actions) |
| **_TabNav.cshtml** | Tab navigation component (underline style, active state) |
| **_FormField.cshtml** | Standardized form field wrapper (label, input, error, help, accessibility) |
| **_KpiStrip.cshtml** | KPI card grid partial (label, value, optional tone/variant) |
| _QuickStat.cshtml / _QuickStatIcon.cshtml | Single stat card components |
| _SectionCard.cshtml | Section wrapper (bordered card, padding) |
| _BackLink.cshtml | Back navigation link with fallback |
| _Pagination.cshtml | Paginated grid footer |
| _PopoutLayout.cshtml | Modal/popout wrapper |
| _EmptyState.cshtml | Empty state placeholder (icon, message, CTA) |
| _ValidationScriptsPartial.cshtml | Client-side validation scripts |
| _AssetMaintenanceHeader.cshtml | Asset-specific header with maintenance KPIs |
| _Layout.cshtml (legacy) | Pre-_ModernLayout layout — being phased out |

---

## 2. Design System & Shared Partials

### Layout Architecture

**Master layout:** `_ModernLayout.cshtml`
- Responsive sidebar (collapsible, 260px width)
- Top header with global search, command palette (Ctrl+K), theme toggle, user menu
- Organization/company selector (multi-tenant scoping)
- Site filter dropdown
- Left sidebar nav (grouped by module: Operations, Finance, Materials, Work, Projects, AI, System)
- Dark/light theme toggle (CSS variables, localStorage-persisted)
- Footer with copyright

**Page template flow:**
```
_ModernLayout (master)
  ├─ Sidebar nav (grouped menu items)
  ├─ Main header (breadcrumb, title, actions, search)
  ├─ _ScreenHeader (optional hero with KPIs)
  ├─ Page body (RenderBody())
  └─ Footer
```

### Design Tokens (`tokens.css`)

| Family | Tokens |
|---|---|
| **Brand colors** | Red `#cf3339`, Navy `#081e3a`, primary blue ramp |
| **Surface colors** | Background, card, border, overlay, input |
| **Text colors** | Primary, secondary, muted, inverted |
| **Semantic colors** | Success (green), Warning (yellow), Danger (red), Info (blue), Muted (gray) |
| **Typography** | Body 14px (1rem), H1 32px (2rem) bold, H2 24px (1.5rem) bold, H3 18px bold, Small 12px, Mono 13px |
| **Spacing** | xs 4px, sm 8px, md 16px, lg 24px, xl 32px, 2xl 48px, 3xl 64px |
| **Radius** | sm 4px, md 8px, lg 16px, full 9999px |
| **Shadows** | 4 elevation levels |
| **Z-index** | base 0, dropdown 100, modal 1000, tooltip 1200 |

### Premium component library
- **DataGrid:** global search, multi-column sort, per-column filter dropdowns, CSV/Excel export, column visibility toggle, pagination
- **Form fields:** standardized via `_FormField.cshtml`; types: text, textarea, select, checkbox, radio, date, number
- **KPI cards:** frosted glass effect (backdrop-filter), semi-transparent background, optional icon, colored accent bar
- **Modals:** overlay, centered, close button, action footer
- **Tabs:** underline style, left-aligned, aria-selected, scrollable
- **Buttons:** primary (solid), secondary (outline), danger, link variants; sizes (sm, md, lg)
- **Badges:** status pills with tones (success, warning, danger, info, muted)
- **Cards:** section cards with optional header/footer
- **Navigation:** breadcrumbs, back links, sidebar groups with expand/collapse

### CSS module architecture
```
wwwroot/css/
├── tokens.css                   # Design token variables (9 families)
├── base.css                     # HTML resets, typography
├── modern.css                   # Layout, sidebar, header
├── premium-components.css       # Grid, KPI, modal, form styling
├── sidebar-nav.css              # Sidebar nav grouping, collapse
├── command-palette.css          # Command palette UI
├── modules/                     # 20+ module-specific CSS
│   ├── forms.css, dashboard.css, assets.css
│   ├── finance.css, reports.css, maintenance.css
│   ├── cip.css, inventory.css, admin.css
│   ├── purchasing.css, workorders.css ...
├── cherryai-theme.css           # Custom theme overrides
└── cherryai-dark-compliance.css # Dark mode compliance
```

### Accessibility features
- ARIA labels on interactive elements
- Form field error messages linked via `aria-describedby`
- Breadcrumbs with `aria-current="page"`
- Data tables with th/td semantics
- Keyboard navigation (tab, arrow keys, enter)
- Color contrast meets WCAG AA

---

## 3. Major User Workflows

### Asset Lifecycle
```
Asset Creation
  → Asset.cshtml: Enter details (number, description, cost, location, category)
Location Assignment
  → Asset.cshtml: Select Location from master
Maintenance Scheduling
  → Maintenance/Schedules.cshtml: Link PM template to asset
  → System auto-generates MaintenanceEvents at interval
Maintenance Execution
  → Maintenance/Index.cshtml: Technician assigned to WO
  → Maintenance/Details: Log labor, parts, closeout
Transfer
  → Assets/Transfer.cshtml: Select new location/department
Disposal
  → Assets/Dispose.cshtml: Set reason, proceeds, date
  → GL entry for loss/gain
Retire
  → Book depreciation to zero, archive in Asset register
```

### Preventive Maintenance Execution
```
PM Template Creation
  → Admin/PMTemplates.cshtml: Define checklist, labor hours, frequency
PM Schedule Creation
  → Maintenance/Schedules.cshtml: Assign template + frequency
  → Auto-generates MaintenanceEvent at schedule date
Assignment & Execution
  → Maintenance/Assignments.cshtml: Assign to technician
  → Technician completes tasks, logs hours, parts
Closeout & Cost Recording
  → Maintenance/Details.cshtml: Mark Complete
  → System posts labor + parts costs to GL
Metrics & Recurring Failures
  → Dashboard: completion %, MTTR, recurring failure codes
  → ICloseoutService.GetRecurringFailuresAsync() — chronic problem detection
```

### Procurement
```
Requisition → Approval → PO → Approval → Goods Receipt → Inspection → 3-way Match → AP → Payment
                                                                                   ↓
                                                                          GL: AP accrual
```

### Capital Project (CIP)
```
Project Initiation
  → CIP/Details.cshtml: name, budget, location, GL account
Cost Accumulation
  → CIP/Costs.cshtml: Add costs (labor, materials, fees)
Budget Monitoring
  → CIP/PartyDrilldown.cshtml: Dashboard (budget vs spent, % complete)
Project Completion → Asset Creation
  → Status → Completed → System triggers asset creation
  → Asset placed in same location; GL changes from WIP → Fixed Asset
Depreciation Setup
  → Asset assigned to depreciation book(s)
  → Monthly depreciation journal posted
```

### Depreciation & GL
```
Book Setup → Asset Assignment → Calculation → Journal Generation → Posting → Tax Reporting
                                     ↓                                            ↓
                              DepreciationService                          Form 4562 / T2 Sched 8
```

---

## 4. Navigation Architecture

Built dynamically in `_ModernLayout.cshtml` with conditional menu groups based on `IModuleGuardService` flags.

**Section: OPERATIONS**
- Assets, Work Management (Work Orders, Work Requests, PM Templates, PM Schedules, PM Assignments, Schedule Board, Technicians)
- Projects (CIP)

**Section: FINANCE**
- Depreciation Books, GL Accounts, Journals, Accounts Payable, Reports, US Tax, Canadian CCA

**Section: MATERIALS & PURCHASING** (conditional on `EnablePurchasing || EnableInventory || EnableVendors`)
- Items, Vendors, POs, Receiving, Requisitions, Inventory, Stock Levels, Kits, Item Categories

**Section: SYSTEM** (Admin only)
- Org & Sites, Companies, Departments, Cost Centers, Users, Lookups, System Settings, Data Management, Audit Log, Manufacturers

**Section: AI & INTEGRATIONS**
- AI Assistant, API Hub, Webhooks, Integration Hub

**Sidebar footer:** Help Center, Sign In/User Profile (Logout)

---

## 5. AI Surface Area

**Pages:** `/AI` (Index.cshtml + 3 partials)

**Capabilities:**
- Multi-turn chat with asset/WO context injection
- Natural language queries: "Create WO for compressor PM", "What's the book value of asset 2045?", "Which technicians are free next week?"
- System prompt includes tenant scoping, visible asset list, current user context
- Integrates with OpenAI API (Claude or GPT-4 capable)
- Chat history persisted per session

**UI components:**
- Chat thread (user right, AI left)
- Input field with markdown support
- Asset/WO mention autocomplete (planned)
- Copy-to-clipboard for generated content

**Limitations (the disruption opportunity):**
- No IoT/SCADA dashboard integration
- No mobile field inspection app
- No AR/QR scanning UI
- No offline-capable WO execution
- No warranty expiration tracking UI
- No condition-based maintenance triggers UI
- No calibration management UI (model fields exist)
- Chat is desktop-only English text (no voice/image)

---

## 6. Missing or Thin UI Areas

### High-priority enterprise gaps

| Area | Gap | Impact |
|---|---|---|
| **Mobile WO execution** | No responsive PWA mobile app for technicians on-site | MaintainX's whole product wedge |
| **Barcode/QR scanning** | Barcodes.cshtml exists; no mobile scanning UX | Asset lookup requires manual search |
| **Condition-Based Monitoring** | No UI for asset condition trends, alarm thresholds | No data-driven maintenance |
| **Warranty Management** | No page or model | Risk of expired coverage |
| **Calibration Management** | Asset fields exist (CalibrationRequired, NextCalibrationDue, etc.) — no UI | Compliance risk for regulated industries |
| **Spare Parts Reservation** | Stock Levels exists; no real-time reservation during WO | Parts shortages discovered during execution |
| **IoT/Sensor Integration** | Asset model has IoT fields; no live dashboard | No early-warning system |
| **Offline WO Mode** | No offline form caching | Field work halts on connectivity loss |
| **Multi-Step Approval Routing** | Approvals.cshtml exists; no visual workflow builder | Complex approvals require code changes |
| **Asset Photo Attachments** | Attachments model exists; no upload/view UI | Visual condition assessment missing |
| **WO Template Library** | Only PM templates; no reusable ad-hoc WO templates | Recurring ad-hoc WOs require manual recreation |
| **Maintenance History Heatmap** | No visual trend by asset type/location | Cannot identify chronic problem areas |
| **Spare Parts Cross-Reference UI** | CrossReference model exists; no dedicated UI | Slow substitute lookup for technicians |
| **Vendor SLA Tracking** | No SLA metrics | Cannot measure vendor performance |

### Medium-priority gaps
- Bulk Transfer/Dispose UX (preview, validation)
- PM Schedule conflict detection (technician double-booking)
- Asset hierarchies (parent-child grouping)
- Custom fields/attributes per company
- Granular time clock / punch-in for labor tracking
- Multi-step asset retirement workflow

### Design system gaps
- Mobile responsive (media queries minimal)
- Dark mode partially styled
- i18n not visible (English only)
- Keyboard navigation incomplete on modals

---

## 7. Anomalies & Notes

### Pages without .cs models
Some partials are inline-Razor (no dedicated logic). Most rely on parent page model.

### FK-bound enum migration status (per HANDOFF_STATUS.md)
- **Fully migrated:** Assets, Books, Journals (read-only filters), CIP, Maintenance (WO statuses), Item Edit, Sites, Locations, Departments, CostCenters, GlAccounts, Work Orders Details, AP Invoice Details, Requisitions, Asset Dispose/Transfer
- **⚠️ NOT migrated:** Pages/Purchasing/Details.cshtml.cs (Update Header, status workflow buttons, Duplicate PO)
- **Pattern:** Model has both enum + FK lookup field; page chooses which to render

### Tenant scoping
All queries filter by `_tenantContext.VisibleCompanyIds`. Critical for multi-tenant SaaS. Never bypass.

### Lookup seeding
- 76 lookup types in JSON (`seed/reference-data/`)
- LookupService caches with 10-min TTL
- Company/tenant can customize values (system vs. custom flags)

### Background services that affect UI
- WebhookDispatcherHostedService — sends outbox events
- InboundEventProcessorHostedService — processes inbound webhooks
- SmokeTestBackgroundService — diagnostic checks

---

## Summary

CherryAI EAM has **one of the strongest UI surfaces of any modern EAM** — premium DataGrid, KPI cards, dark mode, comprehensive workflows for asset/PM/procurement/CIP/depreciation, ~410 pages across 21 modules. The design system is genuinely 2026-grade and is one of the project's strongest competitive moats vs. Maximo/SAP.

**Strengths to preserve:**
- `_ModernLayout` + `_ScreenHeader` + `_KpiStrip` + premium DataGrid combo
- 28-report library with multi-format export
- Cohesive Admin module (158 pages logically grouped)
- Lookup-driven dropdowns everywhere

**Top UI priorities:**
1. Finish Pages/Purchasing/Details.cshtml.cs FK migration
2. Ship a PWA mobile WO execution view (the MaintainX killer)
3. Add Calibration management UI (fields exist, just need workflow)
4. Add IoT live dashboard for IoT-enabled assets
5. Replace SmartAssistService keyword regex with Claude API call
