# CherryAI Enterprise Asset Management - Complete Project Status
## Date: February 24, 2026

---

## 1. TECHNOLOGY STACK

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 9.0 (Razor Pages) |
| ORM | Entity Framework Core 9.0 |
| Database | PostgreSQL (Neon-backed via Replit) |
| CSS | Custom design system with tokens.css + Bootstrap 5 |
| Reports | ClosedXML (Excel), QuestPDF (PDF) |
| Barcodes | ZXing.Net.Bindings.SkiaSharp |
| AI | OpenAI integration (chat assistant) |
| Hosting | Replit (port 5000) |

**Project file:** `Abs.FixedAssets.csproj` targeting `net9.0`

---

## 2. DIRECTORY STRUCTURE

```
/
├── Abs.FixedAssets.csproj          # Project file
├── Program.cs                      # Startup, DI, middleware pipeline
├── replit.md                       # Project memory / architecture notes
├── HANDOFF_STATUS.md               # THIS FILE
│
├── Data/
│   ├── AppDbContext.cs             # EF Core DbContext (all DbSets)
│   ├── Seed.cs                    # Legacy seeder
│   └── CcaClassSeeder.cs         # Canadian CCA class seeder
│
├── Models/                        # 65 model files
│   ├── Enums.cs                   # Legacy enums (DepreciationMethod, AssetStatus, BookType, etc.)
│   ├── Asset.cs                   # Core asset entity
│   ├── Book.cs                    # Depreciation book
│   ├── Item.cs                    # Item master (also contains ItemRevision, ItemTransaction classes)
│   ├── LookupType.cs              # Reference data type definition
│   ├── LookupValue.cs             # Reference data value
│   ├── PurchaseOrder.cs           # PO entity
│   ├── PurchaseRequisition.cs     # Requisition entity
│   ├── WorkOrderOperation.cs      # WO operations
│   ├── GlAccount.cs               # GL accounts (also contains Location, Department, CostCenter classes)
│   ├── Site.cs                    # Site entity
│   ├── AssetMaintenance.cs        # MaintenanceEvent entity
│   ├── ConstructionInProgress.cs  # CIP project + CipCost entities
│   ├── Vendor.cs / VendorInvoice.cs / GoodsReceipt.cs
│   ├── AssetTransfer.cs / PartialDisposal.cs / Attachment.cs
│   ├── Revisions/                 # PMTemplateRevision, ItemRevisionEnhanced, RevisionStatus
│   └── ... (65 files total)
│
├── Pages/                         # 110+ Razor Pages
│   ├── Shared/                    # Layout, partials (_Layout, _ModernLayout, _ScreenHeader, etc.)
│   ├── Assets/                    # Asset CRUD (Asset, Index, Delete, Dispose, Transfer, Improve)
│   ├── Books/                     # Depreciation books (Create, Edit, Details, Delete, Index, GlAccounts)
│   ├── Admin/                     # Admin pages (50+ pages)
│   │   ├── Sites, Locations, Departments, CostCenters, GlAccounts
│   │   ├── WorkOrders, Vendors, Users, Companies
│   │   ├── Requisitions, PMSchedules, PMTemplates, Items
│   │   ├── Lookups/               # Lookup admin (Index, EditValues)
│   │   ├── Integrations/          # Webhook endpoints (Index, Inbound, Maps)
│   │   ├── Outbox/                # Outbox event viewer
│   │   └── Webhooks/              # Webhook subscriptions (Index, Deliveries)
│   ├── Purchasing/                # PO list + details
│   ├── AccountsPayable/           # Vendor invoice management
│   ├── Receiving/                 # Goods receipts
│   ├── Maintenance/               # Work orders, work requests, schedules
│   ├── Materials/                 # Item master edit (ItemEdit, Items list)
│   ├── CIP/                       # Capital improvement projects
│   ├── Inventory/                 # Inventory views
│   ├── Reports/                   # 10 report pages
│   ├── Journals/                  # Journal entries
│   ├── WorkOrders/                # Work order details
│   ├── BulkOperations/            # Bulk transfers, disposals
│   ├── AI/                        # AI assistant chat
│   ├── Help/                      # Help center
│   └── Account/, API/, CCA/, UsTax/
│
├── Services/                      # 60+ service files
│   ├── Lookups/
│   │   ├── ILookupService.cs      # Lookup service interface
│   │   ├── LookupService.cs       # Cached lookup service (10-min TTL)
│   │   └── LookupValueDto.cs      # DTO (Id, Code, Name, SortOrder, IsActive, Metadata)
│   ├── Seeding/                   # Versioned seed pipelines
│   │   ├── Pipelines/             # 8 pipelines (System, Org, Finance, Vendors, Parts, EAM, Demo, Lookup)
│   │   └── SeedPackExecutor.cs
│   ├── Maintenance/               # CloseoutService, PMSchedulerService, WorkRequestConversion
│   ├── Items/                     # BuyabilityScore, EffectiveProcurement, CrossReference, etc.
│   ├── Integrations/              # Inbound/outbound webhook services
│   ├── Testing/                   # SmokeTestRunner + background service
│   └── ... (DepreciationService, CipService, AuditService, etc.)
│
├── Migrations/                    # 30 EF Core migrations
│   └── AppDbContextModelSnapshot.cs
│
├── Middleware/
│   ├── OrgScopeMiddleware.cs      # Organization scope resolution
│   └── TenantContextMiddleware.cs # Tenant context injection
│
├── seed/
│   └── reference-data/            # 76 JSON lookup seed files
│       ├── AssetType.json, AssetStatus.json, AssetCondition.json, AssetPriority.json
│       ├── DepreciationMethod.json, DepreciationConvention.json, DepreciationFrequency.json
│       ├── BookType.json, TaxJurisdiction.json
│       ├── MaintenanceType.json, MaintenanceStatus.json, MaintenancePriority.json
│       ├── POStatus.json, PurchaseOrderType.json, RequisitionStatus.json
│       ├── ItemType.json, ItemStatus.json, CostMethod.json, TrackingType.json
│       ├── CipProjectStatus.json, CipCostType.json
│       ├── SiteType.json, SiteStatus.json, LocationType.json
│       ├── DepartmentType.json, CostCenterType.json, GlAccountType.json
│       ├── OperationType.json, OperationStatus.json
│       ├── TransferReason.json, DisposalReason.json
│       ├── AttachmentCategory.json, RevisionStatus.json
│       ├── WorkOrderType.json, WorkOrderStatus.json, WorkOrderPriority.json
│       ├── InvoiceStatus.json, ReceiptStatus.json
│       └── ... (76 total)
│
├── wwwroot/
│   ├── css/
│   │   ├── tokens.css             # Design token system (9+ families)
│   │   ├── modern.css             # Legacy variable aliases
│   │   ├── base.css, site.css     # Core styles
│   │   ├── premium-components.css # DataGrid, KPI cards
│   │   ├── sidebar-nav.css        # Navigation
│   │   └── modules/               # 20+ module CSS files
│   └── lib/bootstrap/
│
├── docs/                          # 70+ documentation files
│   ├── README.md                  # Documentation index
│   ├── Architecture.md, DeveloperGettingStarted.md
│   ├── reference-data-registry.md # Lookup types catalog
│   ├── adr/                       # 10 Architecture Decision Records
│   └── ...
│
└── tools/
    └── hardcoded-audit/           # Python audit scanner
```

