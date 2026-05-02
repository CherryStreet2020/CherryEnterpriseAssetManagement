# CherryAI EAM Seeder Audit Report

**Audit Date:** 2026-01-22  
**Phase:** 0 (READ-ONLY)  
**Status:** Complete

---

## Executive Summary

The CherryAI EAM codebase has a well-architected, multi-layer seeding infrastructure with robust safety controls. The primary C# seeding system is **SAFE** with proper LAB gating, idempotent upserts, and stable natural keys. However, three Python scripts pose **HIGH** risk due to destructive operations and lack of environment gating.

---

## 1. Seeder Inventory

### 1.1 C# Seed Pipelines (Primary System)

| Path | Type | What It Seeds | How Invoked | Stable Keys |
|------|------|---------------|-------------|-------------|
| `Services/Seeding/Pipelines/SystemReferenceSeedPipeline.cs` | Pipeline | WO types, failure/cause/action/problem codes, crafts, priorities, numbering, currencies, tax limits, CCA classes | Admin UI `/Admin/DataImport` POST handlers | Yes (Code) |
| `Services/Seeding/Pipelines/OrgAndFinanceSeedPipeline.cs` | Pipeline | GL accounts, cost centers, departments, asset categories, fiscal calendars | Admin UI `/Admin/DataImport` | Yes (AccountNumber, Code) |
| `Services/Seeding/Pipelines/VendorsAndPartsFoundationSeedPipeline.cs` | Pipeline | Vendors, items, manufacturer parts | Admin UI `/Admin/DataImport` | Yes (Code, PartNumber) |
| `Services/Seeding/Pipelines/EamExecutionMastersSeedPipeline.cs` | Pipeline | PM templates, technicians, labor rates | Admin UI `/Admin/DataImport` | Yes (TemplateCode, Name) |
| `Services/Seeding/Pipelines/DemoScenarioSeedPipeline.cs` | Pipeline | Demo assets, maintenance events, schedules | Admin UI `/Admin/DataImport` (Dev-only) | Yes (AssetNumber) |

### 1.2 C# SeedPack Executor (Alternative System)

| Path | Type | What It Seeds | How Invoked | Stable Keys |
|------|------|---------------|-------------|-------------|
| `Services/Seeding/SeedPackExecutor.cs` | Executor | Books, Companies, Sites, Locations, Vendors, Technicians, Depreciation Policies, Assets, PM Templates, Maintenance Events/Schedules | Admin UI `/Admin/DataImport?handler=RunSeedPack&packId=X` | Yes (Code, AssetNumber, SiteCode, etc.) |

### 1.3 C# Bootstrap Service (Legacy)

| Path | Type | What It Seeds | How Invoked | Stable Keys |
|------|------|---------------|-------------|-------------|
| `Services/MasterDataBootstrapService.cs` | Service | System reference data, EAM core masters, demo data | Admin UI `/Admin/DataImport` legacy handlers | Yes |

### 1.4 Python Scripts (External)

| Path | Type | What It Seeds | How Invoked | Stable Keys |
|------|------|---------------|-------------|-------------|
| `seed_demo_data.py` | Python Script | Journal entries, asset transfers, capital improvements, CCA transactions, audit logs, CCA class balances | Manual CLI: `python seed_demo_data.py` | **NO** (uses DELETE first) |
| `seed_full_demo.py` | Python Script | Inventory lists/scans, maintenance events/schedules, CIP projects/costs, bulk operations, API keys, audit logs | Manual CLI: `python seed_full_demo.py` | Partial (ON CONFLICT DO NOTHING) |
| `import_assets.py` | Python Script | Assets from Excel file | Manual CLI: `python import_assets.py` | **NO** (DELETE FROM Assets first) |

---

## 2. Risk Classification

### 2.1 SAFE Seeders ✅

| Seeder | Risk Level | Justification |
|--------|------------|---------------|
| `SystemReferenceSeedPipeline` | SAFE | Insert-if-missing by Code, no deletes, LAB gated via `SeedGuardService`, transactional |
| `OrgAndFinanceSeedPipeline` | SAFE | Insert-if-missing by natural key, no deletes, LAB gated, transactional |
| `VendorsAndPartsFoundationSeedPipeline` | SAFE | Insert-if-missing by Code/PartNumber, no deletes, LAB gated, transactional |
| `EamExecutionMastersSeedPipeline` | SAFE | Insert-if-missing by TemplateCode, no deletes, LAB gated, transactional |
| `DemoScenarioSeedPipeline` | SAFE | Insert-if-missing by AssetNumber, no deletes, LAB gated, marked `IsDevOnly=true` |
| `SeedPackExecutor` | SAFE | Insert-if-missing by stable keys, uses `SeedGuardService`, transactional |

