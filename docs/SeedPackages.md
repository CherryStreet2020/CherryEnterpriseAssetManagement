# Seed Packages Documentation

## Overview

CherryAI Enterprise Asset Management uses a versioned seed package architecture with transactional pipelines for reliable, idempotent data seeding. This system supports audit trails and regression testing while ensuring data integrity through natural key-based upserts.

---

## Architecture

### Core Interfaces

| Interface | Purpose |
|-----------|---------|
| `ISeedPipeline` | Defines a named, versioned pipeline with ordered steps |
| `ISeedStep` | Individual domain step with natural key and execute logic |
| `ISeedPipelineExecutor` | Executes pipelines with transaction management and audit logging |

### Key Classes

| Class | Purpose |
|-------|---------|
| `SeedStepResult` | Per-step counts: Inserted/Updated/Skipped/Failed + warnings/errors |
| `PipelineResult` | Aggregate results for entire pipeline execution |
| `BaseSeedStep<TEntity>` | Abstract base with natural key lookup and upsert logic |
| `ValidationResult` | Per-table duplicate check results |
| `SeedValidationReport` | Aggregate validation across all key tables |

---

## Pipelines

### 1. SystemReferenceSeed (v1.0.0)
**Purpose:** Core system reference data required for all operations.

| Step | Domain | Natural Key | Records |
|------|--------|-------------|---------|
| WorkOrderTypes | WorkOrderTypes | Code | 12 |
| FailureCodes | FailureCodes | Code | 24 |
| CauseCodes | CauseCodes | Code | 17 |
| ActionCodes | ActionCodes | Code | 16 |
| ProblemCodes | ProblemCodes | Code | 14 |
| PriorityLevels | PriorityLevels | Code | 6 |
| Crafts | Crafts | Code | 15 |
| MaintenanceTypeCodes | MaintenanceTypeCodes | Code | 10 |
| LaborTypes | LaborTypes | Code | 10 |
| Skills | Skills | Code | 14 |
| NumberingSequences | NumberingSequences | Code | 10 |
| PaymentTerms | PaymentTerms | Code | 13 |
| Currencies | Currencies | Code | 10 |
| UOMDefinitions | UOMDefinitions | Code | 26 |
| ShippingMethods | ShippingMethods | Code | 11 |
| TaxCodes | TaxCodes | Code | 10 |
| Section179Limits | Section179Limits | TaxYear | 7 |
| BonusDepreciationRates | BonusDepreciationRates | TaxYear | 8 |
| CcaClasses | CcaClasses | ClassNumber | 25 |

**Total:** ~250 records

### 2. OrgAndFinanceSeed (v1.0.0)
**Purpose:** Organizational hierarchy and financial masters.

| Step | Domain | Natural Key | Records |
|------|--------|-------------|---------|
| GlAccounts | GlAccounts | AccountNumber | 62 |
| Sites | Sites | SiteCode | 5 |
| Departments | Departments | Code | 10 |
| CostCenters | CostCenters | Code | 10 |
| AssetCategories | AssetCategories | Code | 8 |
| ApprovalWorkflows | ApprovalWorkflows | Code | 6 |

**Total:** ~101 records

### 3. VendorsAndPartsFoundationSeed (v1.0.0)
**Purpose:** Vendor masters and item categories for purchasing/inventory.

| Step | Domain | Natural Key | Records |
|------|--------|-------------|---------|
| ItemCategories | ItemCategories | Code | 16 |
| Manufacturers | Manufacturers | Name | 10 |
| Vendors | Vendors | Code | 10 |
| LaborRates | LaborRates | Code | 10 |

**Total:** ~46 records

### 4. EamExecutionMastersSeed (v1.0.0)
**Purpose:** EAM execution configuration and technicians.

| Step | Domain | Natural Key | Records |
|------|--------|-------------|---------|
| Technicians | Technicians | Name | 10 |

**Total:** ~10 records

### 5. DemoScenarioSeed (v1.0.0) - **DEV ONLY**
**Purpose:** Sample data for development and testing.

| Step | Domain | Natural Key | Records |
|------|--------|-------------|---------|
| DemoAssets | Assets | AssetNumber | 10 |
| DemoItems | Items | PartNumber | 15 |

**Total:** ~25 records