---

## 3. LOOKUP TABLE INFRASTRUCTURE (Reference Data System)

### Schema
```sql
-- LookupTypes: defines categories of reference data
CREATE TABLE "LookupTypes" (
    "Id" SERIAL PRIMARY KEY,
    "TenantId" INTEGER,
    "CompanyId" INTEGER,
    "Key" TEXT NOT NULL,           -- e.g. "AssetType", "POStatus"
    "Name" TEXT NOT NULL,          -- e.g. "Asset Type", "PO Status"
    "Description" TEXT,
    "IsSystem" BOOLEAN DEFAULT TRUE,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMP,
    "UpdatedAt" TIMESTAMP
);

-- LookupValues: individual values within each type
CREATE TABLE "LookupValues" (
    "Id" SERIAL PRIMARY KEY,
    "LookupTypeId" INTEGER REFERENCES "LookupTypes"("Id"),
    "TenantId" INTEGER,
    "CompanyId" INTEGER,
    "Code" TEXT NOT NULL,          -- e.g. "PRODUCTION", "Draft"
    "Name" TEXT NOT NULL,          -- Display name
    "SortOrder" INTEGER DEFAULT 0,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "Metadata" JSONB,             -- Extra properties
    "CreatedAt" TIMESTAMP,
    "UpdatedAt" TIMESTAMP
);
```

