# UI Conformance Allowlist

This document defines exceptions to the standard UI conformance rules for CherryAI EAM. Pages listed here are permitted to deviate from the standard layout and component patterns for documented reasons.

## Purpose

The UI Conformance Smoke Tests (Tests 57-60) enforce consistent UI patterns across the application. This allowlist documents intentional exceptions to prevent test failures while maintaining clarity about why each exception exists.

**Migrated Pages (now enforced):**
- `Pages/Maintenance/Schedules.cshtml` - Migrated to EnhancedGrid (Jan 2026)
- `Pages/Maintenance/Assignments/Index.cshtml` - Migrated to EnhancedGrid (Jan 2026)

## Layout Exceptions (Test 57)

Pages that may use alternative layouts instead of `_ModernLayout`:

| Page | Layout | Reason |
|------|--------|--------|
| `Pages/Account/Login.cshtml` | Minimal | Authentication pages require minimal UI without navigation |
| `Pages/Account/Logout.cshtml` | Minimal | Logout action page |
| `Pages/Account/AccessDenied.cshtml` | Minimal | Access denied requires minimal chrome |
| `Pages/Error.cshtml` | Simple | Error pages need to work even when layout fails |
| `Pages/Reports/Print.cshtml` | Print | Print-specific layout without navigation or chrome |

## DataGrid Exceptions (Test 58)

Pages where simple tables are acceptable (without `data-table` class or `enhanced-grid.js`):

| Page | Table Type | Reason |
|------|------------|--------|
| `Pages/Admin/SystemSettings.cshtml` | Simple | Small config dataset, no search/sort needed |
| `Pages/Admin/Diagnostics.cshtml` | Simple | Developer diagnostics tool |
| `Pages/Help/Glossary.cshtml` | Simple | Read-only reference table |
| `Pages/Help/Implementation.cshtml` | Simple | Documentation table |
| `Pages/CCA/Settings.cshtml` | Simple | Small config dataset |
| `Pages/Admin/Integrations/Inbound.cshtml` | Simple | Internal admin tool |
| `Pages/Admin/Integrations/Index.cshtml` | Simple | Internal admin tool |
| `Pages/Admin/Integrations/Maps.cshtml` | Simple | Internal admin tool |
| `Pages/Admin/Webhooks/Deliveries.cshtml` | Simple | Internal admin tool |
| `Pages/WorkOrders/Details.cshtml` | Embedded | Detail page with inline tables |

## Adding New Exceptions

To add a new exception:

1. Document the reason in this file
2. Add the page path to the appropriate allowlist in `Services/Testing/SmokeTestRunner.cs`:
   - For layout exceptions: `RunUILayoutConformanceTest()` allowlist
   - For DataGrid exceptions: `RunDataGridConformanceTest()` simpleTableAllowed or other lists
3. Commit both changes together to maintain documentation consistency

## Standard Components

All non-allowlisted pages MUST use:

### Layout
- `_ModernLayout` (set via `_ViewStart.cshtml` or explicit `Layout = "_ModernLayout"`)

### Page Structure (when applicable)
- `_ScreenHeader.cshtml` - Standard page header with title, subtitle, status, KPIs, and actions
- `_EmptyState.cshtml` - For empty data states
- `_SectionCard.cshtml` - For content sections
- `_KpiStrip.cshtml` - For KPI displays in hero sections

### Data Tables
- CSS class: `data-table` for styled tables
- CSS class: `enhanced-grid` for tables with search/sort/export
- Script: `enhanced-grid.js` for interactive grid functionality

## Verification

Run Smoke Tests 57 and 58 to verify conformance:
- Test 57: UI Layout Conformance
- Test 58: DataGrid Conformance

These tests run as part of the LAB-only smoke test harness at `/Admin/SmokeTests`.

## UI Hygiene Exceptions (Test 59)

Pages where inline `<style>` blocks are acceptable:

