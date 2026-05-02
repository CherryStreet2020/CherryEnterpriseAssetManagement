# PM Schedule Canonical Model Documentation

**Date:** 2026-01-24  
**Author:** CherryAI Agent  
**Status:** Implemented

## Overview

This document describes the canonical PM Schedule model architecture. **PMSchedule** is the single source of truth for PM schedules, replacing legacy models like `MaintenanceSchedule` and `PMTemplateAsset` for KPI and display purposes.

## Canonical Model

**PMSchedule** is the canonical model for PM schedules. All user-facing KPIs and screens MUST use this model.

### Grain
PMSchedule is **per site-template group**: `(TenantId, CompanyId, SiteId, PMTemplateId)`
- Does NOT include AssetId
- One schedule per unique tenant/company/site/template combination

### Related Models
| Model | Purpose | Used By |
|-------|---------|---------|
| `PMSchedule` | Recurring schedule definition | Dashboard, Maintenance/Schedules, Admin/PMSchedules |
| `PMOccurrence` | Individual occurrences of a schedule | PM execution system |
| `PMTemplate` | Template for PM tasks | Schedule creation |
| `PMTemplateAsset` | Template-to-asset assignments | Source for PMSchedule derivation in seeding |
| `MaintenanceSchedule` | Legacy model | Retained for history, not used for new KPIs |

## Pages Using PMSchedule

### Dashboard (Index.cshtml.cs)
- KPIs: TotalSchedules, SchedulesDueThisWeek, SchedulesOverdue, ScheduledEventsCompleted
- Queries `PMSchedules.Where(s => s.Active)`
- **No tenant filtering** - shows all active schedules for system-wide visibility

### Maintenance/Schedules.cshtml
- Full list of PM schedules with details
- Queries `PMSchedules` **with tenant/company/site scoping** via ITenantContext
- Shows schedule name, template, company/site, cadence, next due, status
- Links to Admin/PMScheduleEdit for editing
- **May display local time** for user-friendly date formatting

### Admin/PMSchedules.cshtml
- Admin view for schedule management
- PM generation and preview
- Same tenant scoping as Maintenance/Schedules

## Scoping (ALIGNED)

| Page | Tenant Scoping | Time Display |
|------|----------------|--------------|
| Dashboard | Scoped by ITenantContext | UTC-based KPIs |
| Maintenance/Schedules | Scoped by ITenantContext | May use local time |
| Admin/PMSchedules | Scoped by ITenantContext | May use local time |

**IMPORTANT:** All pages now use the **same ITenantContext scoping**. Dashboard and Maintenance/Schedules KPI counts will always match for the same tenant/company/site context. This ensures consistency across all views.

## KPI Calculation Pattern (UTC-based)

```csharp
var todayUtc = DateTime.UtcNow.Date;
var weekEndUtc = todayUtc.AddDays(7);

// Total active schedules
TotalSchedules = pmSchedules.Count;

// Due this week
DueThisWeek = pmSchedules.Count(s => 
    s.NextDueDateUtc.HasValue && 
    s.NextDueDateUtc.Value.Date >= todayUtc && 
    s.NextDueDateUtc.Value.Date <= weekEndUtc);

// Overdue
Overdue = pmSchedules.Count(s => 
    s.NextDueDateUtc.HasValue && 
    s.NextDueDateUtc.Value.Date < todayUtc);
```

## Tenant/Company/Site Scoping

PMSchedules include TenantId, CompanyId, and SiteId fields:
- All seeded PMSchedules are stamped with tenant context from the first valid company
- Smoke test 56 validates that no active PMSchedules are missing CompanyId

## Seeding

### Seed Entry Point Map

| Entry Point | Pipeline(s) Executed | Seeds PMSchedules? |
|-------------|---------------------|-------------------|
| SmokeTestRunner (Test 27/28) | DemoPackV2Pipeline | ✅ Yes |
| Admin → Demo Data → Execute V2 | DemoPackV2Pipeline | ✅ Yes |
| Admin → Demo Data → Execute V1 | DemoPackV1Pipeline | ✅ Yes |
| Admin → Data Import → Run Seed Pack | SeedPackExecutor | ✅ Yes |
| MasterDataBootstrapService (startup) | N/A | ❌ No (only core reference data) |

