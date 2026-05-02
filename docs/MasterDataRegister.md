# CherryAI EAM - Master Data Register (MDR)

**Generated:** 2026-01-21  
**Commit:** `c8143e697ceddc65e07cf333aa9fb79be1c4ddcd`

This document catalogs all master data domains in the CherryAI EAM system, their EF entities, natural keys, required fields, owning screens, and seed approach.

---

## LEGEND

| Seed Approach | Description |
|---------------|-------------|
| **System** | Safe reference defaults (statuses, types) - auto-seed always |
| **EAM Core Masters** | Business-specific masters (COA, locations, vendors) - seed on demand |
| **Demo** | Sample data for demo/testing only - dev environment only |

---

## FINANCE DOMAIN

### Chart of Accounts (GL Accounts)

| Property | Value |
|----------|-------|
| **EF Entity** | `GlAccount` |
| **DbSet** | `GlAccounts` |
| **Table** | `GlAccounts` |
| **Current Count** | 15 |
| **Natural Key** | `AccountNumber` (unique per company) |
| **Required Fields** | `AccountNumber`, `Name`, `AccountType`, `Category`, `NormalBalance` |
| **Owning Screen(s)** | `/Admin/GlAccounts` |
| **Import/Seed** | EAM Core Masters |

**Key Properties:**
```csharp
public string AccountNumber { get; set; }  // Required, 20 chars
public string Name { get; set; }            // Required, 100 chars
public GlAccountType AccountType { get; set; }
public GlAccountCategory Category { get; set; }
public NormalBalance NormalBalance { get; set; }
public bool IsActive { get; set; }
public int? CompanyId { get; set; }
```

---

### Fiscal Years

| Property | Value |
|----------|-------|
| **EF Entity** | `FiscalYear` |
| **DbSet** | `FiscalYears` |
| **Table** | `FiscalYears` |
| **Current Count** | 0 |
| **Natural Key** | `Year` + `CompanyId` |
| **Required Fields** | `Year`, `StartDate`, `EndDate`, `CompanyId` |
| **Owning Screen(s)** | `/Admin/SystemSettings` |
| **Import/Seed** | EAM Core Masters |

---

### Fiscal Periods

| Property | Value |
|----------|-------|
| **EF Entity** | `FiscalPeriod` |
| **DbSet** | `FiscalPeriods` |
| **Table** | `FiscalPeriods` |
| **Current Count** | 0 |
| **Natural Key** | `FiscalYearId` + `PeriodNumber` |
| **Required Fields** | `FiscalYearId`, `PeriodNumber`, `StartDate`, `EndDate` |
| **Owning Screen(s)** | `/Admin/SystemSettings` |
| **Import/Seed** | EAM Core Masters |

---

### Depreciation Policies

| Property | Value |
|----------|-------|
| **EF Entity** | `DepreciationPolicy` |
| **DbSet** | `DepreciationPolicies` |
| **Table** | `DepreciationPolicies` |
| **Current Count** | 10 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name`, `Method`, `Convention` |
| **Owning Screen(s)** | `/Admin/SystemSettings` |
| **Import/Seed** | System Reference |

---

## EAM ORGANIZATION DOMAIN

### Companies

| Property | Value |
|----------|-------|
| **EF Entity** | `Company` |
| **DbSet** | `Companies` |
| **Table** | `Companies` |
| **Current Count** | 3 |
| **Natural Key** | `CompanyCode` |
| **Required Fields** | `CompanyCode`, `Name` |
| **Owning Screen(s)** | `/Admin/Company` |
| **Import/Seed** | EAM Core Masters |

---

### Sites

| Property | Value |
|----------|-------|
| **EF Entity** | `Site` |
| **DbSet** | `Sites` |
| **Table** | `Sites` |
| **Current Count** | 0 |
| **Natural Key** | `SiteCode` |
| **Required Fields** | `SiteCode`, `Name`, `CompanyId` |
| **Owning Screen(s)** | `/Admin/Sites` |
| **Import/Seed** | EAM Core Masters |

**Key Properties:**
```csharp
public string SiteCode { get; set; }  // Required, 20 chars
public string Name { get; set; }       // Required, 100 chars
public SiteType Type { get; set; }
public SiteStatus Status { get; set; }
public int CompanyId { get; set; }
```

---

### Locations

| Property | Value |
|----------|-------|
| **EF Entity** | `Location` |
| **DbSet** | `Locations` |
| **Table** | `Locations` |
| **Current Count** | 21 |
| **Natural Key** | `Code` or `Name` (context-dependent) |
| **Required Fields** | `Name` |
| **Owning Screen(s)** | `/Admin/Locations` |
| **Import/Seed** | EAM Core Masters |

---

### Departments

| Property | Value |
|----------|-------|
| **EF Entity** | `Department` |
| **DbSet** | `Departments` |
| **Table** | `Departments` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/Departments` |
| **Import/Seed** | EAM Core Masters |

---

### Cost Centers