### 2.2 MEDIUM Risk Seeders ⚠️

| Seeder | Risk Level | Justification |
|--------|------------|---------------|
| `seed_full_demo.py` | MEDIUM | Uses `ON CONFLICT DO NOTHING` (safe), but no environment gating, no stable keys for all tables |

### 2.3 HIGH Risk Seeders 🚨

| Seeder | Risk Level | Justification |
|--------|------------|---------------|
| `seed_demo_data.py` | **HIGH** | Line 32-38: Executes `DELETE FROM` on JournalLines, JournalEntries, AssetTransfers, CapitalImprovements, CcaTransactions, AuditLogs before seeding. No LAB gating. |
| `import_assets.py` | **HIGH** | Line 85: Executes `DELETE FROM "Assets"` before importing. No LAB gating. Direct production risk. |

---

## 3. Gating Status Analysis

### 3.1 Environment Gating (LAB/Development Checks)

| Component | LAB Gated | How |
|-----------|-----------|-----|
| `SeedGuardService.CheckSeedPermission()` | ✅ | Checks `ASPNETCORE_ENVIRONMENT == Development`, database name for 'lab'/'demo'/'prod' keywords |
| `SeedPipelineExecutor.ExecuteAsync()` | ✅ | Checks `IsDevOnly` flag, Development environment |
| `DataImportModel.CheckDevAdminGate()` | ✅ | Checks `_env.IsDevelopment()` |
| `seed_demo_data.py` | ❌ | No environment check |
| `seed_full_demo.py` | ❌ | No environment check |
| `import_assets.py` | ❌ | No environment check |

### 3.2 Admin Role Gating

| Component | Admin Gated | How |
|-----------|-------------|-----|
| `/Admin/DataImport` page | ✅ | `[Authorize(Roles = "Admin")]` attribute |
| JSON endpoints in DataImport | ✅ | `CheckDevAdminGate()` checks `User.IsInRole("Admin")` |
| Python scripts | ❌ | CLI only, no authentication |

### 3.3 Preview/Dry-Run Support

| Component | Has Preview | Notes |
|-----------|-------------|-------|
| C# Pipelines | ⚠️ Partial | Reports inserted/updated/skipped counts but no pre-execution preview |
| SeedPackExecutor | ⚠️ Partial | Reports results post-execution |
| `SeedValidationReport` | ✅ | Validates duplicate keys without writing |
| Python scripts | ❌ | Execute-only, no dry-run |

---

## 4. Idempotency Analysis

### 4.1 Stable Natural Keys by Domain

| Domain | Natural Key | Used In |
|--------|-------------|---------|
| WorkOrderTypes | `Code` | ✅ All C# pipelines |
| FailureCodes | `Code` | ✅ All C# pipelines |
| CauseCodes | `Code` | ✅ All C# pipelines |
| Crafts | `Code` | ✅ All C# pipelines |
| GlAccounts | `AccountNumber` | ✅ All C# pipelines |
| Sites | `SiteCode` | ✅ All C# pipelines |
| Departments | `Code` | ✅ All C# pipelines |
| CostCenters | `Code` | ✅ All C# pipelines |
| AssetCategories | `Code` | ✅ All C# pipelines |
| Vendors | `Code` | ✅ All C# pipelines |
| Items | `PartNumber` | ✅ All C# pipelines |
| Companies | `CompanyCode` | ✅ SeedPackExecutor |
| Locations | `Code` | ✅ SeedPackExecutor |
| Books | `Code` | ✅ SeedPackExecutor |
| DepreciationPolicies | `Code` | ✅ SeedPackExecutor |
| Assets | `AssetNumber` | ⚠️ C# yes, Python no (uses DELETE) |
| PMTemplates | `TemplateCode` | ✅ All C# pipelines |

### 4.2 C# BaseSeedStep Pattern

The C# seeders use a consistent upsert pattern via `BaseSeedStep<T>`:

