# Reference Data Registry

This document catalogs all lookup types, their JSON seed files, consuming pages, FK linkage status, and usage context within the CherryAI EAM system.

## Summary

- **Total Lookup Types:** 64
- **Total Lookup Values:** 350+
- **Seed File Location:** `seed/reference-data/`
- **FK-Linked Models:** 5 (Phase 3)
- **Scanner Result:** HIGH=0, MEDIUM=0, LOW=2

## Lookup Types — Full Registry

| # | Lookup Key | Display Name | Values | Seed File | UI Pages | FK Column | System |
|---|---|---|---|---|---|---|---|
| 1 | AbcClass | ABC Classification | 4 | AbcClass.json | — | — | ✓ |
| 2 | ActiveInactive | Active / Inactive | 2 | ActiveInactive.json | Admin/AssetCategories, Admin/CostCenters, Admin/Departments, Admin/GlAccounts, Admin/ItemCategories, Admin/Manufacturers, Admin/Vendors, Materials/ItemEdit, Materials/Items | — | ✓ |
| 3 | AssetPriority | Asset Priority | 5 | AssetPriority.json | Assets/Asset | — | ✓ |
| 4 | AssetStatus | Asset Status | 4 | AssetStatus.json | BulkOperations/Index, Reports/Builder | — | ✓ |
| 5 | AssetType | Asset Type | 7 | AssetType.json | Assets/Asset | — | ✓ |
| 6 | AttachmentCategory | Attachment Category | 6 | AttachmentCategory.json | Maintenance/Details | — | ✓ |
| 7 | AttachmentType | Attachment Type | 5 | AttachmentType.json | Assets/Asset | — | ✓ |
| 8 | AuditAction | Audit Action | 9 | AuditAction.json | — | — | ✓ |
| 9 | AuditEntityType | Audit Entity Type | 14 | AuditEntityType.json | — | — | ✓ |
| 10 | BarcodeFormat | Barcode Format | 4 | BarcodeFormat.json | Admin/Barcodes | — | ✓ |
| 11 | BarcodeSize | Barcode Size | 3 | BarcodeSize.json | Admin/Barcodes | — | ✓ |
| 12 | BarcodeType | Barcode Type | 7 | BarcodeType.json | — | — | ✓ |
| 13 | BookType | Book Type | 3 | BookType.json | Books/Edit | `Book.BookTypeLookupValueId` | ✓ |
| 14 | CalibrationFrequency | Calibration Frequency | 4 | CalibrationFrequency.json | Assets/Asset | — | ✓ |
| 15 | CipCostType | CIP Cost Type | 12 | CipCostType.json | CIP/Details | `CipCost.CostTypeLookupValueId` | ✓ |
| 16 | CipProjectStatus | CIP Project Status | 5 | CipProjectStatus.json | — | — | ✓ |
| 17 | CostMethod | Cost Method | 5 | CostMethod.json | — | — | ✓ |
| 18 | Country | Country | 2 | Country.json | Admin/Company | — | ✓ |
| 19 | CraftType | Craft Type | 6 | CraftType.json | Maintenance/Details | — | ✓ |
| 20 | Currency | Currency | 4 | Currency.json | Admin/Company, Admin/ExchangeRates, Assets/Asset | — | ✓ |
| 21 | DepreciationConvention | Depreciation Convention | 12 | DepreciationConvention.json | Books/Edit | — | ✓ |
| 22 | DepreciationFrequency | Depreciation Frequency | 5 | DepreciationFrequency.json | Books/Edit | — | ✓ |
| 23 | DepreciationMethod | Depreciation Method | 22 | DepreciationMethod.json | Books/Edit | — | ✓ |
| 24 | DisposalReason | Disposal Reason | 5 | DisposalReason.json | Assets/Dispose | — | ✓ |
| 25 | DisposalType | Disposal Type | 5 | DisposalType.json | BulkOperations/Index | — | ✓ |
| 26 | EnergyEfficiencyClass | Energy Efficiency Class | 5 | EnergyEfficiencyClass.json | Assets/Asset | — | ✓ |
| 27 | EnvironmentalClass | Environmental Class | 3 | EnvironmentalClass.json | Assets/Asset | — | ✓ |
| 28 | GlNormalBalance | GL Normal Balance | 2 | GlNormalBalance.json | Admin/GlAccounts | — | ✓ |
| 29 | IntegrationEntityType | Integration Entity Type | 5 | IntegrationEntityType.json | Admin/Integrations/Maps | — | ✓ |
| 30 | InventoryCondition | Inventory Condition | 6 | InventoryCondition.json | Inventory/List | — | ✓ |
| 31 | InventoryDiscrepancy | Inventory Discrepancy | 3 | InventoryDiscrepancy.json | Inventory/List | — | ✓ |
| 32 | InventoryTransactionType | Inventory Transaction Type | 5 | InventoryTransactionType.json | Admin/Inventory | — | ✓ |
| 33 | IoTProtocol | IoT Protocol | 6 | IoTProtocol.json | Assets/Asset | — | ✓ |
| 34 | ItemStatus | Item Status | 4 | ItemStatus.json | — | — | ✓ |
| 35 | ItemType | Item Type | 6 | ItemType.json | Materials/Items | — | ✓ |
| 36 | JournalStatus | Journal Status | 3 | JournalStatus.json | — | — | ✓ |
| 37 | Language | Language | 3 | Language.json | Admin/Company | — | ✓ |
| 38 | MaintenancePriority | Maintenance Priority | 4 | MaintenancePriority.json | Maintenance/Details, Maintenance/Index | `MaintenanceEvent.PriorityLookupValueId` | ✓ |
| 39 | MaintenanceType | Maintenance Type | 8 | MaintenanceType.json | Maintenance/Details, Maintenance/Index | `MaintenanceEvent.TypeLookupValueId` | ✓ |
| 40 | MeterUOM | Meter Unit of Measure | 4 | MeterUOM.json | Assets/Asset | — | ✓ |
| 41 | OperationStatus | Operation Status | 5 | OperationStatus.json | WorkOrders/Details | — | ✓ |
| 42 | OperationType | Operation Type | 13 | OperationType.json | WorkOrders/Details | — | ✓ |
| 43 | PaymentMethod | Payment Method | 5 | PaymentMethod.json | AccountsPayable/Details | — | ✓ |
| 44 | PaymentTerms | Payment Terms | 7 | PaymentTerms.json | Admin/Vendors | — | ✓ |
| 45 | PMFrequency | PM Frequency | 4 | PMFrequency.json | Admin/PMSchedules | — | ✓ |
| 46 | PressureUnit | Pressure Unit | 3 | PressureUnit.json | Assets/Asset | — | ✓ |
| 47 | PurchaseOrderType | Purchase Order Type | 4 | PurchaseOrderType.json | Purchasing/Index | `PurchaseOrder.POTypeLookupValueId` | ✓ |
| 48 | RequisitionPriority | Requisition Priority | 4 | RequisitionPriority.json | Admin/Requisitions | — | ✓ |
| 49 | RequisitionStatus | Requisition Status | 5 | RequisitionStatus.json | Admin/Requisitions | — | ✓ |
| 50 | RetentionPeriod | Retention Period | 4 | RetentionPeriod.json | Admin/SystemSettings | — | ✓ |
| 51 | SafetyClassification | Safety Classification | 4 | SafetyClassification.json | Assets/Asset | — | ✓ |
| 52 | StockingMethod | Stocking Method | 5 | StockingMethod.json | — | — | ✓ |
| 53 | TaxJurisdiction | Tax Jurisdiction | 3 | TaxJurisdiction.json | Books/Edit | — | ✓ |
| 54 | Timezone | Timezone | 9 | Timezone.json | Admin/Company, Admin/Sites | — | ✓ |
| 55 | TrackingType | Tracking Type | 4 | TrackingType.json | — | — | ✓ |
| 56 | TransferReason | Transfer Reason | 5 | TransferReason.json | Assets/Transfer | — | ✓ |
| 57 | UnitOfMeasure | Unit of Measure | 12 | UnitOfMeasure.json | — | — | ✓ |
| 58 | UserRole | User Role | 3 | UserRole.json | Admin/Users | — | ✓ |
| 59 | VendorStatus | Vendor Status | 4 | VendorStatus.json | — | — | ✓ |
| 60 | VendorType | Vendor Type | 5 | VendorType.json | Admin/Vendors | — | ✓ |
| 61 | WorkOrderPriority | Work Order Priority | 5 | WorkOrderPriority.json | — | — | ✓ |
| 62 | WorkOrderStatus | Work Order Status | 5 | WorkOrderStatus.json | — | — | ✓ |
| 63 | WorkOrderType | Work Order Type | 6 | WorkOrderType.json | — | — | ✓ |
| 64 | WorkRequestStatus | Work Request Status | 5 | WorkRequestStatus.json | Maintenance/WorkRequests/Index | — | ✓ |

