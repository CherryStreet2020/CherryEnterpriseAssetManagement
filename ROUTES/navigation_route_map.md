# CherryAI EAM — Navigation Route Map

## Canonical Routes (v3)

### Overview
| Label | Canonical Route | Group |
|---|---|---|
| Dashboard | `/` | Overview |

### Assets (MAIN)
| Label | Canonical Route | Legacy Route | Group |
|---|---|---|---|
| Asset Registry | `/Assets` | — | Assets |
| Asset Detail | `/Assets/Asset?id={id}` | — | Assets |
| Locations | `/Assets/Locations` | `/Admin/Locations` (301) | Assets |
| Bulk Operations | `/BulkOperations` | — | Assets |

### Finance (MAIN)
| Label | Canonical Route | Legacy Route | Group |
|---|---|---|---|
| Journals | `/Journals` | — | Finance |
| Depreciation Books | `/Books` | — | Finance |
| GL Accounts | `/Books/GlAccounts` | `/Admin/GlAccounts` (301) | Finance |
| Accounts Payable | `/AccountsPayable` | — | Finance |
| Reports | `/Reports/ReportHub` | — | Finance |

### Materials (MAIN)
| Label | Canonical Route | Legacy Route | Group |
|---|---|---|---|
| Inventory | `/Materials/Items` | — | Materials |
| Warehouses | `/Inventory` | — | Materials |
| Vendors | `/Materials/Vendors` | `/Admin/Vendors` (301) | Materials |
| Purchase Orders | `/Purchasing` | — | Materials |
| Receipts | `/Receiving` | — | Materials |

### Projects (MAIN)
| Label | Canonical Route | Group |
|---|---|---|
| CIP Projects | `/CIP` | Projects |
| Capitalizations | `/CIP/Costs` | Projects |
| Cost Details | `/CIP/CostDetails` | Projects |
| Cost Type Details | `/CIP/CostTypeDetails` | Projects |
| Cost Analytics | `/CIP/PartyDrilldown` | Projects |

### Work (MAIN)
| Label | Canonical Route | Legacy Route | Group |
|---|---|---|---|
| Requests | `/Maintenance/WorkRequests` | — | Work |
| Work Orders | `/Maintenance` | — | Work |
| Work Order Detail | `/Maintenance/Details/{id}` | — | Work |
| Planning & Scheduling | `/Maintenance/Schedules` | — | Work |
| PM Schedule Edit | `/Maintenance/PMScheduleEdit` | `/Admin/PMScheduleEdit` (301) | Work |
| PM Program | `/Maintenance/PMTemplates` | `/Admin/PMTemplates` (301) | Work |

### Admin (SYSTEM)
| Label | Canonical Route | Group |
|---|---|---|
| Organization & Sites | `/Admin/Sites` | Admin |
| Users & Roles | `/Admin/Users` | Admin |
| Lookups | `/Admin/Lookups` | Admin |
| Integrations | `/Admin/Integrations` | Admin |
| Data Import | `/Admin/DataImport` | Admin |
| Audit Log | `/Admin/AuditLog` | Admin |
| System Settings | `/Admin/SystemSettings` | Admin |

### Footer
| Label | Route |
|---|---|
| Help Center | `/Help` |
| Sign In | `/Account/Login` |

## Redirect Table (HTTP 301 Permanent)

| Legacy Route | Canonical Route | Method |
|---|---|---|
| `/Admin/Locations` | `/Assets/Locations` | Middleware + AddPageRoute |
| `/Admin/Vendors` | `/Materials/Vendors` | Middleware + AddPageRoute |
| `/Admin/PMTemplates` | `/Maintenance/PMTemplates` | Middleware + AddPageRoute |
| `/Admin/PMScheduleEdit` | `/Maintenance/PMScheduleEdit` | Middleware + AddPageRoute |
| `/Admin/GlAccounts` | `/Books/GlAccounts` | Middleware + AddPageRoute |

All legacy routes issue HTTP 301 permanent redirects via middleware in Program.cs (before UseStaticFiles/UseRouting). AddPageRoute() also registers dual-path routing for the same Razor Page.

## IA Drift Policy
- Operational pages (outside /Admin section) MUST NOT contain links to /Admin/* routes
- The sidebar Admin section is the ONLY location where /Admin/* links are permitted
- Playwright Gate 02 enforces this by scanning 13+ operational pages for /Admin/ references in main content
- Playwright Gate 04 verifies all 5 redirects via APIRequestContext with maxRedirects:0
