# CherryAI Enterprise Asset Management - Database Schema

## Overview
PostgreSQL database with **99 tables** and approximately **1,700+ columns** covering comprehensive Enterprise Asset Management functionality.

---

## Table Summary by Module

### Core Asset Management (7 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| Assets | 158 | Master asset register with MES/IoT/OEE/Safety/Calibration fields |
| AssetCategories | 16 | Asset classification with depreciation defaults |
| AssetBookSettings | 19 | Per-asset, per-book depreciation overrides |
| AssetTaxSettings | 11 | Canadian CCA tax settings per asset |
| AssetInventories | 13 | Barcode and physical inventory tracking |
| AssetTransfers | 13 | Asset location/department transfer history |
| CapitalImprovements | 12 | Asset improvements and capitalization |

### Depreciation & Books (10 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| Books | 28 | GAAP/Tax depreciation books |
| BookGlAccounts | 9 | GL account mapping per book |
| DepreciationPolicies | 43 | 52 pre-seeded policies (methods + conventions) |
| DepreciationRuns | 15 | Batch depreciation run headers |
| DepreciationRunDetails | 11 | Per-asset depreciation results |
| PolicyCategoryDefaults | 7 | Default policy by asset category |
| UsefulLifeTables | 7 | IRS Rev. Proc. 87-56 tables |
| UsefulLifeEntries | 16 | ADR life ranges and guideline lives |
| Section179Limits | 7 | Historical Section 179 limits by year |
| BonusDepreciationRates | 4 | Historical bonus depreciation rates |

### Canadian Tax (CCA) (3 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| CcaClasses | 9 | 25 pre-seeded CCA classes |
| CcaClassBalances | 17 | Year-over-year UCC balances |
| CcaTransactions | 17 | CCA transaction journal |

### Work Orders / Maintenance (12 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| MaintenanceEvents | 47 | Work order headers |
| WorkOrderOperations | 38 | Multi-operation WO support |
| WorkOrderOperationLabors | 13 | Labor time entries per operation |
| WorkOrderOperationParts | 15 | Parts per operation |
| WorkOrderOperationTools | 13 | Tools required per operation |
| WorkOrderParts | 17 | WO-level materials (planned/issue/return) |
| WorkOrderTypes | 10 | 12 pre-seeded WO types |
| MaintenanceSchedules | 15 | PM schedules linked to assets |
| MaintenanceTypeCodes | 8 | Corrective/Preventive/Predictive codes |
| Technicians | 12 | Maintenance technicians |
| Crafts | 10 | 15 craft types (Electrical, Mechanical, etc.) |
| LaborTypes | 9 | 10 labor types |

### PM Templates (3 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| PMTemplates | 37 | Preventive maintenance templates |
| PMTemplateAssets | 13 | Template-to-asset assignments |
| PMTemplateItems | 8 | Template BOM (parts list) |

### Failure Analysis (4 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| FailureCodes | 8 | 24 failure codes |
| CauseCodes | 8 | 17 cause codes |
| ActionCodes | 8 | 16 action codes |
| ProblemCodes | 8 | 14 problem codes |

### Inventory & Parts (11 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| Items | 87 | Item master with full part detail |
| ItemCategories | 9 | Part categories |
| ItemVendors | 28 | Vendor-item cross-reference |
| ItemInventories2 | 17 | Stocking locations and quantities |
| ItemTransactions | 21 | Inventory movement journal |
| ItemImages | 14 | Part images |
| ItemRevisions | 11 | Part revision control |
| ItemCompanyStockings | 25 | Multi-company stocking decisions |
| Kits | 10 | Kit/assembly headers |
| KitItems | 7 | Kit components |
| ReorderAlerts | 13 | Auto-generated reorder alerts |

### Purchasing (6 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| PurchaseOrders | 28 | PO headers |
| PurchaseOrderLines | 24 | PO line items |
| PurchaseOrderReleases | 10 | Blanket PO releases |
| PurchaseRequisitions | 37 | Requisition headers |
| PurchaseRequisitionLines | 23 | Requisition line items |
| GoodsReceipts | 14 | Receiving headers |
| GoodsReceiptLines | 14 | Receiving line items |

