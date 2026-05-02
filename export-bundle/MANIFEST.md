# CherryAI Enterprise Asset Management — Project State Export
# Generated: 2026-02-24
# Purpose: Full context transfer for ChatGPT continuation

## What Was Just Completed (Session Summary)

### T003: FK-Bound Dropdown Migration (Phase 4)
All 11 page models were updated to use `GetSelectListByIdAsync` for FK-bound dropdown fields. The pattern:
1. **OnGet**: Load dropdown options using `GetSelectListByIdAsync(tenantId, companyId, lookupKey, selectedLookupValueId)` — this populates `<select>` lists with `LookupValue.Id` as the option value
2. **OnPost**: Resolve the submitted `LookupValueId` → fetch `LookupValue.Code` → parse to enum → persist **both** the FK (`*LookupValueId` column) and the legacy enum field for backward compatibility

### T003 Pages & FK Fields
| Page Model | FK-Bound Fields |
|---|---|
| `Pages/Assets/Asset.cshtml.cs` | StatusLookupValueId, AssetTypeLookupValueId, AssetPriorityLookupValueId, ConditionLookupValueId, DepreciationMethodLookupValueId |
| `Pages/Assets/Dispose.cshtml.cs` | DisposalReasonLookupValueId |
| `Pages/Assets/Transfer.cshtml.cs` | TransferReasonLookupValueId |
| `Pages/Books/Edit.cshtml.cs` | BookTypeLookupValueId, MethodLookupValueId, ConventionLookupValueId, TaxJurisdictionLookupValueId, FrequencyLookupValueId |
| `Pages/Admin/Sites.cshtml.cs` | TypeLookupValueId, StatusLookupValueId |
| `Pages/CIP/Details.cshtml.cs` | CipCostTypeLookupValueId, StatusLookupValueId |
| `Pages/Maintenance/Details.cshtml.cs` | TypeLookupValueId, PriorityLookupValueId, StatusLookupValueId |
| `Pages/Purchasing/Index.cshtml.cs` | POTypeLookupValueId, StatusLookupValueId |
| `Pages/Admin/Requisitions.cshtml.cs` | StatusLookupValueId, PriorityLookupValueId |
| `Pages/AccountsPayable/Details.cshtml.cs` | StatusLookupValueId |
| `Pages/Materials/ItemEdit.cshtml.cs` | TypeLookupValueId, StatusLookupValueId, CostMethodLookupValueId, TrackingTypeLookupValueId |

### T004: Org Tree & Analytics (Also Completed)
- UUID-based `OrgNode` hierarchy: Holding → Company → Site → Location
- `OrgController` with `/api/org/tree` and `/api/org/analytics` endpoints
- `OrgScopeMiddleware` sets tenant context from selected org node
- `DetailController` with 21 entity detail endpoints
- Full proof bundle with 4 gate scripts

## Technology Stack
- **Framework**: ASP.NET Core 8.0 with Razor Pages
- **Database**: PostgreSQL (Neon-backed via Replit)
- **ORM**: Entity Framework Core
- **CSS**: Custom design system with Tailwind-inspired tokens
- **AI**: OpenAI integration for AI Assistant

## Project Structure (Key Directories)