| Page | Reason |
|------|--------|
| `Pages/Account/Login.cshtml` | Auth flow requires minimal styling |
| `Pages/Account/Logout.cshtml` | Auth flow requires minimal styling |
| `Pages/Account/AccessDenied.cshtml` | Auth flow requires minimal styling |
| `Pages/Error.cshtml` | Must work when layout fails |
| `Pages/Reports/Print.cshtml` | Print-specific layout |
| `Pages/Shared/_ModernLayout.cshtml` | Layout defines global styles |
| `Pages/Help/TaskGuide.cshtml` | Documentation styling |
| `Pages/Help/ConceptTopic.cshtml` | Documentation styling |
| `Pages/Help/Index.cshtml` | Documentation styling |
| `Pages/Help/Implementation.cshtml` | Documentation styling |
| `Pages/Help/Glossary.cshtml` | Documentation styling |

## Hero Action Contract Exceptions (Test 60)

Pages exempt from hero-tags/hero-btns requirement:

| Page | Reason |
|------|--------|
| `Pages/Admin/DataImport.cshtml` | Internal admin tool |
| `Pages/Admin/DemoData.cshtml` | Internal admin tool |
| `Pages/Admin/EnvironmentStatus.cshtml` | Internal admin tool |
| `Pages/Admin/PMScheduleEdit.cshtml` | Edit form, not a list |
| `Pages/Admin/SmokeTests.cshtml` | Internal admin tool |
| `Pages/Admin/Webhooks/Index.cshtml` | Internal admin tool |
| `Pages/Maintenance/WorkRequests/Details.cshtml` | Detail page, not a list |

## Module CSS Files

Styles that were previously inline have been migrated to dedicated module CSS files loaded globally via `_ModernLayout.cshtml`:

| Module | Purpose | Pages Using |
|--------|---------|-------------|
| `wwwroot/css/modules/dashboard.css` | Dashboard, alerts, activity feed, timeline, quick actions | Index.cshtml |
| `wwwroot/css/modules/assets.css` | Asset hero, wizard steps, transfer cards, disposal forms, improvements | Asset.cshtml, Transfer.cshtml, Dispose.cshtml, Improve.cshtml |
| `wwwroot/css/modules/finance.css` | Books/Journals detail lists, method badges, filter forms | Books/Index.cshtml, Books/Details.cshtml, Journals/Index.cshtml |
| `wwwroot/css/modules/reports.css` | Report builder layout, tabs, compliance stats | Reports/Builder.cshtml, Reports/Compliance.cshtml |

## Migrated Pages (January 2026)

The following pages had inline `<style>` blocks removed and styles migrated to module CSS:

- `Pages/Index.cshtml` - Dashboard styles (~310 lines)
- `Pages/Assets/Asset.cshtml` - Asset hero and transaction styles (~750 lines)
- `Pages/Assets/Transfer.cshtml` - Wizard and location card styles
- `Pages/Assets/Dispose.cshtml` - Disposal form styles
- `Pages/Assets/Improve.cshtml` - Cost preview and history styles
- `Pages/Books/Index.cshtml` - Method badge and inactive row styles
- `Pages/Books/Details.cshtml` - Detail list and option item styles
- `Pages/Journals/Index.cshtml` - Filter form styles
- `Pages/Reports/Builder.cshtml` - Builder layout and field list styles
- `Pages/Reports/Compliance.cshtml` - Tab and stats row styles

## Migrated Shared Partials (January 2026)

The following shared partials had embedded `<style>` blocks removed and styles migrated to `wwwroot/css/premium-components.css`:

| Partial | Styles Migrated | Target CSS |
|---------|-----------------|------------|
| `Pages/Shared/_BackLink.cshtml` | `.back-link` (27 lines) | `premium-components.css` |
| `Pages/Shared/_AssetMaintenanceHeader.cshtml` | `.module-header`, `.module-breadcrumbs`, `.status-pill`, `.module-pills` (~165 lines) | `premium-components.css` |

These partials are now ADR-004 compliant and no longer contain embedded `<style>` blocks.

## Last Updated

January 2026 - Migrated `<style>` blocks from `_BackLink.cshtml` and `_AssetMaintenanceHeader.cshtml` to `premium-components.css`. Added module CSS files and migrated inline styles from 10 operational pages. Expanded to include UI Hygiene (Test 59) and Hero Action Contract (Test 60) exceptions.