### ILookupService Interface
```csharp
public interface ILookupService
{
    Task<List<LookupValueDto>> GetValuesAsync(int? tenantId, int? companyId, string lookupKey, bool includeInactive = false);
    Task<LookupValueDto?> GetValueByIdAsync(int? tenantId, int? companyId, int lookupValueId);
    Task<LookupValueDto?> GetValueByCodeAsync(int? tenantId, int? companyId, string lookupKey, string code);
    Task<List<SelectListItem>> GetSelectListAsync(int? tenantId, int? companyId, string lookupKey, string? selectedValue = null, string placeholder = "-- Select --");
    Task<List<SelectListItem>> GetSelectListByIdAsync(int? tenantId, int? companyId, string lookupKey, int? selectedId = null, string placeholder = "-- Select --");
    void InvalidateCache();
}
```

### LookupValueDto
```csharp
public class LookupValueDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public JsonDocument? Metadata { get; set; }

    public bool GetMetadataFlag(string key) { ... }
}
```

### Seed File Format (JSON in seed/reference-data/)
```json
{
  "lookupKey": "AssetType",
  "name": "Asset Type",
  "isSystem": true,
  "values": [
    { "code": "PRODUCTION", "name": "Production Equipment", "sortOrder": 1, "isActive": true },
    { "code": "EQUIPMENT", "name": "General Equipment", "sortOrder": 2, "isActive": true }
  ]
}
```
**76 lookup types** seeded from JSON, auto-discovered by `LookupSeedFileLoader.LoadAll()`.

---

## 4. FK-BOUND DROPDOWN PATTERN (The Migration We're Doing)

### What It Means
Every domain model has legacy **enum** fields (e.g., `AssetType Type`, `POStatus Status`). We're adding parallel **FK columns** (`int? AssetTypeLookupValueId`, `int? StatusLookupValueId`) that point to `LookupValues.Id`. Form dropdowns bind to the FK column, and on save we resolve the `Code` from the LookupValue to sync back to the enum field for backward compatibility.

### Model Pattern (Already Applied to All Models)
```csharp
// Legacy enum (kept for backward compatibility)
public AssetType Type { get; set; }

// NEW: FK to LookupValues table
public int? AssetTypeLookupValueId { get; set; }
```

### Page Model Pattern (The Standard)

**1. Dropdown population (OnGetAsync):**
```csharp
AssetTypeOptions = await _lookupService.GetSelectListByIdAsync(
    _tenantContext.TenantId, _tenantContext.CompanyId,
    "AssetType", asset?.AssetTypeLookupValueId, "");
```

**2. Form field naming (cshtml):**
```html
<select name="AssetTypeLookupValueId" class="form-select">
    @foreach (var opt in Model.AssetTypeOptions)
    {
        <option value="@opt.Value" selected="@opt.Selected">@opt.Text</option>
    }
</select>
```

