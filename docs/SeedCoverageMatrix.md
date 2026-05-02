# Seed Coverage Matrix

Generated: 2026-01-21
Total EF Entities: 97 DbSets
Seeded Entities: 32 (33%)

---

## 1. Coverage Summary

| Category | Entities | Seeded | Coverage |
|----------|----------|--------|----------|
| System Reference | 19 | 19 | 100% |
| Org & Finance | 6 | 6 | 100% |
| Vendors & Parts | 4 | 4 | 100% |
| EAM Execution | 1 | 1 | 100% |
| Demo Data | 2 | 2 | Dev Only |
| Transactional | 65 | 0 | N/A |

---

## 2. Seed Coverage Matrix

### Pipeline 1: SystemReferenceSeed (v1.0.0)

| Entity | Step | Natural Key | Env | Status | Records |
|--------|------|-------------|-----|--------|---------|
| WorkOrderType | WorkOrderTypesSeedStep | Code | All | SEEDED | 12 |
| FailureCode | FailureCodesSeedStep | Code | All | SEEDED | 24 |
| CauseCode | CauseCodesSeedStep | Code | All | SEEDED | 17 |
| ActionCode | ActionCodesSeedStep | Code | All | SEEDED | 16 |
| ProblemCode | ProblemCodesSeedStep | Code | All | SEEDED | 14 |
| PriorityLevel | PriorityLevelsSeedStep | Code | All | SEEDED | 6 |
| Craft | CraftsSeedStep | Code | All | SEEDED | 15 |
| MaintenanceTypeCode | MaintenanceTypeCodesSeedStep | Code | All | SEEDED | 10 |
| LaborType | LaborTypesSeedStep | Code | All | SEEDED | 10 |
| Skill | SkillsSeedStep | Code | All | SEEDED | 14 |
| NumberingSequence | NumberingSequencesSeedStep | Code | All | SEEDED | 10 |
| PaymentTerm | PaymentTermsSeedStep | Code | All | SEEDED | 13 |
| Currency | CurrenciesSeedStep | Code | All | SEEDED | 10 |
| UOMDefinition | UOMDefinitionsSeedStep | Code | All | SEEDED | 26 |
| ShippingMethod | ShippingMethodsSeedStep | Code | All | SEEDED | 11 |
| TaxCode | TaxCodesSeedStep | Code | All | SEEDED | 10 |
| Section179Limits | Section179LimitsSeedStep | TaxYear | All | SEEDED | 6 |
| BonusDepreciationRates | BonusDepreciationRatesSeedStep | TaxYear | All | SEEDED | 10 |
| CcaClass | CcaClassesSeedStep | ClassNumber | All | SEEDED | 25 |

### Pipeline 2: OrgAndFinanceSeed (v1.0.0)

| Entity | Step | Natural Key | Env | Status | Records |
|--------|------|-------------|-----|--------|---------|
| GlAccount | GlAccountsSeedStep | AccountNumber | All | SEEDED | 30 |
| Site | SitesSeedStep | SiteCode | All | SEEDED | 5 |
| Department | DepartmentsSeedStep | Code | All | SEEDED | 8 |
| CostCenter | CostCentersSeedStep | Code | All | SEEDED | 7 |
| AssetCategory | AssetCategoriesSeedStep | Code | All | SEEDED | 8 |
| ApprovalWorkflow | ApprovalWorkflowsSeedStep | Code | All | SEEDED | 6 |

### Pipeline 3: VendorsAndPartsFoundationSeed (v1.0.0)

| Entity | Step | Natural Key | Env | Status | Records |
|--------|------|-------------|-----|--------|---------|
| ItemCategory | ItemCategoriesSeedStep | Code | All | SEEDED | 16 |
| Manufacturer | ManufacturersSeedStep | Name (case-insensitive) | All | SEEDED | 10 |
| Vendor | VendorsSeedStep | Code | All | SEEDED | 10 |
| LaborRate | LaborRatesSeedStep | Code | All | SEEDED | 10 |

### Pipeline 4: EamExecutionMastersSeed (v1.0.0)

| Entity | Step | Natural Key | Env | Status | Records |
|--------|------|-------------|-----|--------|---------|
| Technician | TechniciansSeedStep | Name (case-insensitive) | All | SEEDED | 10 |

### Pipeline 5: DemoScenarioSeed (v1.0.0)

| Entity | Step | Natural Key | Env | Status | Records |
|--------|------|-------------|-----|--------|---------|
| Asset | DemoAssetsSeedStep | AssetNumber | Dev Only | SEEDED | 10 |
| Item | DemoItemsSeedStep | PartNumber | Dev Only | SEEDED | 15 |

---

## 3. Unseeded Entities (Transactional/User-Created)

These entities are NOT seeded because they represent transactional data or user-created content:

