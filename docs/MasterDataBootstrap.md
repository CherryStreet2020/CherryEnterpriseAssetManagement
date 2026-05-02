# CherryAI EAM - Master Data Bootstrap Guide

**Generated:** 2026-01-21  
**Purpose:** Idempotent master data seeding to prevent "revert chaos"

## Overview

The Master Data Bootstrap system provides three separate pipelines for loading data:

1. **System Reference Seed** - Safe defaults (always OK to run)
2. **EAM Core Masters Load** - Business-specific data (on-demand)
3. **Demo Seed** - Sample data for testing (dev-only)

All pipelines are **idempotent** - they use natural keys to detect existing records and skip duplicates.

## How to Run

### Option 1: Admin UI (Recommended)

1. Navigate to `/Admin/DataImport`
2. Click the appropriate button for each pipeline
3. Review the import report for results

### Option 2: Programmatic (Service Injection)

```csharp
// Inject the service
private readonly IMasterDataBootstrapService _bootstrapService;

// Run a specific pipeline
var report = await _bootstrapService.RunSystemReferenceSeedAsync();
var report = await _bootstrapService.RunCustomerMasterLoadAsync();  // EAM Core Masters Load
var report = await _bootstrapService.RunDemoSeedAsync();

// Check results
if (report.Success)
{
    Console.WriteLine($"Inserted: {report.TotalInserted}, Updated: {report.TotalUpdated}");
}
```

## Pipeline Details

### System Reference Seed (Safe Always)

Seeds standard reference data that is common across all implementations:

| Domain | Records | Natural Key |
|--------|---------|-------------|
| WorkOrderTypes | 12 | Code |
| FailureCodes | 24 | Code |
| CauseCodes | 17 | Code |
| PriorityLevels | 6 | Code |
| Crafts | 15 | Code |
| NumberingSequences | 10 | SequenceType |
| PaymentTerms | 13 | Code |
| Currencies | 10 | Code (ISO) |
| Section179Limits | 7 | TaxYear |
| BonusDepreciationRates | 8 | TaxYear |

**When to Run:** After initial deployment, after major version upgrade, or after database restore.

### EAM Core Masters Load (On Demand)

Seeds customer-specific business data:

| Domain | Records | Natural Key |
|--------|---------|-------------|
| GlAccounts | 30 | AccountNumber + CompanyId |
| Sites | 5 | SiteCode |
| Departments | 10 | Code |
| CostCenters | 9 | Code |
| AssetCategories | 12 | Code |

**When to Run:** Only when explicitly needed. Do NOT run automatically in production.

### Demo Seed (Dev Only)

Seeds sample data for demonstration:

| Domain | Records | Natural Key |
|--------|---------|-------------|
| PMTemplates | 8 | Code |

**When to Run:** Development/testing environments only. Never in production.

## CSV Templates

Templates are located in `/data/templates/`:

- `ChartOfAccounts.csv` - GL account master
- `Sites.csv` - Facility sites
- `Locations.csv` - Locations within sites
- `Vendors.csv` - Vendor master
- `AssetCategories.csv` - Asset category definitions

## Idempotency Explained

The system uses **natural keys** (not auto-generated IDs) to determine if a record exists:

```csharp
// Example: Check by natural key, not ID
var existing = await _context.WorkOrderTypes
    .FirstOrDefaultAsync(x => x.Code == code);

if (existing == null)
{
    // Insert new record
    _context.WorkOrderTypes.Add(new WorkOrderType { ... });
}
else
{
    // Skip (or update if needed)
}
```

This ensures:
- Running the seed 100 times produces the same result as running it once
- No duplicate records are created
- Safe to run after reverts or database restores

## Guardrails

| Pipeline | Auto-Run | Production Safe | Recommended |
|----------|----------|-----------------|-------------|
| SystemReferenceSeed | OK | Yes | Always run after deploy |
| CustomerMasterLoad | NO | With caution | Only on initial setup |
| DemoSeed | NO | NO | Dev/test only |

## Import Report Format

Each pipeline returns a `BootstrapReport` with detailed results:

```json
{
  "Results": [
    {
      "Domain": "WorkOrderTypes",
      "TotalRecords": 12,
      "Inserted": 12,
      "Updated": 0,
      "Skipped": 0,
      "Failed": 0,
      "Success": true
    }
  ],
  "StartTime": "2026-01-21T10:00:00Z",
  "EndTime": "2026-01-21T10:00:05Z",
  "Success": true,
  "TotalInserted": 122,
  "TotalUpdated": 0,
  "TotalFailed": 0
}
```

## Troubleshooting

### "No company exists" Error
- The EAM Core Masters Load requires at least one company to exist
- Create a company first via `/Admin/Company`

### Records Not Appearing
- Check the import report for errors
- Verify the natural key doesn't already exist
- Check database constraints

### Production Safety
- EAM Core Masters Load and Demo Seed should NEVER run automatically
- Implement feature flags if needed for environments
- Always back up database before running in production