**3. Handler receives LookupValueId and syncs enum (OnPostAsync):**
```csharp
public async Task<IActionResult> OnPostUpdateAsync(..., int assetTypeLookupValueId, ...)
{
    // Resolve LookupValue to get Code
    int? resolvedLvId = assetTypeLookupValueId > 0 ? assetTypeLookupValueId : (int?)null;
    var typeLv = await _lookupService.GetValueByIdAsync(null, null, assetTypeLookupValueId);

    // Sync enum from Code
    if (typeLv != null && Enum.TryParse<AssetType>(typeLv.Code, true, out var parsedType))
        asset.Type = parsedType;

    // Set FK
    asset.AssetTypeLookupValueId = resolvedLvId;
}
```

### Special Patterns

**Status workflow buttons (CIP, Maintenance, PO):**
When a button changes status (e.g., "Approve"), resolve the LookupValue by code at POST time:
```csharp
var approvedLv = await _lookupService.GetValueByCodeAsync(
    _tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", "Approved");
if (approvedLv != null)
{
    po.Status = POStatus.Approved;
    po.StatusLookupValueId = approvedLv.Id;
}
```

**GET filter parameters:**
GET-based filters (like GlAccounts type filter, asset list status filter) may continue using enum integer values for URL parameters - this is acceptable. Only POST form submissions need the FK pattern.

---

## 5. FK COLUMN STATUS IN DATABASE (All 39 Columns Present)

| Table | FK Column | Status |
|-------|----------|--------|
| Assets | AssetTypeLookupValueId | Present |
| Assets | AssetPriorityLookupValueId | Present |
| Assets | ConditionLookupValueId | Present |
| Assets | DepreciationMethodLookupValueId | Present |
| Assets | StatusLookupValueId | Present |
| AssetTransfers | ReasonLookupValueId | Present |
| Attachments | CategoryLookupValueId | Present |
| Books | BookTypeLookupValueId | Present |
| Books | ConventionLookupValueId | Present |
| Books | FrequencyLookupValueId | Present |
| Books | MethodLookupValueId | Present |
| Books | TaxJurisdictionLookupValueId | Present |
| CipCosts | CostTypeLookupValueId | Present |
| CipProjects | StatusLookupValueId | Present |
| CostCenters | TypeLookupValueId | Present |
| CustomerInvoices | StatusLookupValueId | Present |
| Departments | TypeLookupValueId | Present |
| GlAccounts | AccountTypeLookupValueId | Present |
| GoodsReceipts | StatusLookupValueId | Present |
| Items | TypeLookupValueId | Present |
| Items | StatusLookupValueId | Present |
| Items | CostMethodLookupValueId | Present |
| Items | TrackingTypeLookupValueId | Present |
| ItemRevisions | StatusLookupValueId | Present |
| ItemTransactions | TypeLookupValueId | Present |
| Locations | TypeLookupValueId | Present |
| MaintenanceEvents | TypeLookupValueId | Present |
| MaintenanceEvents | StatusLookupValueId | Present |
| MaintenanceEvents | PriorityLookupValueId | Present |
| PartialDisposals | ReasonLookupValueId | Present |
| PurchaseOrders | POTypeLookupValueId | Present |
| PurchaseOrders | StatusLookupValueId | Present |
| PurchaseRequisitions | StatusLookupValueId | Present |
| PurchaseRequisitions | PriorityLookupValueId | Present |
| Sites | TypeLookupValueId | Present |
| Sites | StatusLookupValueId | Present |
| VendorInvoices | StatusLookupValueId | Present |
| WorkOrderOperations | TypeLookupValueId | Present |
| WorkOrderOperations | StatusLookupValueId | Present |

---

## 6. PAGE-LEVEL FK-BOUND MIGRATION STATUS

### FULLY MIGRATED (FK-bound dropdowns + enum sync on save)