```csharp
protected override async Task<WorkOrderType?> FindByNaturalKeyAsync(WorkOrderType item, CancellationToken ct)
    => await Context.WorkOrderTypes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);

protected override bool ShouldUpdate(WorkOrderType existing, WorkOrderType incoming)
    => !StringEquals(existing.Name, incoming.Name) || ...;

protected override void UpdateEntity(WorkOrderType existing, WorkOrderType incoming)
{
    existing.Name = incoming.Name;
    // ... update other fields
}
```

This pattern ensures:
- **Insert-if-missing:** Only inserts if `FindByNaturalKeyAsync` returns null
- **Update-if-changed:** Only updates if `ShouldUpdate` returns true
- **Skip-if-same:** Skips entirely if record exists and hasn't changed

---

## 5. Uppercase String Mutation Risk

**Finding:** No evidence of uppercase string mutation that could affect seeded secrets or payloads.

- Natural keys (Codes) are stored as-is
- No `ToUpper()` or `ToLower()` transformations on secrets
- API key hashing in `seed_full_demo.py` uses `hashlib.sha256` directly

---

## 6. Proposed DB-Safe Seeding Contract

### 6.1 Core Principles

1. **Upsert-by-Stable-Key Only**
   - Every entity must have a declared natural key (Code, AccountNumber, PartNumber, etc.)
   - All inserts must check for existing record by natural key first
   - No blind inserts without duplicate checking

2. **No Deletes**
   - Seeders must never execute DELETE, TRUNCATE, or DROP
   - Obsolete records should be marked as `IsActive = false`
   - Only exception: Transaction rollback on failure

3. **Preview Mode (WouldCreate/WouldUpdate/WouldSkip)**
   - Every seeder must support a dry-run mode
   - Preview returns counts without database modification
   - UI shows preview before user confirms execution

4. **Audit Trail**
   - Every seed execution must create an AuditLog entry
   - Action type: `DEMOSEED` or `SEED`
   - Include correlationId, pipeline name, version, counts

5. **LAB-Only + Admin-Only**
   - All seeders must check `SeedGuardService.CheckSeedPermission()`
   - All UI endpoints must have `[Authorize(Roles = "Admin")]`
   - JSON/API endpoints must verify both environment and role

---

## 7. Recommendations

### 7.1 What to KEEP ✅

| Component | Action | Reason |
|-----------|--------|--------|
| All 5 C# Seed Pipelines | Keep | Well-designed, safe, idempotent |
| `SeedGuardService` | Keep | Critical safety gate |
| `SeedPackExecutor` | Keep | Alternative safe seeding path |
| `SeedPipelineExecutor` | Keep | Proper transaction handling |
| `/Admin/DataImport` page | Keep | Good Admin-only UI |

### 7.2 What to MODIFY ⚠️

| Component | Modification | Priority |
|-----------|--------------|----------|
| `seed_full_demo.py` | Add LAB environment check at start, add dry-run mode | Medium |
| All C# Pipelines | Add `PreviewAsync()` method returning WouldCreate/WouldUpdate/WouldSkip | Low |
| DataImport page | Add preview buttons before each seed action | Low |

### 7.3 What to BLOCK 🚨

| Component | Action | Reason |
|-----------|--------|--------|
| `seed_demo_data.py` | **BLOCK EXECUTION** - Mark as deprecated, add startup check | Uses DELETE, no LAB gate |
| `import_assets.py` | **BLOCK EXECUTION** - Mark as deprecated, add startup check | Uses DELETE, no LAB gate |

### 7.4 Immediate Actions (Zero-Risk)

1. Add warning comments to Python scripts:
   ```python
   # WARNING: DEPRECATED - DO NOT RUN
   # This script uses DELETE operations and has no environment protection.
   # Use /Admin/DataImport instead.
   ```

2. Create `.github/workflows/block-dangerous-scripts.yml` to prevent accidental execution

---

## 8. Implementation Plan

### Phase 1: Preview Mode + Safety Gates (Recommended Next)

1. **Add `PreviewAsync()` to `ISeedPipeline` interface**
   - Returns `PreviewResult` with WouldCreate/WouldUpdate/WouldSkip counts
   - No database writes

2. **Add preview buttons to `/Admin/DataImport` UI**
   - "Preview" button for each pipeline
   - Shows results before "Execute" is enabled

