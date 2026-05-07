# FK-bound Dropdown Migration — Status

The codebase is mid-migration from hardcoded enums to FK-bound `LookupValue`
references. Every dropdown should become a join from a `LookupTypeCode` (e.g.,
`"AssetStatus"`) to a `LookupValue` row. Each entity carries both the legacy
enum column AND a `*LookupValueId` FK column; on save, both are written
together so the FK and enum stay in lockstep.

The audit at [`docs/audit-2026-05-07/`](audit-2026-05-07/00_AUDIT_INDEX.md)
called this the "Sprint 0 #1" task and named `Pages/Purchasing/Details.cshtml.cs`
as the holdout. Direct inspection on 2026-05-07 found that page **fully
migrated**. The actual remaining gaps are listed below.

## ✅ Closed

| File | Handler | Shipped in |
|---|---|---|
| `Pages/Assets/Dispose.cshtml.cs` | `OnPostAsync` (Asset.Status=Disposed) | [#2](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/2) |
| `Pages/Materials/ItemEdit.cshtml.cs` | `OnPostObsoleteRevisionAsync` (RevisionStatus.Obsolete) | [#2](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/2) |

## 🟡 Open — order matters

These three are deferred from PR [#2](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/2)
because each requires more than a page edit. Do them in this order.

### 1. `OperationStatus` seed/enum drift — fix the seed first

The `OperationStatus` C# enum and the `OperationStatus.json` seed are drifted.
Mapping FK by enum int → seed code returns the wrong `LookupValue`. Until this
is fixed, **migrating `Pages/Maintenance/ScheduleBoard.cshtml.cs` would write
the wrong FK** (e.g., setting `op.Status = OperationStatus.Ready` (=1) would
look up code `"1"` and resolve "In Progress" as the LookupValue — wrong).

Drift table:

| Code | C# enum (`Models/WorkOrderOperation.cs:6`) | `OperationStatus.json` |
|---|---|---|
| 0 | `Pending` | "Pending" ✓ |
| 1 | `Ready` | "In Progress" ✗ |
| 2 | `InProgress` | "Completed" ✗ |
| 3 | `OnHold` | "On Hold" (close, but wrong value) |
| 4 | `Completed` | "Cancelled" ✗ |
| 5 | `Cancelled` | (missing) ✗ |

C# is the source of truth (heavily referenced). Update
`seed/reference-data/OperationStatus.json` to match — and add an EF Core data
migration that re-points existing `LookupValues` rows for `OperationStatus` so
in-flight operation rows don't reference now-renamed values. After the seed is
correct, migrate `Pages/Maintenance/ScheduleBoard.cshtml.cs::OnPostAssignAsync`
and `OnPostUnassignAsync` using the established `SyncStatusFkAsync` pattern.

> Tracked externally: GitHub issue
> [#3](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/issues/3)
> (created in error before this in-repo tracker existed; it can be closed once
> work starts here).

### 2. `InventoryList` — add FK column + migrate page

`Pages/Inventory/List.cshtml.cs::OnPostStartAsync` (line 71) and
`OnPostCompleteAsync` (line 85) write `InventoryStatus` enum without an FK
companion. The `InventoryList` entity (`Models/AssetInventory.cs:59`) has the
enum field but no `StatusLookupValueId` column.

Single PR:
1. Add `StatusLookupValueId` (`int?`) and `StatusLookupValue` (nav prop) to
   `InventoryList`.
2. EF Core migration `AddStatusLookupValueIdToInventoryList` with `Up` backfill:
   ```sql
   UPDATE "InventoryLists" SET "StatusLookupValueId" = (
     SELECT lv.id FROM "LookupValues" lv
     JOIN "LookupTypes" lt ON lv."LookupTypeId" = lt."Id"
     WHERE lt."Code" = 'InventoryStatus' AND lv."Code" = "InventoryLists"."Status"::int::text
   );
   ```
3. Update both handlers using the `SyncStatusFkAsync` pattern.
4. **First** verify `seed/reference-data/InventoryStatus.json` aligns with
   `InventoryStatus` enum. If drifted, fix it the same way as (1) above.

### 3. `WorkRequest` — add FK column + migrate page

`Pages/Maintenance/WorkRequests/Create.cshtml.cs::OnPostAsync` (line 238)
writes `WorkRequestStatus.New` without an FK companion. The `WorkRequest`
entity (`Models/WorkRequest.cs:41`) lacks a `StatusLookupValueId` column.

Single PR:
1. Add `StatusLookupValueId` + nav prop to `WorkRequest`.
2. EF Core migration with backfill (same shape as (2)).
3. Inject `ILookupService` into `CreateModel` (not currently injected).
4. Apply the FK pattern in `OnPostAsync`.
5. **First** verify `seed/reference-data/WorkRequestStatus.json` aligns with
   the enum.

## Pattern reference

The canonical pattern lives at
[`Pages/Purchasing/Details.cshtml.cs::SyncStatusFkAsync`](../Pages/Purchasing/Details.cshtml.cs):

```csharp
private async Task SyncStatusFkAsync(PurchaseOrder po, POStatus status)
{
    po.Status = status;
    var lv = await _lookupService.GetValueByCodeAsync(
        _tenantContext.TenantId, _tenantContext.CompanyId,
        "POStatus", ((int)status).ToString());
    if (lv != null)
        po.StatusLookupValueId = lv.Id;
}
```

## Verifying seed/enum alignment before any migration

Before writing the FK pattern for any entity, **always**:

1. Locate the C# enum (e.g., `Models/Asset.cs`, `Models/Enums.cs`).
2. Open the matching seed file (e.g., `seed/reference-data/AssetStatus.json`).
3. Confirm every enum value has a seed entry where the seed `code` is the enum
   int as a string AND the seed `name` is meaningful for the enum value.
4. If drifted, fix the seed first (and consider whether existing rows in the
   `LookupValues` table need a one-time data migration).

This is how PR [#2](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/2)
caught the `OperationStatus` drift.