| Page | File | FK Fields Bound | Lookup Keys Used |
|------|------|----------------|-----------------|
| **Asset Edit/Create** | Pages/Assets/Asset.cshtml.cs | 5 | AssetType, AssetPriority, AssetCondition, DepreciationMethod, AssetStatus |
| **Asset Dispose** | Pages/Assets/Dispose.cshtml.cs | 1 | DisposalReason |
| **Asset Transfer** | Pages/Assets/Transfer.cshtml.cs | 1 | TransferReason |
| **Book Edit** | Pages/Books/Edit.cshtml.cs | 5 | DepreciationMethod, DepreciationConvention, TaxJurisdiction, DepreciationFrequency, BookType |
| **Site Admin** | Pages/Admin/Sites.cshtml.cs | 2 | SiteType, SiteStatus |
| **CIP Details** | Pages/CIP/Details.cshtml.cs | 2 | CipProjectStatus, CipCostType |
| **Maintenance Details** | Pages/Maintenance/Details.cshtml.cs | 3 | MaintenanceType, MaintenanceStatus, MaintenancePriority |
| **PO Index (Create)** | Pages/Purchasing/Index.cshtml.cs | 2 | PurchaseOrderType, POStatus |
| **Requisitions** | Pages/Admin/Requisitions.cshtml.cs | 2 | RequisitionStatus, RequisitionPriority |
| **AP Invoice Details** | Pages/AccountsPayable/Details.cshtml.cs | 1 | InvoiceStatus |
| **Item Edit** | Pages/Materials/ItemEdit.cshtml.cs | 4+1 | ItemType, ItemStatus, CostMethod, TrackingType (+RevisionStatus) |
| **Locations** | Pages/Admin/Locations.cshtml.cs | 1 | LocationType |
| **Departments** | Pages/Admin/Departments.cshtml.cs | 1 | DepartmentType |
| **Cost Centers** | Pages/Admin/CostCenters.cshtml.cs | 1 | CostCenterType |
| **GL Accounts** | Pages/Admin/GlAccounts.cshtml.cs | 1 | GlAccountType |
| **Work Order Details** | Pages/WorkOrders/Details.cshtml.cs | 2 | OperationType, OperationStatus |

### NOT YET MIGRATED (Still using enum-only)

| Page | File | Issue | FK Columns Available |
|------|------|-------|---------------------|
| **PO Details** | Pages/Purchasing/Details.cshtml.cs | `OnPostUpdateHeaderAsync` uses `(POType)poType` cast; status workflow buttons use `POStatus.Draft` etc. without syncing FK | POTypeLookupValueId, StatusLookupValueId |
| **Work Orders List** | Pages/Admin/WorkOrders.cshtml.cs | Read-only list page, uses enum comparisons for KPIs; no create/edit handlers | (N/A - display only) |
| **Receiving** | Pages/Receiving/Index.cshtml.cs | Read-only list page, filters by `POStatus` enum array | (N/A - display only) |

### NOTES ON "NOT YET MIGRATED" PAGES
- **Admin/WorkOrders.cshtml.cs** and **Receiving/Index.cshtml.cs** are **read-only list pages** with no create/update forms. They use enum comparisons for display filtering only. These are low priority - the FK migration pattern only matters for pages that **write** data.
- **Purchasing/Details.cshtml.cs** is the main gap - it has `OnPostUpdateHeaderAsync` (POType dropdown), status workflow buttons (Submit, Approve, etc.), and `OnPostDuplicatePOAsync` that all need FK sync.

---

## 7. ALL LEGACY ENUMS AND THEIR LOOKUP KEY MAPPINGS