```
/
├── Abs.FixedAssets.csproj       # Project file
├── Program.cs                    # App entry point & DI registration
├── replit.md                     # Project memory/preferences
├── Controllers/                  # API controllers
│   ├── AnalyticsController.cs
│   ├── AssetsApiController.cs
│   ├── DetailController.cs       # 21 entity detail endpoints
│   ├── OrgController.cs          # Org tree + analytics API
│   └── ...
├── Data/
│   ├── AppDbContext.cs           # EF Core DbContext
│   └── Seed.cs                   # Demo data seeder
├── Middleware/
│   ├── OrgScopeMiddleware.cs     # Org node → tenant context
│   └── TenantContextMiddleware.cs
├── Migrations/                   # EF Core migrations (chronological)
├── Models/                       # Domain models (65+ entities)
│   ├── Asset.cs, Book.cs, Company.cs, ...
│   ├── Enums.cs                  # All enum definitions
│   ├── LookupType.cs             # Lookup infrastructure
│   ├── LookupValue.cs
│   ├── OrgNode.cs                # Org hierarchy node
│   └── ...
├── Pages/                        # Razor Pages (100+ pages)
│   ├── Assets/                   # Asset CRUD + lifecycle
│   ├── Books/                    # Depreciation books
│   ├── Admin/                    # Admin pages (Sites, Users, Lookups, etc.)
│   ├── CIP/                      # Capital improvement projects
│   ├── Maintenance/              # Work orders & maintenance
│   ├── Materials/                # Item master & inventory
│   ├── Purchasing/               # Purchase orders
│   ├── AccountsPayable/          # Invoices & payments
│   └── ...
├── Services/                     # Business logic services
│   ├── Lookups/
│   │   ├── ILookupService.cs     # Lookup service interface
│   │   └── LookupService.cs      # Cached lookup implementation
│   ├── Items/                    # Item-related services
│   ├── Maintenance/              # Maintenance services
│   ├── Seeding/                  # Versioned seed pipelines
│   └── ...
├── wwwroot/                      # Static assets (CSS, JS, images)
├── seed/reference-data/          # JSON seed files for lookups
├── scripts/                      # Gate/proof scripts (Python)
├── docs/                         # Documentation (40+ files)
│   ├── README.md                 # Documentation index
│   ├── Architecture.md
│   ├── adr/                      # Architecture Decision Records
│   └── ...
└── tools/                        # Audit/CI tools
```

## Database Schema Highlights
- **Lookup Infrastructure**: `LookupType` + `LookupValue` tables with tenant/company scoping, JSONB metadata
- **64 lookup types** with 350+ values seeded from JSON files
- **FK linkage**: 5+ domain models have `*LookupValueId` FK columns pointing to `LookupValues`
- **OrgNode**: UUID PK, self-referencing `ParentId`, `NodeType` enum (Holding/Company/Site/Location), `EntityId` linking to Company/Site/Location
- **Multi-tenant**: `TenantId` on most entities, company-scoped data access

## Key Patterns

### Dual-Write FK Pattern (T003)
```csharp
// OnGet: Load dropdown with LookupValueId as option value
Options = await _lookupService.GetSelectListByIdAsync(tenantId, companyId, "LookupKey", entity.FKLookupValueId, "");

// OnPost: Resolve FK → enum, persist both
var lv = await _lookupService.GetValueByIdAsync(tenantId, companyId, submittedLookupValueId);
if (lv != null && int.TryParse(lv.Code, out var enumVal))
    entity.EnumField = (MyEnum)enumVal;
entity.FKLookupValueId = submittedLookupValueId;
```

### Tenant Context Flow
```
Request → TenantContextMiddleware (reads X-Tenant-Id header or cookie)
        → OrgScopeMiddleware (reads X-OrgNode-Id, sets CompanyId/SiteId from OrgNode)
        → Page/Controller (uses ITenantContext for scoped queries)
```

### Seed Pipeline Architecture
Versioned seed packages with idempotent upserts:
- SystemReferenceSeedPipeline → OrgAndFinanceSeedPipeline → VendorsAndPartsSeedPipeline → EamExecutionMastersSeedPipeline → DemoPackV1/V2

## Files Included in This Bundle

### Core Infrastructure
- `Program.cs` — DI registration, middleware pipeline, app configuration
- `Abs.FixedAssets.csproj` — Project dependencies
- `Data/AppDbContext.cs` — Full DbContext with all DbSets and model configuration

### All Models (65+ files)
- Complete domain model layer including FK properties

### All Services
- Lookup service (ILookupService/LookupService) — central to T003
- All business services

### All Controllers
- DetailController (21 endpoints), OrgController, AnalyticsController, etc.

### All Page Models & Views (T003 pages)
- All 11 T003 page models with their .cshtml views

### Migrations
- All EF Core migrations including lookup table creation and FK backfill

### Documentation
- Architecture docs, ADRs, reference data registry, route registry

### Scripts & Gates
- All Python gate scripts for proof bundle validation

### Seed Data
- JSON seed files for lookup types and values