**Important:** This pipeline is gated to Development environment only. Execution in non-Development environments will fail with a clear error message.

---

## Natural Key Rules

All steps use natural key-based idempotent upserts:

| Domain | Natural Key | Example |
|--------|-------------|---------|
| WorkOrderTypes | Code | "CM", "PM", "PDM" |
| FailureCodes | Code | "BEAR", "BELT", "GEAR" |
| CauseCodes | Code | "WEAR", "LACK", "IMPR" |
| ActionCodes | Code | "REP", "REPL", "ADJ" |
| ProblemCodes | Code | "NRUN", "NOIS", "VIBR" |
| PriorityLevels | Code | "EMER", "HIGH", "MED" |
| Crafts | Code | "ELEC", "MECH", "HVAC" |
| NumberingSequences | Code | "ASSET", "WO", "PO" |
| PaymentTerms | Code | "NET30", "NET45", "COD" |
| Currencies | Code | "USD", "CAD", "EUR" |
| GlAccounts | AccountNumber | "1000", "1720", "6500" |
| Sites | SiteCode | "HQ", "MFG1", "DIST" |
| Departments | Code | "MAINT", "PROD", "QA" |
| CostCenters | Code | "CC100", "CC200", "CC300" |
| AssetCategories | Code | "BLDG", "MACH", "EQUIP" |
| Vendors | Code | "GRNGR", "MCMSTR", "MOTION" |
| Items | PartNumber | "BRG-6205-2RS", "BLT-A68" |
| Technicians | Name | "John Smith", "Mike Johnson", etc. |
| Section179Limits | TaxYear | 2024, 2025, 2026 |
| BonusDepreciationRates | TaxYear | 2024, 2025, 2026 |
| CcaClasses | ClassNumber | 1, 8, 45 |

### Upsert Logic

For each record:
1. **Find by Natural Key:** `FindByNaturalKeyAsync()` looks up existing record
2. **Check for Changes:** `ShouldUpdate()` compares key fields
3. **Action:**
   - If not found → **Insert**
   - If found and changed → **Update**
   - If found and unchanged → **Skip**

---

## Transaction Management

All pipeline executions use EF Core's `CreateExecutionStrategy()` with explicit transactions:

```csharp
var executionStrategy = _context.Database.CreateExecutionStrategy();
await executionStrategy.ExecuteAsync(async () =>
{
    await using var transaction = await _context.Database.BeginTransactionAsync(ct);
    try
    {
        // Execute all steps
        foreach (var step in pipeline.Steps) { ... }
        await transaction.CommitAsync(ct);
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
});
```

**Key behaviors:**
- Entire pipeline runs in a single transaction
- Any step failure triggers rollback of all changes
- Retries are handled by the execution strategy

---

## Audit Receipts

### Per-Step Audit (AuditLogs table)
Each step writes an AuditLog entry with:
- `EntityType`: `SeedStep:{PipelineName}`
- `Action`: `SeedStepExecute`
- `Description`: `{Domain}: I={Inserted} U={Updated} S={Skipped} F={Failed}`
- `AfterJson`: Full step result as JSON

### Per-Pipeline Audit (BulkOperations + AuditLogs)
Each pipeline run writes:
- **BulkOperation record** with pipeline summary JSON
- **AuditLog record** with `EntityType: SeedPipeline` and `Action: PipelineCompleted`

---

## Validation Helper

The `ValidateSeedDataAsync()` method checks 14 key tables for natural key duplicates:

| Table | Natural Key |
|-------|-------------|
| WorkOrderTypes | Code |
| FailureCodes | Code |
| CauseCodes | Code |
| Crafts | Code |
| GlAccounts | AccountNumber |
| Sites | Code |
| Departments | Code |
| CostCenters | Code |
| AssetCategories | Code |
| Currencies | Code |
| NumberingSequences | Code |
| PaymentTerms | Code |
| Vendors | VendorCode |
| Items | PartNumber |

### Validation Result
```json
{
  "allValid": true,
  "tablesChecked": 14,
  "tablesWithIssues": 0,
  "results": [
    { "table": "WorkOrderTypes", "naturalKey": "Code", "isValid": true, "duplicateCount": 0 }
  ]
}
```

---

## API Endpoints