## FK Linkage Status (Phase 3)

Five representative domain models now carry nullable FK columns pointing to `LookupValues`, enabling relational integrity between business entities and reference data.

| Model | FK Column | Lookup Key | Old Enum Field | Migration |
|---|---|---|---|---|
| `Book` | `BookTypeLookupValueId` | BookType | `BookType` (enum) | `20260224000000_AddLookupValueForeignKeys` |
| `PurchaseOrder` | `POTypeLookupValueId` | PurchaseOrderType | `POType` (enum) | Same |
| `CipCost` | `CostTypeLookupValueId` | CipCostType | `CostType` (enum) | Same |
| `MaintenanceEvent` | `TypeLookupValueId` | MaintenanceType | `Type` (enum) | Same |
| `MaintenanceEvent` | `PriorityLookupValueId` | MaintenancePriority | `Priority` (enum) | Same |

### FK Transition Pattern

During the transition period, both the old enum field and the new FK column are maintained:

1. **On Read:** `GetSelectListByIdAsync` loads dropdown options keyed by `LookupValue.Id`
2. **On Save:** The FK column is set from the form value; the old enum field is synced from the LookupValue's `Code` for backward compatibility
3. **Migration Backfill:** SQL in the migration populates FK values for existing rows by matching enum integer codes to `LookupValue.Code`