| Enum (Models/Enums.cs or inline) | Lookup Key | Seed File |
|----------------------------------|-----------|-----------|
| DepreciationMethod (22 values) | DepreciationMethod | DepreciationMethod.json |
| DepreciationConvention (12 values) | DepreciationConvention | DepreciationConvention.json |
| TaxJurisdiction (3 values) | TaxJurisdiction | TaxJurisdiction.json |
| AssetStatus (7 values) | AssetStatus | AssetStatus.json |
| BookType (3 values) | BookType | BookType.json |
| PeriodStatus (3 values) | - | (no lookup yet) |
| DepreciationRunStatus (3 values) | - | (no lookup yet) |

### Enums Defined Inline in Model Files

| Enum | Defined In | Lookup Key | Seed File |
|------|-----------|-----------|-----------|
| AssetType | Asset.cs | AssetType | AssetType.json |
| AssetPriority | Asset.cs | AssetPriority | AssetPriority.json |
| AssetCondition | Asset.cs | AssetCondition | AssetCondition.json |
| MaintenanceType | AssetMaintenance.cs | MaintenanceType | MaintenanceType.json |
| MaintenanceStatus | AssetMaintenance.cs | MaintenanceStatus | MaintenanceStatus.json |
| MaintenancePriority | AssetMaintenance.cs | MaintenancePriority | MaintenancePriority.json |
| POType (PurchaseOrderType) | PurchaseOrder.cs | PurchaseOrderType | PurchaseOrderType.json |
| POStatus | PurchaseOrder.cs | POStatus | POStatus.json |
| RequisitionStatus | PurchaseRequisition.cs | RequisitionStatus | RequisitionStatus.json |
| RequisitionPriority | PurchaseRequisition.cs | RequisitionPriority | RequisitionPriority.json |
| InvoiceStatus | VendorInvoice.cs | InvoiceStatus | InvoiceStatus.json |
| ReceiptStatus | GoodsReceipt.cs | ReceiptStatus | ReceiptStatus.json |
| ItemType | Item.cs | ItemType | ItemType.json |
| ItemStatus | Item.cs | ItemStatus | ItemStatus.json |
| CostMethod | Item.cs | CostMethod | CostMethod.json |
| TrackingType | Item.cs | TrackingType | TrackingType.json |
| SiteType | Site.cs | SiteType | SiteType.json |
| SiteStatus | Site.cs | SiteStatus | SiteStatus.json |
| CipProjectStatus | ConstructionInProgress.cs | CipProjectStatus | CipProjectStatus.json |
| CipCostType | ConstructionInProgress.cs | CipCostType | CipCostType.json |
| TransferReason | AssetTransfer.cs | TransferReason | TransferReason.json |
| DisposalReason | PartialDisposal.cs | DisposalReason | DisposalReason.json |
| GlAccountType | GlAccount.cs | GlAccountType | GlAccountType.json |
| LocationType | GlAccount.cs (Location class) | LocationType | LocationType.json |
| DepartmentType | GlAccount.cs (Department class) | DepartmentType | DepartmentType.json |
| CostCenterType | GlAccount.cs (CostCenter class) | CostCenterType | CostCenterType.json |
| OperationType | WorkOrderOperation.cs | OperationType | OperationType.json |
| OperationStatus | WorkOrderOperation.cs | OperationStatus | OperationStatus.json |
| DepreciationFrequency | Book.cs | DepreciationFrequency | DepreciationFrequency.json |
| AttachmentCategory | Attachment.cs | AttachmentCategory | AttachmentCategory.json |
| RevisionStatus | Revisions/RevisionStatus.cs | RevisionStatus | RevisionStatus.json |

---

## 8. EF CORE MIGRATIONS (Chronological)

