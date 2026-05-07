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
| `seed/reference-data/OperationStatus.json` | seed/enum drift fix + data migration | [#5](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/5) |
| 5-seed coordinated alignment + missing InventoryStatus | (AssetStatus, CipProjectStatus, WorkRequestStatus, VendorStatus, ItemStatus, InventoryStatus) | [#6](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/6) |
| Lookup orphan + InventoryStatus dup cleanup | post-#6 verification residue | [#7](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/7) |
| `Pages/Maintenance/ScheduleBoard.cshtml.cs` | `OnPostAssignAsync`, `OnPostUnassignAsync` | [#10](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/10) |
| `Pages/Inventory/List.cshtml.cs` + `InventoryList` schema (StatusLookupValueId column + FK + backfill) | `OnPostStartAsync`, `OnPostCompleteAsync` | [#20](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/20) |
| `Pages/Maintenance/WorkRequests/Create.cshtml.cs` + `WorkRequest` schema (StatusLookupValueId column + FK + backfill) | `OnPostAsync` | [#21](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/21) |

## 🟢 All known FK-bound dropdown migrations are closed

Every entity surfaced by the audit + the cross-codebase sweep now has both
the legacy enum column and the matching `*LookupValueId` FK column, with
the canonical `SyncStatusFkAsync` helper writing both in lockstep on every
status transition. The seed/enum drift chain (PRs [#5](../pull/5),
[#6](../pull/6), [#7](../pull/7)) made this safe; the page handlers caught
up in PRs [#2](../pull/2), [#10](../pull/10), [#20](../pull/20),
[#21](../pull/21).

If a new entity is added with an `Enum Status` column going forward, follow
the pattern documented below — same `*LookupValueId` shape, same
`SyncStatusFkAsync` helper, same backfill migration template.

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