| Property | Value |
|----------|-------|
| **EF Entity** | `CostCenter` |
| **DbSet** | `CostCenters` |
| **Table** | `CostCenters` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/CostCenters` |
| **Import/Seed** | EAM Core Masters |

---

### Asset Categories

| Property | Value |
|----------|-------|
| **EF Entity** | `AssetCategory` |
| **DbSet** | `AssetCategories` |
| **Table** | `AssetCategories` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/AssetCategories` |
| **Import/Seed** | EAM Core Masters |

---

## EAM VENDOR & PEOPLE DOMAIN

### Vendors

| Property | Value |
|----------|-------|
| **EF Entity** | `Vendor` |
| **DbSet** | `Vendors` |
| **Table** | `Vendors` |
| **Current Count** | 10 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/Vendors` |
| **Import/Seed** | EAM Core Masters |

**Key Properties:**
```csharp
public string Code { get; set; }      // Required, 20 chars
public string Name { get; set; }       // Required, 100 chars
public VendorType Type { get; set; }
public VendorStatus Status { get; set; }
```

---

### Technicians

| Property | Value |
|----------|-------|
| **EF Entity** | `Technician` |
| **DbSet** | `Technicians` |
| **Table** | `Technicians` |
| **Current Count** | 5 |
| **Natural Key** | `EmployeeId` or `Email` |
| **Required Fields** | `Name` |
| **Owning Screen(s)** | `/Admin/Technicians` |
| **Import/Seed** | EAM Core Masters |

---

### Project Managers

| Property | Value |
|----------|-------|
| **EF Entity** | `ProjectManager` |
| **DbSet** | `ProjectManagers` |
| **Table** | `ProjectManagers` |
| **Current Count** | 0 |
| **Natural Key** | `Name` or `Email` |
| **Required Fields** | `Name` |
| **Owning Screen(s)** | `/Admin/ProjectManagers` |
| **Import/Seed** | EAM Core Masters |

---

### Manufacturers

| Property | Value |
|----------|-------|
| **EF Entity** | `Manufacturer` |
| **DbSet** | `Manufacturers` |
| **Table** | `Manufacturers` |
| **Current Count** | 0 |
| **Natural Key** | `Name` |
| **Required Fields** | `Name` |
| **Owning Screen(s)** | `/Admin/Manufacturers` |
| **Import/Seed** | EAM Core Masters |

---

## WORK ORDER DOMAIN

### PM Templates

| Property | Value |
|----------|-------|
| **EF Entity** | `PMTemplate` |
| **DbSet** | `PMTemplates` |
| **Table** | `PMTemplates` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/PMTemplates` |
| **Import/Seed** | EAM Core Masters or Demo |

**Key Properties:**
```csharp
public string Code { get; set; }       // Required, 50 chars
public string Name { get; set; }        // Required, 200 chars
public MaintenanceType Type { get; set; }
public PMTriggerType TriggerType { get; set; }
public RecurrenceType CalendarInterval { get; set; }
```

---

### PM Template Assets

| Property | Value |
|----------|-------|
| **EF Entity** | `PMTemplateAsset` |
| **DbSet** | `PMTemplateAssets` |
| **Table** | `PMTemplateAssets` |
| **Current Count** | 0 |
| **Natural Key** | `PMTemplateId` + `AssetId` |
| **Required Fields** | `PMTemplateId`, `AssetId` |
| **Owning Screen(s)** | `/Admin/PMTemplates`, `/Maintenance/Schedules` |
| **Import/Seed** | Demo |

---

## SYSTEM CONFIGURATION DOMAIN

### Work Order Types

| Property | Value |
|----------|-------|
| **EF Entity** | `WorkOrderType` |
| **DbSet** | `WorkOrderTypes` |
| **Table** | `WorkOrderTypes` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/WorkOrders` |
| **Import/Seed** | System Reference |

---

### Failure Codes

| Property | Value |
|----------|-------|
| **EF Entity** | `FailureCode` |
| **DbSet** | `FailureCodes` |
| **Table** | `FailureCodes` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/WorkOrders` |
| **Import/Seed** | System Reference |

---

### Cause Codes

| Property | Value |
|----------|-------|
| **EF Entity** | `CauseCode` |
| **DbSet** | `CauseCodes` |
| **Table** | `CauseCodes` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/WorkOrders` |
| **Import/Seed** | System Reference |

---

### Priority Levels

| Property | Value |
|----------|-------|
| **EF Entity** | `PriorityLevel` |
| **DbSet** | `PriorityLevels` |
| **Table** | `PriorityLevels` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/WorkOrders` |
| **Import/Seed** | System Reference |

---

### Crafts

| Property | Value |
|----------|-------|
| **EF Entity** | `Craft` |
| **DbSet** | `Crafts` |
| **Table** | `Crafts` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/WorkOrders` |
| **Import/Seed** | System Reference |

---

### Numbering Sequences

| Property | Value |
|----------|-------|
| **EF Entity** | `NumberingSequence` |
| **DbSet** | `NumberingSequences` |
| **Table** | `NumberingSequences` |
| **Current Count** | 0 |
| **Natural Key** | `SequenceType` |
| **Required Fields** | `SequenceType`, `Prefix`, `NextNumber` |
| **Owning Screen(s)** | `/Admin/SystemSettings` |
| **Import/Seed** | System Reference |

---

### Payment Terms

| Property | Value |
|----------|-------|
| **EF Entity** | `PaymentTerm` |
| **DbSet** | `PaymentTerms` |
| **Table** | `PaymentTerms` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/SystemSettings` |
| **Import/Seed** | System Reference |