| Migration | Date | Purpose |
|-----------|------|---------|
| AddMultiLocationPurchasing | 2026-01-19 | PO releases, multi-location |
| AddPurchaseOrderReleases | 2026-01-19 | Release scheduling |
| AddFiscalCalendar | 2026-01-20 | Fiscal year/period tables |
| AddCIPToBookGlAccount | 2026-01-20 | CIP GL account mapping |
| AddMesIotOeeFields | 2026-01-20 | MES/IoT/OEE asset fields |
| AddAssetImageUrl | 2026-01-20 | Asset image URL |
| AddWorkRequests | 2026-01-21 | Work request system |
| Sprint3CloseoutIntelligence | 2026-01-21 | WO closeout heuristics |
| Sprint4WebhooksIntegrationHub | 2026-01-21 | Outbox webhook system |
| Sprint5WebhooksProductization | 2026-01-21 | HMAC signing, retry |
| Sprint6InboundWebhooks | 2026-01-21 | Inbound webhook receiver |
| Sprint6InboundWebhooksV2 | 2026-01-21 | Inbound v2 improvements |
| AddPMTemplateRevisions | 2026-01-22 | PM template versioning |
| Sprint11ItemCrossReference | 2026-01-22 | 3-way part number resolution |
| Sprint12ProcurementGradeParts | 2026-01-22 | AVL, alternates, supersession |
| AddImageUrlToVendorItemPart | 2026-01-22 | Vendor part images |
| Sprint13CatalogIntelligence | 2026-01-22 | Catalog URL enrichment |
| Sprint14ProcurementV2Lite | 2026-01-22 | Procurement cascade |
| RequireTenantIdOnTenantScopedEntities | 2026-01-22 | Tenant isolation |
| AddWorkOrderExecutionFields | 2026-01-23 | WO operation fields |
| **AddLookupTables** | 2026-02-23 | LookupType + LookupValue tables |
| **AddLookupValueForeignKeys** | 2026-02-24 | First batch of FK columns |
| **AddRemainingLookupValueForeignKeys** | 2026-02-24 | Remaining FK columns |
| **AddPhase4LookupValueForeignKeys** | 2026-02-25 | Phase 4 FK columns |
| FixAssetTypeAndPriorityBackfill | 2026-02-25 | Backfill AssetType FK values |
| FinalizeAssetTypeBackfill | 2026-02-25 | Finalize backfill |
| HardenAssetTypeUnspecifiedBackfill | 2026-02-25 | Unspecified fallback |
| HardenAssetTypeUnspecifiedBackfill_TenantScoped | 2026-02-25 | Tenant-scoped backfill |
| HardenAssetTypeUnspecifiedBackfill_CompanyPreferred | 2026-02-25 | Company-preferred backfill |
| AddOrgNodeAndCustomerTables | 2026-02-26 | OrgNode hierarchy + Customers |

**NOTE:** `AddRemainingLookupValueForeignKeys` was marked as applied in `__EFMigrationsHistory` but its columns were never actually created. We applied the missing columns (Locations, Departments, CostCenters, WorkOrderOperations, ItemTransactions, ItemRevisions, GlAccounts) via direct SQL ALTER TABLE on 2026-02-24.

---

## 9. KEY SERVICE DEPENDENCIES

### TenantContext
```csharp
public interface ITenantContext
{
    int TenantId { get; }
    int CompanyId { get; }
    int? SiteId { get; }
}
```
Resolved by `TenantContextMiddleware` from request headers/cookies.

### ModuleGuardService
```csharp
public interface IModuleGuardService
{
    Task<bool> IsModuleEnabledAsync(string moduleName);
}
```
Controls feature toggles (purchasing, maintenance, etc.).

### Page Model Constructor Pattern
```csharp
public AssetModel(
    AppDbContext context,
    IModuleGuardService moduleGuard,
    ITenantContext tenantContext,
    ILookupService lookupService)
```

---

## 10. BUILD STATUS
- **0 build errors** as of 2026-02-24
- Warnings are all nullable reference type warnings (CS8602, CS8619, CS8620) - cosmetic only
- Server runs on port 5000, PostgreSQL connected

---

## 11. WHAT'S NEXT (Remaining Work)

