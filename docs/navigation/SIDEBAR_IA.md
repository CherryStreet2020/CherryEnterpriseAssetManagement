# Sidebar Information Architecture

This document explains the grouping rationale and naming conventions for the CherryAI EAM sidebar navigation.

## Grouping Rationale

The sidebar groups follow an enterprise EAM workflow lifecycle:

### 1. Overview
Single "Dashboard" link. The entry point for all users. Shows KPIs, alerts, and quick metrics scoped to the selected organization node.

### 2. Work
Groups all work management pages following the EAM work lifecycle: Request > Plan > Schedule > Execute > Close.

- **Work Requests**: Intake point for maintenance needs
- **Work Orders**: Execution cockpit for approved work
- **Planning & Scheduling**: PM schedule management
- **PM Program**: Technician assignments and preventive maintenance program oversight

Rationale: Maintenance practitioners spend 80%+ of their time in these screens. Grouping them together reduces clicks and mirrors the workflow sequence.

### 3. Assets
The asset register and related master data.

- **Asset Registry**: Core asset list and detail views
- **Locations**: Functional locations (buildings, lines, areas) — never called "Sites" here (Sites are organizational units)
- **Meters**: Runtime/usage meters attached to assets
- **Condition & Health**: Condition assessments and health scores
- **Physical Inventory**: Periodic physical asset counts
- **Bulk Operations**: Mass updates, imports, tag changes

Rationale: Assets are the central entity in EAM. This group covers the asset lifecycle from registration through condition tracking.

### 4. Materials
Inventory management, procurement, and vendor management.

- **Item Master**: Parts catalog with MPNs, VPNs, alternates
- **Warehouses**: Stocking locations (renamed from "Storerooms" for clarity)
- **Stock Levels / Transactions**: Current on-hand and movement history
- **Vendors, POs, Receipts, Invoices**: Procure-to-pay chain

Rationale: Materials and procurement are tightly coupled in EAM. Practitioners look up parts, check stock, create POs, and receive goods in a single workflow.

### 5. Finance
Financial tracking, depreciation, tax, and capital projects.

- **CIP Projects / Cost Analysis**: Construction-in-progress tracking
- **Depreciation Books**: Book/tax depreciation schedules
- **Journal Entries**: GL postings
- **Tax modules**: US MACRS/179 and Canadian CCA
- **Cost Analytics**: Financial reporting hub

Rationale: Finance users need a dedicated section for depreciation, capitalization, and tax compliance.

### 6. Reports
Cross-functional reporting and export.

Rationale: Reports span all modules, so they get their own top-level group rather than being scattered across functional groups.

### 7. Admin
System administration, visible only to Admin-role users.

- **Organization & Sites**: Multi-company/site hierarchy
- **Users & Roles**: Identity and access management
- **Lookups**: Reference data tables
- **Integrations**: API and webhook configuration
- **PM Templates**: Preventive maintenance template library
- **Audit Log**: Change tracking

Rationale: Admin functions are separated to reduce clutter for non-admin users and to provide clear access control boundaries.

## Naming Rules

| Rule | Example | Anti-pattern |
|---|---|---|
| Use plural nouns for list pages | "Work Orders", "Vendors" | "Work Order List" |
| Use action verbs for create pages | "Create Work Request" | "New Work Request Form" |
| Prefer industry-standard EAM terms | "Work Order", "PM Schedule" | "Maintenance Task", "Recurring Job" |
| "Locations" = functional locations | "Locations" under Assets | "Functional Locations" |
| "Warehouses" = stocking points | "Warehouses" under Materials | "Storerooms", "Stockrooms" |
| "Sites" = organizational units | "Organization & Sites" under Admin | "Facilities" |
| No redundant module prefix in label | "Work Orders" (not "Maintenance Work Orders") | "Maintenance Work Orders" |
| Parenthetical clarifiers for acronyms | "US Tax (MACRS/179)" | "MACRS" alone |

## Visibility Rules

| Group | Visibility Condition |
|---|---|
| Overview | Always |
| Work | `company.EnableWorkOrders == true` |
| Assets | Always |
| Materials | `company.EnableInventory == true` |
| Finance | Always |
| Reports | Always |
| Admin | User has Admin role |

## Collapse Behavior

- Accordion groups are mutually exclusive by default (one open at a time)
- Active-route group auto-expands on page load
- Collapsed sidebar shows icon-rail (64px) with tooltips on hover
- Sidebar state persists in localStorage key `cherryai_sidebar_collapsed`