### Accounts Payable (3 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| VendorInvoices | 23 | Vendor invoice headers |
| VendorInvoiceLines | 12 | Invoice line items |
| InvoicePayments | 8 | Payment records |

### Vendors & Manufacturers (2 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| Vendors | 30 | Vendor master |
| Manufacturers | 11 | Manufacturer master |

### Capital Projects (CIP) (3 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| CipProjects | 24 | Capital improvement project headers |
| CipCosts | 14 | Project cost entries |
| ProjectManagers | 11 | Project manager assignments |

### Organizational Hierarchy (5 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| Companies | 46 | Multi-company support |
| Sites | 43 | Manufacturing sites |
| Locations | 36 | Locations within sites |
| Departments | 11 | Organizational departments |
| CostCenters | 15 | Cost center tracking |

### Financial (5 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| GlAccounts | 19 | 155 pre-seeded GL accounts |
| JournalEntries | 9 | GL journal headers |
| JournalLines | 7 | GL journal lines |
| FiscalYears | 14 | Fiscal year definitions |
| FiscalPeriods | 15 | Accounting periods |

### System Configuration (10 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| NumberingSequences | 17 | Auto-numbering (10 types) |
| PaymentTerms | 10 | 13 payment terms |
| UOMDefinitions | 10 | 26 units of measure |
| Currencies | 8 | 10 currencies |
| TaxCodes | 10 | 10 tax codes |
| ShippingMethods | 10 | 11 shipping methods |
| ApprovalWorkflows | 13 | 9 approval workflow types |
| ExchangeRates | 9 | Currency exchange rates |
| PriorityLevels | 11 | 6 priority levels |
| LaborRates | 13 | 15 labor rate definitions |

### Skills & Labor (2 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| Skills | 13 | 14 technician skills |
| OperationLabor | 13 | Labor tracking |
| OperationTools | 13 | Tool tracking |

### Security & Audit (4 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| Users | 14 | User accounts (Admin/Accountant/Viewer) |
| ApiKeys | 10 | API key management |
| AuditLogs | 10 | Change audit trail |
| PeriodLocks | 6 | Accounting period locks |

### Attachments & Documents (2 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| Attachments | 16 | Universal attachment system |
| MeterReadings | 14 | Asset meter readings |

### Physical Inventory (2 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| InventoryLists | 13 | Physical inventory count headers |
| InventoryScans | 11 | Individual scan records |

### Bulk Operations (2 tables)
| Table | Columns | Description |
|-------|---------|-------------|
| BulkOperations | 11 | Bulk operation headers |
| PartialDisposals | 16 | Partial asset disposals |

### US Tax (1 table)
| Table | Columns | Description |
|-------|---------|-------------|
| UsTaxSettings | 17 | US tax settings per asset |

---

## Key Relationships

```
Organization (implicit)
  └── Companies (multi-company)
        ├── Sites
        │     └── Locations
        │           └── Assets
        ├── Vendors
        ├── Items (Item Master)
        └── FiscalYears → FiscalPeriods

Assets
  ├── AssetBookSettings (per-book overrides)
  ├── AssetTaxSettings (CCA)
  ├── AssetInventories (barcode)
  ├── AssetTransfers (history)
  ├── CapitalImprovements
  ├── MaintenanceEvents (Work Orders)
  │     ├── WorkOrderOperations
  │     │     ├── WorkOrderOperationLabors
  │     │     ├── WorkOrderOperationParts
  │     │     └── WorkOrderOperationTools
  │     └── WorkOrderParts (WO-level materials)
  └── Attachments

PMTemplates
  ├── PMTemplateAssets (links to Assets)
  ├── PMTemplateItems (BOM)
  └── MaintenanceSchedules

Items
  ├── ItemVendors (cross-reference)
  ├── ItemInventories2 (stocking)
  ├── ItemTransactions
  ├── ItemImages
  ├── ItemRevisions
  └── ItemCompanyStockings

PurchaseOrders
  ├── PurchaseOrderLines → Items
  └── GoodsReceipts → GoodsReceiptLines

VendorInvoices
  ├── VendorInvoiceLines
  └── InvoicePayments

CipProjects
  └── CipCosts
```

