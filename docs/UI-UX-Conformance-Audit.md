# UI/UX Conformance Audit

## Overview
This document tracks the repo-wide purge of inline `<style>` blocks from Razor Pages and consolidation into module CSS files.

**Audit Date:** January 2026  
**Status:** COMPLETE - Zero inline styles on operational pages

---

## Summary

| Metric | Count |
|--------|-------|
| Pages audited | 39 |
| Inline styles removed | 36 |
| Module CSS files created | 15 |
| Allowed exceptions | 3 |

---

## Module CSS Files

All module CSS files are loaded globally via `Pages/Shared/_ModernLayout.cshtml`:

| Module File | Purpose |
|-------------|---------|
| `dashboard.css` | Dashboard, alerts, activity feed, timeline, quick actions |
| `assets.css` | Asset hero, wizard steps, transfer cards, disposal forms |
| `finance.css` | Books/Journals detail lists, method badges, filters |
| `reports.css` | Report builder, tabs, compliance stats, depreciation schedule |
| `help.css` | Task steps, tips, prerequisites, quick-start cards |
| `tax.css` | Convention cards for US Tax pages |
| `inventory.css` | Content grids, detail items, action cards |
| `maintenance.css` | Maintenance type cards, stat variants |
| `cip.css` | Cost type cards, progress bars, CIP details |
| `admin.css` | Modal forms, filter forms, settings grids, tech cards |
| `api.css` | API alerts, code blocks, endpoint cards |
| `ai.css` | Chat container, messages, suggestions, input styles |
| `bulk-operations.css` | Modal, asset selector, bulk details grid |
| `purchasing.css` | Modal variants, PO details, line items |
| `workorders.css` | Operations grid, subpanels, summary cards, badges |

---

## Pages Migrated

### CIP Module
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/CIP/Costs.cshtml` | Flex, text utilities | `cip.css` |
| `Pages/CIP/Index.cshtml` | Badge, progress, cost types | `cip.css` |
| `Pages/CIP/Details.cshtml` | Content grid, budget stats | `cip.css` |
| `Pages/CIP/CostDetails.cshtml` | Form grid, summary stats | `cip.css` |

### Bulk Operations
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/BulkOperations/Index.cshtml` | Modal, asset selector, forms | `bulk-operations.css` |
| `Pages/BulkOperations/Details.cshtml` | Grid layout, clickable rows | `bulk-operations.css` |

### AI & API
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/AI/Index.cshtml` | Chat UI, messages, suggestions | `ai.css` |
| `Pages/API/Index.cshtml` | Alerts, code blocks | `api.css` |
| `Pages/API/Import.cshtml` | Alerts, import styling | `api.css` |

### Admin Module
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/Admin/Departments.cshtml` | Modal form grid | `admin.css` |
| `Pages/Admin/CostCenters.cshtml` | Modal form grid | `admin.css` |
| `Pages/Admin/AssetCategories.cshtml` | Modal form, sections | `admin.css` |
| `Pages/Admin/Manufacturers.cshtml` | Modal form, checkbox | `admin.css` |
| `Pages/Admin/Users.cshtml` | Modal, alerts, badges | `admin.css` |
| `Pages/Admin/AuditLog.cshtml` | Filter form, timestamp | `admin.css` |
| `Pages/Admin/Technicians.cshtml` | Breadcrumb, modal, tech cards | `admin.css` |
| `Pages/Admin/GlAccounts.cshtml` | Filter bar, modal form | `admin.css` |
| `Pages/Admin/ProjectManagers.cshtml` | Modal form, checkbox | `admin.css` |
| `Pages/Admin/ExchangeRates.cshtml` | Modal form, rates grid | `admin.css` |
| `Pages/Admin/Sites.cshtml` | Sites grid, site cards | `admin.css` |
| `Pages/Admin/Company.cshtml` | Settings layout, hierarchy | `admin.css` |
| `Pages/Admin/SystemSettings.cshtml` | Settings grid, toggles | `admin.css` |
| `Pages/Admin/Approvals.cshtml` | Settings grid, approval chain | `admin.css` |
| `Pages/Admin/DataImport.cshtml` | Grid utilities, seed cards | `admin.css` |
| `Pages/Admin/Diagnostics.cshtml` | Diagnostics hero, info cards | `admin.css` |
| `Pages/Admin/Outbox/Index.cshtml` | Tabs, badges, modal | `admin.css` |
| `Pages/Admin/DemoData.cshtml` | Grid utilities, spacing | `admin.css` |
| `Pages/Admin/PMTemplateEdit.cshtml` | PM template form, toggles | `admin.css` |
| `Pages/Admin/Items.cshtml` | Modal form grid | `admin.css` |