**Security Notice:** All seed execution endpoints are protected by a dual-gate mechanism requiring:
1. **Development Environment Only** - Endpoints return HTTP 403 in production
2. **Admin Role Authorization** - User must be authenticated with Admin role

### Endpoint Security Matrix

| Endpoint | Method | Dev-Only | Admin-Only | Description |
|----------|--------|----------|------------|-------------|
| `?handler=RunPipeline&name={name}` | GET | Yes | Yes | Run named pipeline |
| `?handler=RunPipelineJson&pipeline={key}` | GET | Yes | Yes | Run pipeline (JSON API) |
| `?handler=Validate` | GET | Yes | Yes | Validate seed data integrity |
| `?handler=RunSystemReferenceSeed` | GET | Yes | Yes | Run SystemReference pipeline |
| `?handler=RunEamCoreMastersLoad` | GET | Yes | Yes | Run EAM Core Masters (preferred) |
| `?handler=RunCustomerMasterLoad` | GET | Yes | Yes | Run EAM Core Masters (legacy alias) |
| `?handler=RunDemoSeed` | GET | Yes | Yes | Run Demo pipeline |
| `?handler=AuditReceipts` | GET | Yes | Yes | View recent audit logs |

### Security Implementation

```csharp
[Authorize(Roles = "Admin")]  // Page-level authorization
public class DataImportModel : PageModel
{
    private IActionResult? CheckDevAdminGate()
    {
        // Defense-in-depth: explicit checks in each endpoint
        if (!_env.IsDevelopment())
            return new JsonResult(new { error = "Seed endpoints are only available in Development mode" }) { StatusCode = 403 };
        
        if (!User.IsInRole("Admin"))
            return new JsonResult(new { error = "Seed endpoints require Admin role" }) { StatusCode = 403 };
        
        return null;
    }
}
```

### Error Responses

**Non-Development Environment:**
```json
{
  "error": "Seed endpoints are only available in Development mode"
}
```
HTTP Status: 403 Forbidden

**Missing Admin Role:**
```json
{
  "error": "Seed endpoints require Admin role"
}
```
HTTP Status: 403 Forbidden

### Run Pipeline
```
GET /Admin/DataImport?handler=RunPipeline&name={pipelineName}
```
Pipeline names: `SystemReference`, `OrgAndFinance`, `VendorsParts`, `EamExecution`, `Demo`

### Validate Seed Data
```
GET /Admin/DataImport?handler=Validate
```

### JSON API Endpoints
```
GET /Admin/DataImport?handler=RunPipelineJson&pipeline={key}
```
Pipeline keys: `system`, `org`, `vendors`, `eam`, `demo`

### Legacy Endpoints (still supported)
```
GET /Admin/DataImport?handler=RunSystemReferenceSeed
GET /Admin/DataImport?handler=RunEamCoreMastersLoad  (preferred)
GET /Admin/DataImport?handler=RunCustomerMasterLoad  (legacy alias, still works)
GET /Admin/DataImport?handler=RunDemoSeed
```

---

## Dev-Only Gating

Demo and test data pipelines are protected:

```csharp
public class DemoScenarioSeedPipeline : ISeedPipeline
{
    public bool IsDevOnly => true;  // <-- Gating flag
}
```

The executor checks this before running:
```csharp
if (pipeline.IsDevOnly && !_env.IsDevelopment())
{
    // Return error, do not execute
}
```

---

## Usage

### Service Registration
```csharp
// In Program.cs
builder.Services.AddSeedingServices();
```

### Running Pipelines Programmatically
```csharp
public class MyService
{
    private readonly ISeedPipelineExecutor _executor;
    private readonly SystemReferenceSeedPipeline _pipeline;

    public async Task SeedAsync()
    {
        var result = await _executor.ExecuteAsync(_pipeline);
        if (result.Success)
        {
            // Handle success
        }
    }
}
```

### From Admin UI
Navigate to `/Admin/DataImport` and use the pipeline buttons.

---

## Recommended Execution Order

For a fresh database:
1. **SystemReferenceSeed** - Core lookup tables
2. **OrgAndFinanceSeed** - Organization and financial structure
3. **VendorsAndPartsFoundationSeed** - Purchasing foundation
4. **EamExecutionMastersSeed** - Maintenance configuration
5. **DemoScenarioSeed** (dev only) - Sample data

---

*Generated: January 21, 2026*