### Immediate: FK-bind Purchasing/Details.cshtml.cs
The PO detail page still uses raw enum casts and enum-only status transitions:
- `OnPostUpdateHeaderAsync`: `po.POType = (POType)poType` needs FK pattern
- Status buttons (Submit, Approve, Delete): need to resolve LookupValue by code and sync both enum + FK
- `OnPostDuplicatePOAsync`: needs to copy `POTypeLookupValueId` and set `StatusLookupValueId` for Draft

### Future: FK Value Backfill
Existing data rows have enum values but null FK columns. A backfill operation should:
1. For each table with a FK column, find rows where FK is null but enum has a value
2. Look up the corresponding LookupValue by matching enum name to Code
3. Set the FK column

### Future: Enum Removal
Once all code paths write to FK columns and backfill is complete, the legacy enum columns can be deprecated.

---

## 12. COMPLETE SEED FILE INVENTORY (76 Lookup Types)

AbcClass, ActiveInactive, AssetCondition, AssetPriority, AssetStatus, AssetType,
AttachmentCategory, AttachmentType, AuditAction, AuditEntityType,
BarcodeFormat, BarcodeSize, BarcodeType, BookType,
CalibrationFrequency, CipCostType, CipProjectStatus, CostCenterType, CostMethod,
Country, CraftType, Currency,
DepartmentType, DepreciationConvention, DepreciationFrequency, DepreciationMethod,
DisposalReason, DisposalType,
EnergyEfficiencyClass, EnvironmentalClass,
GlAccountType, GlNormalBalance,
IntegrationEntityType, InventoryCondition, InventoryDiscrepancy, InventoryTransactionType,
InvoiceStatus, IoTProtocol, ItemStatus, ItemType,
JournalStatus, Language, LocationType,
MaintenancePriority, MaintenanceStatus, MaintenanceType, MeterUOM,
OperationStatus, OperationType,
PaymentMethod, PaymentTerms, PMFrequency, POStatus, PressureUnit, PurchaseOrderType,
ReceiptStatus, RequisitionPriority, RequisitionStatus, RetentionPeriod, RevisionStatus,
SafetyClassification, SiteStatus, SiteType, StockingMethod,
TaxJurisdiction, Timezone, TrackingType, TransferReason,
UnitOfMeasure, UserRole,
VendorStatus, VendorType,
WorkOrderPriority, WorkOrderStatus, WorkOrderType, WorkRequestStatus

---

## 13. UI/DESIGN SYSTEM SUMMARY

- **Design Tokens:** `wwwroot/css/tokens.css` - 9+ token families (brand, surface, border, text, semantic, typography, spacing, radius, shadows)
- **Theme:** Light mode, blue primary, dark sidebar
- **Layout:** `_ModernLayout.cshtml` with `_ScreenHeader` partial (hero gradient header)
- **DataGrid:** Premium system with global search, multi-column sort, per-column filters, CSV/Excel export, column visibility
- **KPI Cards:** Frosted glass design in `_KpiStrip` partial
- **Tab Navigation:** `_TabNav` partial for consistent tabbed interfaces
- **Forms:** `_FormField` partial for standardized form fields

---

## 14. AUTHENTICATION & AUTHORIZATION

- Cookie-based auth via ASP.NET Core Identity
- 3 roles: Admin, Accountant, Viewer
- Multi-tenant isolation with tenant/company/site scoping
- `[Authorize(Roles = "Admin")]` on admin pages
- `[AllowAnonymous]` on public-facing pages (for development)

---

## 15. KEY CONFIGURATION (Program.cs)

- URL: `http://0.0.0.0:5000`
- DB: PostgreSQL via environment variables (PGHOST, PGPORT, etc.)
- Environment: Development (LAB profile)
- Demo data: Enabled
- Auto-migrate on startup: `db.Database.MigrateAsync()`
- Lookup seeding on startup: 76 types from JSON
- Background services: SmokeTestBackgroundService, WebhookDispatcherHostedService, InboundEventProcessorHostedService