### Purchasing & Accounts Payable
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/Purchasing/Index.cshtml` | Modal, form, buttons | `purchasing.css` |
| `Pages/Purchasing/Details.cshtml` | PO layout, line items | `purchasing.css` |
| `Pages/AccountsPayable/Index.cshtml` | Text utilities | `purchasing.css` |

### Work Orders & Maintenance
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/WorkOrders/Details.cshtml` | Operations, summary, badges | `workorders.css` |
| `Pages/Maintenance/Index.cshtml` | Maintenance type cards | `maintenance.css` |
| `Pages/Maintenance/WorkRequests/Details.cshtml` | Status/priority badges | `workorders.css` |

### Reports & Help
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/Reports/ChartOfAccounts.cshtml` | Filter form | `reports.css` |
| `Pages/Reports/ReportHub.cshtml` | Report sections, cards | `reports.css` |
| `Pages/Reports/Builder.cshtml` | Report builder layout | `reports.css` |
| `Pages/Reports/Compliance.cshtml` | Compliance stats | `reports.css` |
| `Pages/Reports/DepreciationSchedule.cshtml .html` | Schedule table styling | `reports.css` |
| `Pages/Help/Index.cshtml` | Quick-start, task items | `help.css` |
| `Pages/Help/Tasks.cshtml` | Task steps, tips | `help.css` |

### Account Pages
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/Account/Login.cshtml` | Login form styling | `admin.css` |
| `Pages/Account/AccessDenied.cshtml` | Access denied styling | Uses `_ModernLayout` |

### Other Pages
| Page | Styles Description | Migrated To |
|------|-------------------|-------------|
| `Pages/Index.cshtml` | Dashboard styling | `dashboard.css` |
| `Pages/Assets/Asset.cshtml` | Asset hero, wizard | `assets.css` |
| `Pages/Assets/Transfer.cshtml` | Transfer cards | `assets.css` |
| `Pages/Assets/Dispose.cshtml` | Disposal forms | `assets.css` |
| `Pages/Assets/Improve.cshtml` | Improvement wizard | `assets.css` |
| `Pages/Books/Index.cshtml` | Books list | `finance.css` |
| `Pages/Books/Details.cshtml` | Book details | `finance.css` |
| `Pages/Journals/Index.cshtml` | Journals list | `finance.css` |
| `Pages/UsTax/Index.cshtml` | Convention cards | `tax.css` |
| `Pages/Inventory/Index.cshtml` | Action cards | `inventory.css` |
| `Pages/Inventory/List.cshtml` | Content grid | `inventory.css` |

---

## Allowed Exceptions (Shared Partials)

These files retain inline styles because they are shared partial components with unique, self-contained styles not suitable for global module CSS:

| File | Rationale |
|------|-----------|
| `Pages/Shared/_ModernLayout.cshtml` | Layout file containing dynamic theme styles and critical layout overrides |
| `Pages/Shared/_BackLink.cshtml` | Small partial with `.back-link` component styles unique to this partial |
| `Pages/Shared/_AssetMaintenanceHeader.cshtml` | Navigation component with `.module-header`, `.module-breadcrumbs`, `.module-pills` styles specific to this partial |

---

## Test Coverage

The following smoke tests enforce UI conformance:

| Test ID | Test Name | Status |
|---------|-----------|--------|
| 57 | Layout Hero Contract | PASS |
| 58 | DataGrid Conformance | PASS |
| 59 | UI Hygiene - No Inline Styles | PASS |
| 60 | Hero Action Contract | PASS |

---

## Maintenance Guidelines

1. **Never add inline `<style>` blocks to Pages/**
2. **Use module CSS files** organized by functional area
3. **Add new module CSS to `_ModernLayout.cshtml`** for global loading
4. **Use design system classes** from `premium-components.css` when possible
5. **If a partial truly needs unique styles**, document the exception in this file
