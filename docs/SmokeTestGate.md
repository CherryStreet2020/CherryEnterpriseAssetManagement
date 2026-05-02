# Smoke Test Gate

## Purpose
Lightweight release gate to verify the EAM loop works before shipping. All checks must pass before deploying.

---

## 1. Must-Pass Routes

| Route | Success Criteria |
|-------|------------------|
| `/Maintenance` | Page loads, shows Work Orders KPI cards (Total, Overdue, In Progress, Completed) |
| `/Maintenance/Schedules` | Page loads, shows active PM assignments with NextDueDate and "Create WO" buttons |
| `/Maintenance/Assignments` | Page loads, shows PMTemplateAsset list with Create/Deactivate/Delete actions |
| `/Admin/PMTemplates` | Page loads, shows PM Template list with template codes and intervals |
| `/Admin/DataImport` | Page loads, shows 3 pipeline buttons (System Reference, EAM Core Masters, Demo Seed) |
| Work Order Create Modal | Modal opens, asset dropdown populates, form submits successfully |

---

## 2. Must-Pass Workflow (Core EAM Loop)

### Test Scenario
Use Asset **AST-00001** which has 2 active assignments (Id=1, Id=4).

### Steps

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Navigate to `/Maintenance/Assignments` | See existing assignments listed |
| 2 | Create new assignment (or use existing Id=4) | PMTemplateAsset row created with NextDueDate calculated |
| 3 | Navigate to `/Maintenance/Schedules` | Assignment appears in schedules list |
| 4 | Click "Create WO" for assignment Id=4 | URL includes `?createFor=1&pmtaId=4`, modal opens with asset pre-selected |
| 5 | Submit WO creation | WO created with `CustomField1 = "PMTA:4"` |
| 6 | Complete the WO | WO status changes to Completed |
| 7 | Verify assignment updates | **ONLY** assignment Id=4 updated: LastCompletedDate set, NextDueDate advanced |
| 8 | Verify other assignments unchanged | Assignment Id=1 has no changes to LastCompletedDate/NextDueDate |

### Verification Query
```sql
SELECT "Id", "AssetId", "LastCompletedDate", "NextDueDate"
FROM "PMTemplateAssets"
WHERE "AssetId" = 1
ORDER BY "Id";
```

---

## 3. Idempotency Checks

### System Reference Seed
```
Run 1: X records inserted
Run 2: 0 records inserted (all skipped as duplicates)
```
**Pass Criteria:** Second run inserts = 0

### EAM Core Masters Load
```
Run 1: Y records inserted
Run 2: 0 records inserted (no duplicates by natural keys)
```
**Pass Criteria:** No duplicate violations, second run inserts = 0

---

## 4. Required Receipts to Pass Gate

### Row Counts
```sql
SELECT 'Assets' as entity, COUNT(*) as count FROM "Assets"
UNION ALL
SELECT 'PMTemplates', COUNT(*) FROM "PMTemplates"
UNION ALL
SELECT 'PMTemplateAssets', COUNT(*) FROM "PMTemplateAssets";
```

**Minimum Required:**
- Assets > 0
- PMTemplates > 0
- PMTemplateAssets > 0

### PMTA Linkage Proof
```sql
SELECT "Id", "CustomField1"
FROM "MaintenanceEvents"
WHERE "CustomField1" LIKE 'PMTA:%'
LIMIT 5;
```

**Pass Criteria:** At least 1 WO with `CustomField1` starting with `PMTA:`

---

## 5. Gate Checklist

- [ ] All 6 routes load without errors
- [ ] Core EAM loop workflow completes successfully
- [ ] PMTA linkage verified (CustomField1 set on WO creation)
- [ ] Single-assignment update verified (only linked assignment changes)
- [ ] Idempotency verified for both seed pipelines
- [ ] Row counts meet minimums

---

## Date
2026-01-21