---

## Column Counts by Table

| Table | Columns |
|-------|---------|
| ActionCodes | 8 |
| ApiKeys | 10 |
| ApprovalWorkflows | 13 |
| AssetBookSettings | 19 |
| AssetCategories | 16 |
| AssetInventories | 13 |
| AssetTaxSettings | 11 |
| AssetTransfers | 13 |
| **Assets** | **158** |
| Attachments | 16 |
| AuditLogs | 10 |
| BonusDepreciationRates | 4 |
| BookGlAccounts | 9 |
| Books | 28 |
| BulkOperations | 11 |
| CapitalImprovements | 12 |
| CauseCodes | 8 |
| CcaClassBalances | 17 |
| CcaClasses | 9 |
| CcaTransactions | 17 |
| CipCosts | 14 |
| CipProjects | 24 |
| Companies | 46 |
| CostCenters | 15 |
| Crafts | 10 |
| Currencies | 8 |
| Departments | 11 |
| DepreciationPolicies | 43 |
| DepreciationRunDetails | 11 |
| DepreciationRuns | 15 |
| ExchangeRates | 9 |
| FailureCodes | 8 |
| FiscalPeriods | 15 |
| FiscalYears | 14 |
| GlAccounts | 19 |
| GoodsReceiptLines | 14 |
| GoodsReceipts | 14 |
| InventoryLists | 13 |
| InventoryScans | 11 |
| InvoicePayments | 8 |
| ItemCategories | 9 |
| ItemCompanyStockings | 25 |
| ItemImages | 14 |
| ItemInventories2 | 17 |
| ItemRevisions | 11 |
| ItemTransactions | 21 |
| ItemVendors | 28 |
| **Items** | **87** |
| JournalEntries | 9 |
| JournalLines | 7 |
| KitItems | 7 |
| Kits | 10 |
| LaborRates | 13 |
| LaborTypes | 9 |
| Locations | 36 |
| MaintenanceEvents | 47 |
| MaintenanceSchedules | 15 |
| MaintenanceTypeCodes | 8 |
| Manufacturers | 11 |
| MeterReadings | 14 |
| NumberingSequences | 17 |
| OperationLabor | 13 |
| OperationTools | 13 |
| PMTemplateAssets | 13 |
| PMTemplateItems | 8 |
| PMTemplates | 37 |
| PartialDisposals | 16 |
| PaymentTerms | 10 |
| PeriodLocks | 6 |
| PolicyCategoryDefaults | 7 |
| PriorityLevels | 11 |
| ProblemCodes | 8 |
| ProjectManagers | 11 |
| PurchaseOrderLines | 24 |
| PurchaseOrderReleases | 10 |
| PurchaseOrders | 28 |
| PurchaseRequisitionLines | 23 |
| PurchaseRequisitions | 37 |
| ReorderAlerts | 13 |
| Section179Limits | 7 |
| ShippingMethods | 10 |
| Sites | 43 |
| Skills | 13 |
| TaxCodes | 11 |
| Technicians | 12 |
| UOMDefinitions | 10 |
| UsTaxSettings | 17 |
| UsefulLifeEntries | 16 |
| UsefulLifeTables | 7 |
| Users | 14 |
| VendorInvoiceLines | 12 |
| VendorInvoices | 23 |
| Vendors | 30 |
| WorkOrderOperationLabors | 13 |
| WorkOrderOperationParts | 15 |
| WorkOrderOperationTools | 13 |
| WorkOrderOperations | 38 |
| WorkOrderParts | 17 |
| WorkOrderTypes | 10 |

---

*Generated: January 21, 2026*