### Core Asset Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| Asset | User-created (Demo only in dev) |
| Book | User-configured per deployment |
| BookGlAccount | User-configured |
| AssetBookSettings | Per-asset configuration |
| AssetTransfer | Transactional |
| CapitalImprovement | Transactional |
| AssetTaxSettings | Per-asset configuration |
| AssetInventory | Transactional |

### Financial/Journal Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| JournalEntry | Transactional |
| JournalLine | Transactional |
| DepreciationRun | Transactional |
| DepreciationRunDetail | Transactional |
| PartialDisposal | Transactional |

### CCA (Canadian Tax) Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| CcaClassBalance | Transactional |
| CcaTransaction | Transactional |
| UsTaxSettings | Per-asset configuration |

### Organization Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| Company | User-configured (1+ per deployment) |
| Location | User-created |
| User | User-created |

### Fiscal/Period Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| FiscalYear | User-configured |
| FiscalPeriod | Generated from FiscalYear |
| PeriodLock | User-configured |

### Depreciation Policy Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| DepreciationPolicy | Pre-seeded by MasterDataBootstrap |
| UsefulLifeTable | Pre-seeded by MasterDataBootstrap |
| UsefulLifeEntry | Pre-seeded by MasterDataBootstrap |
| PolicyCategoryDefault | User-configured |

### Purchasing/AP Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| PurchaseOrder | Transactional |
| PurchaseOrderLine | Transactional |
| PurchaseOrderRelease | Transactional |
| GoodsReceipt | Transactional |
| GoodsReceiptLine | Transactional |
| VendorInvoice | Transactional |
| VendorInvoiceLine | Transactional |
| InvoicePayment | Transactional |
| PurchaseRequisition | Transactional |
| PurchaseRequisitionLine | Transactional |
| ReorderAlert | System-generated |

### Inventory/Item Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| Item | User-created (Demo only in dev) |
| ItemVendor | User-created |
| ItemRevision | User-created |
| ItemInventory | Transactional |
| ItemTransaction | Transactional |
| ItemImage | User-uploaded |
| ItemCompanyStocking | User-configured |

### Maintenance Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| MaintenanceEvent | Transactional |
| MaintenanceSchedule | User-created |
| PMTemplate | User-created |
| PMTemplateItem | User-created |
| PMTemplateAsset | User-created |
| MeterReading | Transactional |
| WorkOrderPart | Transactional |

### CIP Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| CipProject | Transactional |
| CipCost | Transactional |

### Kit Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| Kit | User-created |
| KitItem | User-created |

### Inventory Management Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| InventoryList | User-created |
| InventoryScan | Transactional |

### System Entities
| Entity | Reason Not Seeded |
|--------|-------------------|
| AuditLog | System-generated |
| BulkOperation | System-generated |
| ApiKey | User-created |
| Attachment | User-uploaded |
| ExchangeRate | User-configured |
| ProjectManager | User-created |

---

## 4. Critical Masters for Operations

These entities are REQUIRED for basic operations and should be monitored:

| Entity | Min Required | Current Pipeline | Priority |
|--------|--------------|------------------|----------|
| Company | 1 | MasterDataBootstrap | CRITICAL |
| Site | 1 | OrgAndFinanceSeed | CRITICAL |
| Location | 1 | Not Seeded | HIGH |
| GlAccount | 10+ | OrgAndFinanceSeed | CRITICAL |
| Department | 1 | OrgAndFinanceSeed | HIGH |
| CostCenter | 1 | OrgAndFinanceSeed | HIGH |
| AssetCategory | 1 | OrgAndFinanceSeed | HIGH |
| Vendor | 1 | VendorsAndPartsFoundationSeed | MEDIUM |
| ItemCategory | 1 | VendorsAndPartsFoundationSeed | MEDIUM |
| Technician | 1 | EamExecutionMastersSeed | MEDIUM |
| PMTemplate | 0 | Not Seeded | LOW |

---

## 5. Environment Flags

| Pipeline | IsDevOnly | Production Safe |
|----------|-----------|-----------------|
| SystemReferenceSeed | false | YES |
| OrgAndFinanceSeed | false | YES |
| VendorsAndPartsFoundationSeed | false | YES |
| EamExecutionMastersSeed | false | YES |
| DemoScenarioSeed | true | NO |

---

## 6. Idempotency Verification

All pipelines verified for TRUE idempotency (Inserted=0, Updated=0 on rerun):

| Pipeline | Last Verified | Result |
|----------|---------------|--------|
| SystemReferenceSeed | 2026-01-21 | PASS (159 skipped) |
| OrgAndFinanceSeed | 2026-01-21 | PASS (64 skipped) |
| VendorsAndPartsFoundationSeed | 2026-01-21 | PASS (46 skipped) |
| EamExecutionMastersSeed | 2026-01-21 | PASS (10 skipped) |
| DemoScenarioSeed | 2026-01-21 | PASS (25 skipped) |
