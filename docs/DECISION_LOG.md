# CherryAI EAM Decision Log

## Purpose
This document tracks key technical and architectural decisions made during development. Each entry includes context, decision, rationale, and any constraints.

---

## 2026-01-24: Return Path / Back Navigation Standard Adopted

### Decision: All Drill-Down Navigation Must Pass returnUrl; All Detail Pages Must Render Back

**Context:** Users navigate from context screens (list/dashboard/details) into related records but cannot reliably return to where they came from. This causes frustration and workflow interruption.

**Decision:**
1. Every drill-down link MUST pass `returnUrl` query parameter containing source path + query string
2. Every detail page reachable via drill-down MUST render a visible Back affordance
3. Back link uses returnUrl if valid, otherwise falls back to canonical module list
4. returnUrl MUST be validated to prevent open redirects (security requirement)

**Implementation:**
- `Services/Navigation/ReturnUrlHelper.cs` - Central helper for URL building/validation
- `Pages/Shared/_BackLink.cshtml` - Shared partial for consistent back button UI
- Source pages pass returnUrl via asp-route-returnUrl or JavaScript URL construction
- Detail pages bind ReturnUrl property and render _BackLink partial

**Security Rules:**
- No external URLs (reject schemes like http://, https://)
- No protocol-relative URLs (reject //)
- No path traversal (reject ..)
- No XSS vectors (reject <, >, ", ')
- Allowlist validation against known app routes
- Fallback to canonical module route if validation fails

**Guardrail Tests Added:**
- Test 53: "Return Path → Open Redirect Protection" - Validates URL security
- Test 54: "Return Path → Detail Pages Accept returnUrl" - Verifies binding + partial
- Test 55: "Return Path → Source Pages Pass returnUrl" - Verifies parameter passing

**Artifacts Created:**
- `docs/ReturnPathAuditReport.md` - Full implementation report
- `Services/Navigation/ReturnUrlHelper.cs` - URL helper
- `Pages/Shared/_BackLink.cshtml` - Back link partial

**Rationale:**
- Context-preserving navigation improves user experience
- Security validation prevents open redirect vulnerabilities
- Canonical fallbacks ensure users never get stranded
- Automated tests prevent future regressions

---

## 2026-01-24: Navigation and Labels Audited + Enforced by Smoke Tests

### Decision: No Hardcoded href Drift / No Broken asp-page Targets Rule

**Context:** Sidebar navigation and intra-screen links can drift over time as pages are added, renamed, or reorganized. Broken links cause 404 errors and poor user experience.

**Decision:**
1. All sidebar links MUST resolve to existing Razor Page files
2. All asp-page attributes MUST reference valid page targets
3. docs/RouteRegistry.md is the CANONICAL SOURCE OF TRUTH for all routes
4. Automated smoke tests enforce navigation correctness

**Guardrail Tests Added:**
- **"Navigation → All Sidebar Links Resolve"** - Scans sidebar for href patterns and verifies each page exists
- **"Navigation → Intra-Screen asp-page Targets Valid"** - Scans all Razor Pages for asp-page usage and verifies targets exist

**Audit Results:**
- 8 sidebar sections verified
- 58 sidebar links verified (all resolve)
- 65 asp-page usages verified (all valid)
- 403 href usages audited
- No broken links found

**Artifacts Created:**
- `docs/NavigationAuditReport.md` - Full audit report with sidebar inventory
- `docs/RouteRegistry.md` - Updated with CANONICAL SOURCE OF TRUTH header

**Rationale:**
- Prevents broken links from reaching production
- Smoke tests catch future regressions automatically
- Single source of truth for route documentation
- Automated enforcement more reliable than manual review

---

## 2026-01-24: Schema/UI Drift Prevention Policy

### Decision: No Orphan UI Fields / No Orphan DB Columns Rule

**Context:** Discovered potential drift between UI fields (asp-for bindings), EF Core entities, and PostgreSQL columns. This can cause runtime errors, data loss, or empty screens.

**Decision:**
1. Every UI field that users can view/edit MUST be backed by a persisted DB column OR explicitly documented as computed/read-only
2. Every EF Core mapped property MUST exist in the DB schema (migrations applied)
3. Every DB column in operational tables MUST be mapped to EF OR documented as reserved/deprecated
4. Seed data MUST populate all required (non-nullable) fields for every row
5. Optional fields MUST have at least one non-null example value in demo data (coverage seeding)

**Guardrail Tests Added:**
- **"Schema Drift → UI Field Persistence Audit"** - Scans Razor Pages for asp-for bindings and verifies each maps to a persisted EF property or is in the computed fields allowlist
- **"Schema Drift → Seed Data Coverage Audit"** - For core tables, asserts all non-nullable columns are populated and nullable columns have at least one non-null value

**Computed Fields Allowlist:**
Maintained in `SmokeTestRunner.cs` as `ComputedFieldsAllowlist` static field. Categories:
- **Computed from persisted fields:** Asset.BookValue, Asset.CurrentOEE, Asset.CurrentAvailability, Asset.CurrentPerformance, Asset.CurrentQuality
- **ML model outputs:** Asset.PredictiveHealthScore, Asset.PredictedFailureDate
- **UI helper fields:** AssetHint, AttachmentNotes, Mode, Month, HorizonDays, etc.
- **DTO form inputs:** Input.*, password fields, filter inputs

See `SmokeTestRunner.cs` for the authoritative list with inline comments.

**Artifacts Created:**
- `docs/SchemaCoverageReport.md` - Human-readable coverage report
- `docs/SchemaCoverageReport.json` - Machine-readable for CI/CD

**Rationale:**
- Prevents silent data loss from unbound UI fields
- Ensures demo screens display realistic data
- Smoke tests catch future regressions automatically
- Documentation provides audit trail

---

## 2026-01-24: Work Order Details Tenant Scoping Rule

### Decision: Mandatory Tenant-Scoped Loading and Mutation Guards

**Context:** Work Order Details page (`/Maintenance/Details/{id}`) is a critical mutation surface. Users can Start, Complete, Cancel, Edit work orders, upload attachments, and capitalize costs. Without proper tenant isolation, a malicious actor could manipulate work orders belonging to other companies by guessing IDs.

**Decision:**
1. All GET and POST handlers MUST use company-scoped queries that filter by `Asset.CompanyId == TenantContext.CompanyId`
2. The page uses `GetScopedEventAsync(id)` and `GetScopedEventSimpleAsync(id)` helper methods that include the company filter
3. If a work order is not found OR belongs to another company, return `NotFound()` uniformly (no existence leakage via different status codes)
4. `OnPostDispatchUpdateAsync` delegates to `MaintenanceService.UpdateDispatchAsync`, which internally uses `GetEventAsync(id)` with the same company filter
5. Smoke test "WorkOrder Details → Tenant Scoped Access" validates cross-tenant access is blocked

**Implementation Pattern:**
```csharp
private async Task<MaintenanceEvent?> GetScopedEventAsync(int id)
{
    var companyId = GetCompanyId();
    return await _context.MaintenanceEvents
        .Include(e => e.Asset)
        .Where(e => e.Asset != null && e.Asset.CompanyId == companyId)
        .FirstOrDefaultAsync(e => e.Id == id);
}
```

**Rationale:**
- Company isolation is enforced at the data access layer, not just UI
- Uniform `NotFound()` response prevents ID enumeration attacks
- Pattern is consistent with Work Request and other tenant-scoped pages
- Smoke test ensures regression protection

**Constraints:**
- Routes remain unchanged (`/Maintenance/Details/{id}`)
- UX unchanged - users see NotFound for any inaccessible work order
- Pattern applies to all future work order mutation endpoints

---

## 2026-01-21: Product Naming & Branding Standard

### Decision: Product Naming Standard

**Context:** Need consistent product naming across all UI, documentation, and marketing materials.

**Decision:**
1. Full formal name: "CherryAI Enterprise Asset Management"
2. Shorthand for UI: "CherryAI EAM"
3. Page title format: "CherryAI EAM — <PageName>"

**Rationale:**
- Short name fits sidebar and header better
- Formal name used in legal/about pages
- Consistent format improves brand recognition

---

### Decision: Tier Naming Standard

**Context:** Need product tier names for pricing and feature differentiation.

**Decision:**
1. CherryAI EAM Launchpad — Small business tier
2. CherryAI EAM Autopilot — Mid-market tier with automation
3. CherryAI EAM Command Center — Enterprise tier with full features

**Rationale:**
- Names convey progression and capability level
- "Launchpad" = getting started, simple
- "Autopilot" = automation, hands-off operation
- "Command Center" = full control, enterprise scale

---

### Decision: LAB/DEMO Environment Safety Policy

**Context:** Prevent accidental data modifications in demo/production environments.

**Decision:**
1. APP_ENVIRONMENT env var controls mode: LAB, DEMO, or auto-detect from DB name
2. LAB = development sandbox, seeding allowed with guards
3. DEMO = read-only demonstration, no seeding permitted
4. PROD = production, fully locked
5. Visual banner on every page shows current environment
6. Footer displays masked database host/name for verification

**Rationale:**
- Clear visual indicators prevent environment confusion
- Multi-layer protection ensures data safety
- Override mechanism allows intentional demo refreshes when needed

---

## 2026-01-21: Commanding Demo V1 - Sprint 1

### Decision: LAB/DEMO Environment Separation

**Context:** Need to prevent accidental data modifications in production/demo environments during development and demos.

**Decision:**
1. Use `ASPNETCORE_ENVIRONMENT` as primary gate (must be "Development" for any seeding)
2. Detect environment profile from database name patterns:
   - Contains "lab" → LAB (development sandbox)
   - Contains "demo" → DEMO (protected demo environment)
   - Contains "prod" → PROD (production, fully locked)
3. Require `ALLOW_DEMO_SEED=true` to override protection in DEMO/PROD
4. Display clear environment banner in UI to prevent confusion

**Rationale:**
- Multi-layer protection prevents accidental data loss
- Visual banner makes environment immediately obvious
- Override mechanism allows intentional demo refreshes

**Constraints:**
- Never touch DEMO env or DEMO DB during development
- No destructive schema actions
- Seeds only when ALLOW_DEMO_SEED=true in LAB

---

### Decision: Seed Pack Architecture

**Context:** Need realistic demo data that can be loaded quickly without duplicates.

**Decision:**
1. Three preset packs: Small (25 assets), Mid-Size (100), Enterprise (321)
2. Natural key-based idempotent upserts (check by code/number before insert)
3. Transaction-wrapped execution with rollback on failure
4. Guard check at execution time (not just UI time)

**Rationale:**
- Idempotent seeding allows safe re-runs
- Natural keys prevent duplicate business entities
- Transaction wrapper ensures data consistency

---

### Decision: Dashboard KPIs Selection

**Context:** Need key metrics visible on dashboard for maintenance operations.

**Decision:**
1. Work Order Backlog - count of open/in-progress work orders
2. PM Compliance % - scheduled PM events completed on time vs total
3. MTTR (Mean Time To Repair) - average hours from WO open to close

**Rationale:**
- These are industry-standard EAM KPIs
- Can be calculated from existing MaintenanceEvent data
- Provides immediate value for demo presentations

---

### Decision: MaintenanceSchedule UI Pattern

**Context:** Need list/detail views for preventive maintenance schedules.

**Decision:**
1. Use Premium Hero Design System for consistency
2. List page with EnhancedGrid (search, sort, export)
3. Detail page showing schedule info + generated work orders

**Rationale:**
- Consistent with existing page patterns
- Leverages existing reusable components
- Familiar UX for users

---

## Template for Future Entries

## 2026-01-22: PM Templates UX Refactor (Sprint 7 Closeout)

### Decision: PM Templates Moved from Modals to Full-Page Create/Edit

**Context:** The PM Templates feature used modal dialogs for create/edit operations. This caused several issues:
- Multi-section form complexity made modals cramped and difficult to navigate
- Double-modal bugs appeared when clicking edit buttons
- Translucent/ghost card styling issues
- Text clipping on toggle labels ("Requires" appeared as "PRQUIRES")

**Decision:**
1. Convert PM Templates from modal-based to full-page create/edit flow
2. Use single unified page (`/Admin/PMTemplateEdit/{id?}`) for both create (no ID) and edit (with ID)
3. Organize form into logical sections: Basic Information, Scheduling, Cost Estimates, Safety Requirements, Status
4. Use toggle switches for boolean fields (Shutdown, LOTO, Active)
5. Include sidebar with Status and Danger Zone (delete) cards
6. Dynamic trigger field visibility based on Calendar/Meter selection
7. Remove old modal handlers from PMTemplates.cshtml.cs

**Rationale:**
- Full page provides more room for multi-section complex forms
- Eliminates modal layering/transparency issues
- Easier future expansion (assignments, schedules, linked assets)
- Consistent with Unified Asset Page pattern
- Reduced UI brittleness and better brand consistency

**Constraints:**
- Preserve all existing data (no schema changes)
- Maintain role-based access (Admin, Accountant)
- Keep TempData success/error message pattern

**Date:** 2026-01-22

---

## 2026-01-22: Tenant/Company/Site Control Plane

### Decision: Multi-Tenant Architecture with Deployment Mode Toggle

**Context:** CherryAI EAM needs to support both single-tenant (on-premise) and multi-tenant (SaaS) deployment models without maintaining separate codebases.

**Decision:**
1. Introduce a `Tenant` entity as the top-level organizational unit (Tenant -> Company -> Site -> Location -> Asset)
2. Add `DeploymentMode` configuration: `SingleTenant` or `MultiTenant`
3. Create `TenantContext` service for deterministic tenant resolution
4. In SingleTenant mode: use configured defaults, no headers required
5. In MultiTenant mode: resolve via X-CherryAI-Tenant header (required), optional X-CherryAI-Company and X-CherryAI-Site
6. All new records stamped with TenantId/CompanyId/SiteId from TenantContext
7. Integration mappings and idempotency keys are tenant-scoped

**Rationale:**
- Single codebase serves both deployment models
- TenantContext provides single source of truth for scoping
- Header-based resolution is stateless and scales horizontally
- Fail-safe behavior differs by mode (400 in MultiTenant, defaults in SingleTenant)
- Tenant-scoped idempotency prevents cross-tenant collisions

**Constraints:**
- Schema changes additive only (no dropping columns)
- Existing data assigned to default tenant during migration
- Webhook secrets and payloads exempt from uppercase mutation
- Background services use explicit tenant context, not request headers

**Date:** 2026-01-22

---

## 2026-01-22: Sprint 8 — Work Orders UX + Data Clarity Hardening

### Decision: Work Order Origin Classification System

**Context:** Users couldn't easily tell if a Work Order came from Smart Assist analysis, PM Schedule automation, or manual creation. This caused confusion about data provenance and trust issues with the UI.

**Decision:**
1. Created `WorkOrderOriginService` with deterministic classification logic:
   - **Smart Assist**: WorkRequest with `GeneratedWorkOrderId` and `IsAIAssisted=true`
   - **PM Schedule**: `CustomField1` starts with "PMTA:" OR `Type=Preventative` with `RecurrenceIntervalDays`
   - **Manual**: All other work orders
2. Added origin badges to Work Orders list page (Source column)
3. Added origin badges to Work Order details page (hero tags section)
4. No database schema changes required — uses existing fields heuristically

**Rationale:**
- Provides clear data provenance without schema migrations
- Deterministic classification ensures consistent labeling
- Batch lookup (`GetOriginsForEventsAsync`) optimizes list page performance
- Classification logic centralized in single service for consistency

### Decision: Work Order Empty States UX

**Context:** Empty sections (Operations, Parts, Labor) looked like missing/broken data rather than "not entered yet" states.

**Decision:**
1. All empty sections now show clear empty state messages with context-aware help text
2. Operations empty state varies based on origin (PM vs manual)
3. Parts and Labor sections added with explicit empty states
4. Filter reset action added when no work orders match current filters

**Rationale:**
- Eliminates "looks broken" user perception
- Provides helpful guidance on next steps
- Consistent empty state pattern across all sections

### Decision: Smoke Tests #11 and #12

**Context:** Need automated verification that origin classification is deterministic and that empty states render without errors.

**Decision:**
1. Smoke Test #11: Origin Classification — Creates Smart Assist, PM, and Manual work orders, verifies classification
2. Smoke Test #12: Empty States Render Safety — Creates WO with zero operations/parts/labor, verifies data loads correctly

**Constraints:**
- Both tests use transaction rollback (zero DB pollution)
- LAB environment only
- All 12 tests must pass for quality gate

**Date:** 2026-01-22

---

## 2026-01-22: Item Master Cross-Reference v1 (Sprint 11)

### Decision: Three-Way Part Number Resolution

**Context:** Manufacturing organizations use multiple part number systems - their internal part numbers, manufacturer part numbers (MPN), and vendor part numbers (VPN). Need unified lookup across all three.

**Decision:**
1. Resolution priority: Internal PN → MPN → VPN (exact match only)
2. Optional vendor filter for VPN lookups
3. Match origin tracking to identify which identifier type matched
4. No fuzzy matching in v1 to ensure deterministic results

**Rationale:**
- Industry standard resolution order (local first, then external references)
- Exact matching prevents ambiguous results
- Vendor filter enables PO-context lookups
- Origin tracking aids troubleshooting and auditing

---

### Decision: Item Revision Control Pattern

**Context:** Items need revision control matching PMTemplate revision pattern for consistency.

**Decision:**
1. Reuse RevisionStatus enum (Draft/Released/Obsolete)
2. Auto-generate revision codes (A, B, C... AA, AB...)
3. Supersession chain via SupersedesItemRevisionId
4. Item.CurrentReleasedRevisionId pointer pattern
5. Released revisions are immutable (cannot edit via UpdateDraftAsync)

**Rationale:**
- Consistent pattern with PM Template revisions
- Audit trail via supersession chain
- Current pointer simplifies lookups
- Immutability ensures compliance

---

### Decision: Cross-Reference Unique Constraints

**Context:** Need to prevent duplicate part numbers while allowing same VPN across different vendors.

**Decision:**
1. ItemManufacturerPart: Unique(ItemId, ManufacturerId, MfrPartNumber)
2. VendorItemPart: Unique(VendorId, VendorPartNumber)
3. Manufacturer: Unique(TenantId, Code)

**Rationale:**
- Same MPN can be sold by different manufacturers (rare but possible)
- Same VPN can exist for different vendors (each vendor has own catalog)
- Tenant-scoped manufacturer codes for multi-tenant isolation

**Date:** 2026-01-22

---

## 2026-01-22: Procurement-Grade Parts v1 (Sprint 12)

### Decision: Approved Vendor List (AVL) Architecture

**Context:** Manufacturing organizations need to track which vendors are approved to supply specific items.

**Decision:**
1. ItemApprovedVendor entity with TenantId/CompanyId/SiteId scoping
2. ApprovalStatus enum: Approved, Conditional, Blocked
3. Exactly one preferred vendor per item (enforced by service, not DB constraint)
4. Unique constraint on (TenantId, ItemId, VendorId)

**Rationale:**
- Matches industry AVL practices
- Service enforcement of single-preferred is simpler to maintain
- Status enum covers common approval states
- Tenant/Company/Site scoping for multi-tenant flexibility

---

### Decision: Alternates/Substitutes Design

**Context:** Items may have multiple alternates with different suitability levels.

**Decision:**
1. ItemAlternate entity with Rank-based ordering
2. AlternateType enum: Substitute, Equivalent, Upgrade, Downgrade
3. IsApproved flag to control which alternates are valid
4. GetBestAlternate: approved only, lowest rank, tie-break by AlternateItemId

**Rationale:**
- Rank enables deterministic selection
- Type classification aids decision-making
- Approval flag provides control without deletion
- Stable tie-break ensures predictable results

---

### Decision: Supersession Chain Design

**Context:** Parts get replaced over time; need to track replacement chains.

**Decision:**
1. ItemSupersession entity with OldItemId -> NewItemId
2. Unique(TenantId, OldItemId) - one direct successor per old item
3. Cycle prevention via graph walk before creation
4. ResolveCurrentItem follows chain to terminal

**Rationale:**
- Simple chain model (no branching in v1)
- Cycle prevention ensures data integrity
- Resolution helper simplifies purchasing lookups

**Date:** 2026-01-22