3. **Add LAB check to Python scripts**
   - Immediate abort if `REPLIT_ENVIRONMENT != 'LAB'`
   - Print warning and exit

4. **Create unified orchestrator service**
   - `ISeedOrchestrator.RunAllAsync()` - runs pipelines in deterministic order
   - Order: SystemReference → OrgAndFinance → VendorsParts → EamExecution → Demo

### Phase 2: Execution + Smoke Tests

1. **Add idempotency smoke test**
   - Run seed twice in same transaction
   - Assert second run shows "0 new created"
   - Rollback transaction

2. **Add audit receipts**
   - Every seed creates `AuditLog` with:
     - Action: `SEED_PIPELINE`
     - EntityType: Pipeline name
     - Description: `{inserted} inserted, {updated} updated, {skipped} skipped`
     - AfterJson: Full result summary

3. **Retire Python scripts**
   - Move to `scripts/deprecated/`
   - Remove from any automation

---

## 9. Smoke Test Coverage

Current smoke tests (`/Admin/SmokeTests`) do not explicitly test seeding idempotency. Recommended additions:

| Test ID | Test Name | Description |
|---------|-----------|-------------|
| #15 | Seed Idempotency - SystemReference | Run SystemReferenceSeedPipeline twice, assert 0 inserts on second run |
| #16 | Seed Idempotency - OrgAndFinance | Run OrgAndFinanceSeedPipeline twice, assert 0 inserts on second run |
| #17 | Preview vs Execute Consistency | Run preview, then execute, assert counts match |

---

## 10. Files Changed in This Audit

- `docs/SeederAudit.md` - Created (this document)

**No functional code changes were made. Phase 0 is complete.**

---

## Appendix A: SeedGuardService Logic Flow

```
CheckSeedPermission()
    ├── If !IsDevelopment() → BLOCKED
    ├── If IsDemoEnvironment() && !ALLOW_DEMO_SEED → BLOCKED  
    ├── If DB name contains 'prod'/'demo' && !ALLOW_DEMO_SEED → BLOCKED
    └── Else → ALLOWED
```

## Appendix B: Pipeline Execution Order (Recommended)

1. SystemReferenceSeedPipeline (foundational codes)
2. OrgAndFinanceSeedPipeline (organizational structure)
3. VendorsAndPartsFoundationSeedPipeline (supply chain)
4. EamExecutionMastersSeedPipeline (maintenance masters)
5. DemoScenarioSeedPipeline (transactional demo data)

---

---

## Phase 1 Completed — 2026-01-22

### Changes Implemented

**A) Python Scripts Locked Down**
- `seed_demo_data.py`: Added hard-stop safety guard requiring:
  - `ALLOW_DANGEROUS_SEED_SCRIPTS=I_UNDERSTAND_THIS_CAN_DELETE_DATA`
  - `ASPNETCORE_ENVIRONMENT=Development` or `LAB`
  - Clear deprecation banner warning about DELETE operations
- `import_assets.py`: Same safety guards applied
- `seed_full_demo.py`: Added environment check (medium risk script)

**B) Preview/Dry-Run Mode Added**
- Added `PreviewStepResult` and `PreviewResult` classes to `ISeedPipeline.cs`
- Added `PreviewAsync()` method to `ISeedStep` interface and `BaseSeedStep<T>` implementation
- Added `PreviewAsync()` method to `ISeedPipelineExecutor` interface and implementation
- Added preview POST handlers for all 5 pipelines in `/Admin/DataImport`
- Added JSON endpoint `OnGetPreviewPipelineAsync(string name)` for API access

**Preview Mode Guarantees:**
- Uses `FindByNaturalKeyAsync()` to check existence (read-only)
- Never calls `Context.SaveChangesAsync()`
- Never attaches entities to the change tracker
- Returns WouldCreate/WouldUpdate/WouldSkip counts only

**C) Smoke Tests Added**
- Test #15: `Seed Idempotency - SystemReferenceSeedPipeline`
  - Runs seed twice in transaction, verifies second run inserts 0
- Test #16: `Seed Idempotency - OrgAndFinanceSeedPipeline`
  - Runs seed twice in transaction, verifies second run inserts 0
- Test #17: `Seed Preview vs Execute Consistency`
  - Runs preview, then execute, then preview again
  - Verifies execute counts <= preview counts
  - Verifies post-execute preview shows WouldCreate=0

All smoke tests run inside transactions with automatic rollback.