### DemoPackV2Pipeline (Primary) - HARDENED
Seeds PMSchedules **derived from PMTemplateAsset assignments** with proper tenant/company/site context:
- **Derives schedules from canonical PMTemplateAsset assignments** (not hardcoded examples)
- Creates schedules for **ALL companies/sites** where PMTemplateAssets exist
- Natural key: `(CompanyId, SiteId, PMTemplateId)` - one schedule per unique combination
- Up to 5 schedules per site (MaxSchedulesPerSite limit)
- All have NextDueDateUtc set with varying offsets (-5 to +45 days) for KPI visibility
- Mix of overdue, due-soon, and upcoming schedules
- **Idempotent**: Skips existing schedules based on natural key
- **Fallback**: If no PMTemplateAssets exist, creates schedules from PM templates for all companies/sites

### DemoPackV1Pipeline (Legacy)
Seeds PMSchedules with proper tenant/company/site context:
- CNC Daily/Weekly/Monthly schedules
- Compressor, Crane, Robot, HVAC, Press schedules
- All have NextDueDateUtc set for immediate visibility

### SeedPackExecutor
Seeds PMSchedules as part of "EAM Execution Masters":
- Links to PM Templates by code (PM-CNC-*, PM-COMP-*, etc.)
- Creates schedules with varying intervals (1-30 days)

> **NOTE:** If PMSchedules = 0 in the live database, run **Admin → Demo Data → Execute DemoPackV2** to populate canonical PMSchedules.

## Manual Verification Checklist

1. **Seed DemoPackV2** (Admin → Demo Data → Execute DemoPackV2)
   - Verify PMSchedules step shows "Inserted > 0"
   - Note the company/site coverage

2. **Navigate to Dashboard** (`/`)
   - Verify PM Schedules section shows non-zero counts
   - Note: Active Schedules, Due This Week, Overdue counts

3. **Click "Manage Schedules"** in PM Schedules section
   - Should navigate to `/Maintenance/Schedules`

4. **Verify Schedules Page Counts Match Dashboard**
   - Total Schedules count == Dashboard Active Schedules
   - Due This Week count == Dashboard Due This Week
   - Overdue count == Dashboard Overdue
   - **These MUST match exactly** (same ITenantContext scoping)

5. **Verify Admin/PMSchedules**
   - Shows same schedules as Maintenance/Schedules
   - Can generate work orders from schedules

## Smoke Test (STRICT)

Test 56: "PM Schedule → Canonical Model & Tenant Isolation"

### Full Seeding Chain Executed (Transaction-Isolated)
The smoke test wraps all seeding in a database transaction that is ALWAYS rolled back, ensuring smoke tests are non-destructive.

**Transaction Pattern:**
```csharp
await using var transaction = await _db.Database.BeginTransactionAsync();
try
{
    // Execute seeding steps...
    // Validate results...
    await transaction.RollbackAsync(); // ALWAYS rollback
}
catch
{
    await transaction.RollbackAsync();
}
```

**Seeding Chain (all within same transaction):**
1. **DemoPackV2PMTemplatesSeedStep** - Seeds 12 PM templates with released revisions
2. **DemoPackV2PMTemplateAssetsSeedStep** - Assigns templates to first 30 assets per site
3. **DemoPackV2PMSchedulesSeedStep** - Derives schedules from template assignments

### Rollback Verification
The following tables are monitored for count changes:
- PMTemplates, PMTemplateAssets, PMSchedules, PMOccurrences, PMTemplateRevisions
- WorkRequests, MaintenanceEvents, AuditLogs, WorkOrderOperations
- Items, ItemRevisions, ItemManufacturerParts, VendorItemParts
- And many more...

If any table counts differ before/after the test suite, rollback is marked as FAILED.

### Validation Requirements

**MUST FAIL** if any of these conditions are not met:
1. **PMSchedules == 0** → CRITICAL failure (seeding did not work)
2. **Active PMSchedules missing CompanyId** → Tenant isolation breach
3. **No NextDueDateUtc populated** → Dashboard KPIs will show 0
4. **Invalid PMTemplateId references** → Data integrity violation
5. **All schedules have future due dates** → No overdue/due-soon for realistic testing

Additional validations:
- Reports companies/sites with schedules (verifies multi-site coverage)
- Reports KPI distribution (Overdue, DueThisWeek, Upcoming)
- Legacy MaintenanceSchedule count (retained for history)

## Migration Notes

### Breaking Changes
- Maintenance/Schedules now queries PMSchedule instead of PMTemplateAsset
- Dashboard now queries PMSchedule instead of MaintenanceSchedule
- UI columns updated to match PMSchedule fields

### Backwards Compatibility
- MaintenanceSchedule model retained for existing data
- PMTemplateAsset model retained for template-asset mappings