---

### Currencies

| Property | Value |
|----------|-------|
| **EF Entity** | `Currency` |
| **DbSet** | `Currencies` |
| **Table** | `Currencies` |
| **Current Count** | 0 |
| **Natural Key** | `Code` (ISO 4217) |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/SystemSettings` |
| **Import/Seed** | System Reference |

---

### Tax Codes

| Property | Value |
|----------|-------|
| **EF Entity** | `TaxCode` |
| **DbSet** | `TaxCodes` |
| **Table** | `TaxCodes` |
| **Current Count** | 0 |
| **Natural Key** | `Code` |
| **Required Fields** | `Code`, `Name` |
| **Owning Screen(s)** | `/Admin/SystemSettings` |
| **Import/Seed** | System Reference |

---

## TAX REFERENCE DATA

### CCA Classes (Canada)

| Property | Value |
|----------|-------|
| **EF Entity** | `CcaClass` |
| **DbSet** | `CcaClasses` |
| **Table** | `CcaClasses` |
| **Current Count** | 25 |
| **Natural Key** | `ClassNumber` |
| **Required Fields** | `ClassNumber`, `Name`, `Rate` |
| **Owning Screen(s)** | `/CCA` |
| **Import/Seed** | System Reference |

---

### Section 179 Limits (US)

| Property | Value |
|----------|-------|
| **EF Entity** | `Section179Limits` |
| **DbSet** | `Section179Limits` |
| **Table** | `Section179Limits` |
| **Current Count** | 0 |
| **Natural Key** | `TaxYear` |
| **Required Fields** | `TaxYear`, `MaxDeduction`, `PhaseoutThreshold` |
| **Owning Screen(s)** | `/UsTax` |
| **Import/Seed** | System Reference |

---

### Bonus Depreciation Rates (US)

| Property | Value |
|----------|-------|
| **EF Entity** | `BonusDepreciationRates` |
| **DbSet** | `BonusDepreciationRates` |
| **Table** | `BonusDepreciationRates` |
| **Current Count** | 0 |
| **Natural Key** | `TaxYear` |
| **Required Fields** | `TaxYear`, `Rate` |
| **Owning Screen(s)** | `/UsTax` |
| **Import/Seed** | System Reference |

---

## SEED CLASSIFICATION SUMMARY

### System Reference Data (Always Safe to Seed)
- DepreciationPolicies
- CcaClasses
- Section179Limits
- BonusDepreciationRates
- WorkOrderTypes
- FailureCodes
- CauseCodes
- ActionCodes
- ProblemCodes
- PriorityLevels
- Crafts
- Skills
- LaborTypes
- NumberingSequences
- PaymentTerms
- Currencies
- TaxCodes
- ShippingMethods

### EAM Core Masters Data (Seed on Demand)
- GlAccounts (Chart of Accounts)
- Companies
- Sites
- Locations
- Departments
- CostCenters
- AssetCategories
- Vendors
- Technicians
- ProjectManagers
- Manufacturers
- FiscalYears
- FiscalPeriods
- PMTemplates

### Demo Data (Dev Only)
- Assets
- MaintenanceEvents (Work Orders)
- PMTemplateAssets
- CipProjects
- PurchaseOrders
- Items

---

## DbSet Declarations Reference

From `Data/AppDbContext.cs`:

```csharp
// Enterprise Master Files
public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
public DbSet<CostCenter> CostCenters => Set<CostCenter>();
public DbSet<Department> Departments => Set<Department>();
public DbSet<Location> Locations => Set<Location>();
public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
public DbSet<Vendor> Vendors => Set<Vendor>();

// Sites & Company
public DbSet<Company> Companies => Set<Company>();
public DbSet<Site> Sites => Set<Site>();

// PM Templates & Maintenance
public DbSet<PMTemplate> PMTemplates => Set<PMTemplate>();
public DbSet<PMTemplateAsset> PMTemplateAssets => Set<PMTemplateAsset>();
public DbSet<MaintenanceEvent> MaintenanceEvents => Set<MaintenanceEvent>();
public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();

// System Configuration Tables
public DbSet<NumberingSequence> NumberingSequences => Set<NumberingSequence>();
public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();
public DbSet<Currency> Currencies => Set<Currency>();
public DbSet<TaxCode> TaxCodes => Set<TaxCode>();

// Work Order Code Tables
public DbSet<WorkOrderType> WorkOrderTypes => Set<WorkOrderType>();
public DbSet<FailureCode> FailureCodes => Set<FailureCode>();
public DbSet<CauseCode> CauseCodes => Set<CauseCode>();
public DbSet<PriorityLevel> PriorityLevels => Set<PriorityLevel>();
public DbSet<Craft> Crafts => Set<Craft>();
```