### Extending FK Linkage

To add FK linkage to additional models:

1. Add `public int? <Field>LookupValueId { get; set; }` and `public LookupValue? <Field>LookupValue { get; set; }` to the model
2. Register the FK in `AppDbContext.OnModelCreating` with `.HasOne<LookupValue>().WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.SetNull)`
3. Create a migration with SQL backfill: `UPDATE <table> SET <FK> = (SELECT lv."Id" FROM "LookupValues" lv JOIN "LookupTypes" lt ON lt."Id" = lv."LookupTypeId" WHERE lt."Key" = '<LookupKey>' AND lv."Code" = CAST(<OldField> AS TEXT))`
4. Update the PageModel to use `GetSelectListByIdAsync` and sync enum on save

## Architecture

### Data Flow

1. JSON files in `seed/reference-data/` define all reference data
2. `LookupSeedFileLoader` reads JSON at startup
3. `LookupDirectSeeder` upserts into `lookup_types` and `lookup_values` tables
4. `ILookupService` provides cached access (10-min TTL) with tenant/company scoping
5. Razor Pages consume via `GetSelectListAsync()` (code-keyed) or `GetSelectListByIdAsync()` (id-keyed) for dropdowns
6. FK columns on domain models reference `LookupValues.Id` for relational integrity

### Adding New Lookup Types

1. Create a new JSON file in `seed/reference-data/` following the schema below
2. The seeder automatically picks it up on next startup
3. Wire into page models via `ILookupService.GetSelectListAsync()` or `GetSelectListByIdAsync()`

### JSON Seed Schema

```json
{
  "key": "LookupKey",
  "displayName": "Human-Readable Name",
  "isSystem": true,
  "values": [
    { "code": "0", "name": "Value Name", "sortOrder": 1 }
  ]
}
```

### Scanner

Run the hardcoded data scanner to verify no regressions:

```bash
python3 tools/hardcoded-audit/audit_hardcoded_data.py --out proof-bundle-3
```

Expected output:
```
Scan complete: 2 findings (HIGH=0, MEDIUM=0, LOW=2)
PASS: No findings at or above HIGH severity
```

### Consuming Pages (28+)

The following pages load reference data from `ILookupService`:

- `Pages/AccountsPayable/Details` — PaymentMethod
- `Pages/Admin/AssetCategories` — ActiveInactive
- `Pages/Admin/Barcodes` — BarcodeFormat, BarcodeSize
- `Pages/Admin/Company` — Country, Currency, Language, Timezone
- `Pages/Admin/CostCenters` — ActiveInactive
- `Pages/Admin/Departments` — ActiveInactive
- `Pages/Admin/ExchangeRates` — Currency
- `Pages/Admin/GlAccounts` — ActiveInactive, GlNormalBalance
- `Pages/Admin/Integrations/Maps` — IntegrationEntityType
- `Pages/Admin/Inventory` — InventoryTransactionType
- `Pages/Admin/ItemCategories` — ActiveInactive
- `Pages/Admin/Manufacturers` — ActiveInactive
- `Pages/Admin/PMSchedules` — PMFrequency
- `Pages/Admin/Requisitions` — RequisitionPriority, RequisitionStatus
- `Pages/Admin/Sites` — Timezone
- `Pages/Admin/SystemSettings` — RetentionPeriod
- `Pages/Admin/Users` — UserRole
- `Pages/Admin/Vendors` — ActiveInactive, PaymentTerms, VendorType
- `Pages/Assets/Asset` — AssetPriority, AssetType, AttachmentType, CalibrationFrequency, Currency, EnergyEfficiencyClass, EnvironmentalClass, IoTProtocol, MeterUOM, PressureUnit, SafetyClassification
- `Pages/Assets/Dispose` — DisposalReason
- `Pages/Assets/Transfer` — TransferReason
- `Pages/Books/Edit` — BookType, DepreciationConvention, DepreciationFrequency, DepreciationMethod, TaxJurisdiction
- `Pages/BulkOperations/Index` — AssetStatus, DisposalType
- `Pages/CIP/Details` — CipCostType
- `Pages/Inventory/List` — InventoryCondition, InventoryDiscrepancy
- `Pages/Maintenance/Details` — AttachmentCategory, CraftType, MaintenancePriority, MaintenanceType
- `Pages/Maintenance/Index` — MaintenancePriority, MaintenanceType
- `Pages/Maintenance/WorkRequests/Index` — WorkRequestStatus
- `Pages/Materials/ItemEdit` — ActiveInactive
- `Pages/Materials/Items` — ActiveInactive, ItemType
- `Pages/Purchasing/Index` — PurchaseOrderType
- `Pages/Reports/Builder` — AssetStatus
- `Pages/WorkOrders/Details` — OperationStatus, OperationType