### Files Modified
- `Services/Seeding/ISeedPipeline.cs` - Added PreviewStepResult, PreviewResult classes, ISeedStep.PreviewAsync()
- `Services/Seeding/BaseSeedStep.cs` - Added PreviewAsync() implementation
- `Services/Seeding/SeedPipelineExecutor.cs` - Added PreviewAsync() to interface and implementation
- `Pages/Admin/DataImport.cshtml.cs` - Added preview handlers for all pipelines
- `Services/Testing/SmokeTestRunner.cs` - Added tests #15, #16, #17
- `seed_demo_data.py` - Added safety guards and deprecation banner
- `import_assets.py` - Added safety guards and deprecation banner  
- `seed_full_demo.py` - Added environment check
- `docs/SeederAudit.md` - This section

### Verification
- All 17 smoke tests pass
- Rollback verified (all table counts unchanged)
- Python scripts blocked without explicit override

---

## Phase 2 Completed — 2026-01-22

### Demo Data Pack v1 (LAB-only)

**Goal:** Seed a persistent, high-quality demo dataset in LAB environment for product demonstrations.

**A) New Demo Seed Pipeline: DemoPackV1Pipeline**
- File: `Services/Seeding/Pipelines/DemoPackV1Pipeline.cs`
- Contains 3 seed steps executed in order:
  1. `DemoPackV1AssetsSeedStep` - 20 demo assets with DEMO-* prefix
  2. `DemoPackV1PMTemplatesSeedStep` - 10 PM templates with PM-* prefix
  3. `DemoPackV1PMSchedulesSeedStep` - 10 PM schedules linked to templates

**Demo Asset Categories (20 total):**
- CNC Machines: VMC-1100, LAT-600, EDM-400
- Presses: HP-200, MP-100
- Robots: WR-2000, PAL-500
- Compressors: RSC-100, RSC-50
- Cranes: OBC-20, JIB-5
- Conveyors: PRC-100, PBC-50
- Forklifts: EF-5000, PF-8000
- HVAC: RTU-500, SS-250
- Grinder, Saw, Pump

**PM Template Categories (10 total):**
- Daily: CNC-DAILY, FORK-DAILY
- Weekly: CNC-WEEKLY, COMP-WEEKLY, ROBOT-WEEKLY, PRESS-WEEKLY
- Monthly: CNC-MONTHLY, CRANE-MONTHLY, HVAC-MONTHLY
- Quarterly: COMP-QUARTERLY

**PM Schedule Types:**
- IntervalDays (daily inspections)
- Weekly (Monday/Tuesday/Wednesday/Thursday schedules)
- Monthly (day-of-month schedules)

**B) Admin UI: /Admin/DemoData Page**
- LAB-only + Admin-only access (uses SeedGuardService)
- Preview button: Shows WouldCreate/WouldUpdate/WouldSkip counts without writing
- Run button: Executes seed with summary results
- Toggle: "Generate Due Work Orders (Next 30 Days)" - calls PMSchedulerService.GenerateDueAsync
- Shows current counts for Demo Assets, PM Templates, PM Schedules, Work Orders

**C) Audit Logging**
- Creates AuditLog entry with Action = "DEMOSEED"
- Includes correlationId, execution counts, user, timestamp
- AfterJson contains full step-by-step summary

**D) Safety Checks**
- Seed Guard blocks execution in non-LAB environments
- All seeds are idempotent (upsert by stable natural key)
- No DELETE operations - only INSERT/UPDATE
- Preview mode is read-only (no database writes)

**Natural Keys:**
- Assets: AssetNumber (DEMO-*)
- PM Templates: Code (PM-*)
- PM Schedules: Name (descriptive schedule name)

### Files Added/Modified
- `Services/Seeding/Pipelines/DemoPackV1Pipeline.cs` (NEW)
- `Services/Seeding/SeedingServiceExtensions.cs` (registered DemoPackV1Pipeline)
- `Pages/Admin/DemoData.cshtml` (NEW)
- `Pages/Admin/DemoData.cshtml.cs` (NEW)
- `docs/SeederAudit.md` (this section)

### Release Note
Demo Data Pack v1 added (LAB-only) - Seeds 20 demo assets, 10 PM templates, and 10 PM schedules for product demonstrations. Accessible via /Admin/DemoData.

---

*End of Audit Report*
