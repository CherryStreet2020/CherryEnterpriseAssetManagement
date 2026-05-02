# Procurement-Grade Parts Architecture (Sprint 12)

## Overview

Sprint 12 extends the Item Master into a purchasing-ready foundation with three key relationship capabilities:

1. **Approved Vendor List (AVL)** - Define which vendors are approved to supply an item
2. **Alternates/Substitutes** - Support item substitution with ranking and approval
3. **Supersession Chain** - Track when old items are replaced by new items

## Data Model

### ItemApprovedVendor (AVL)

Tracks which vendors are approved to supply an item.

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| TenantId | int? | Tenant scope |
| CompanyId | int? | Company scope |
| SiteId | int? | Site scope |
| ItemId | int | FK to Item (required) |
| VendorId | int | FK to Vendor (required) |
| IsPreferred | bool | Only one preferred per item |
| ApprovalStatus | enum | Approved, Conditional, Blocked |
| Notes | string? | Optional notes |
| CreatedAtUtc | DateTime | Creation timestamp |
| CreatedByUserId | int? | User who created |

**Constraints:**
- Unique: (TenantId, ItemId, VendorId)
- At most ONE preferred vendor per item (enforced by service)

### ItemAlternate

Defines substitute/equivalent items with ranking.

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| TenantId | int? | Tenant scope |
| ItemId | int | Primary item (required) |
| AlternateItemId | int | Substitute item (required) |
| AlternateType | enum | Substitute, Equivalent, Upgrade, Downgrade |
| Rank | int | Priority (lower = better) |
| Reason | string? | Why this is an alternate |
| IsApproved | bool | Whether approved for use |
| CreatedAtUtc | DateTime | Creation timestamp |
| CreatedByUserId | int? | User who created |

**Constraints:**
- Unique: (TenantId, ItemId, AlternateItemId)
- Self-reference prevented: ItemId != AlternateItemId
- Deterministic ordering: by Rank, then AlternateItemId

### ItemSupersession

Tracks item replacement chains (old item -> new item).

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| TenantId | int? | Tenant scope |
| OldItemId | int | The superseded item |
| NewItemId | int | The replacement item |
| EffectiveFromUtc | DateTime? | When supersession takes effect |
| Reason | string? | Why superseded |
| CreatedAtUtc | DateTime | Creation timestamp |
| CreatedByUserId | int? | User who created |

**Constraints:**
- Unique: (TenantId, OldItemId) - one direct successor per old item
- Self-reference prevented: OldItemId != NewItemId
- Cycle prevention: Cannot create A->B->A chains

## Services

### IItemSourcingService

Manages the Approved Vendor List.

```csharp
interface IItemSourcingService
{
    Task<ItemApprovedVendor> SetApprovedVendorAsync(itemId, vendorId, status, isPreferred, notes);
    Task RemoveApprovedVendorAsync(itemId, vendorId);
    Task<List<ItemApprovedVendor>> GetApprovedVendorsAsync(itemId);
    Task<ItemApprovedVendor?> SetPreferredVendorAsync(itemId, vendorId);
    Task<ItemApprovedVendor?> GetPreferredVendorAsync(itemId);
}
```

**Business Rules:**
- When setting a vendor as preferred, automatically clears previous preferred
- Audit log entries: ITEM.AVL.UPDATED, ITEM.AVL.PREFERRED.SET

### IItemAlternateService

Manages alternates/substitutes.

```csharp
interface IItemAlternateService
{
    Task<ItemAlternate> AddAlternateAsync(itemId, alternateItemId, type, rank, reason, isApproved);
    Task RemoveAlternateAsync(itemId, alternateItemId);
    Task<List<ItemAlternate>> GetAlternatesAsync(itemId);
    Task<ItemAlternate?> GetBestAlternateAsync(itemId);
}
```

**Business Rules:**
- GetBestAlternate returns: approved only, lowest rank, tie-break by AlternateItemId
- Self-referencing alternates are rejected
- Audit log entries: ITEM.ALTERNATE.ADDED, ITEM.ALTERNATE.REMOVED

### IItemSupersessionService

Manages supersession chains.

```csharp
interface IItemSupersessionService
{
    Task<ItemSupersession> SetSupersessionAsync(oldItemId, newItemId, effectiveFromUtc, reason);
    Task RemoveSupersessionAsync(oldItemId);
    Task<List<Item>> GetSupersessionChainAsync(itemId);
    Task<Item?> ResolveCurrentItemAsync(itemId);
    Task<ItemSupersession?> GetSupersessionAsync(oldItemId);
    Task<ItemSupersession?> GetSupersededByAsync(newItemId);
}
```

**Business Rules:**
- Cycles are detected via graph walk and rejected
- ResolveCurrentItem follows chain to terminal item
- GetSupersessionChain returns full chain from starting item
- Audit log entries: ITEM.SUPERSESSION.SET, ITEM.SUPERSESSION.REMOVED

## UI Updates

Item Edit page (`/Materials/ItemEdit`) includes three new tabs:

### Approved Vendors Tab
- Table: Vendor, Status, Preferred badge, Notes
- Add form: Vendor dropdown, Status, Preferred checkbox, Notes
- Actions: Set Preferred, Remove

### Alternates Tab
- Table: Rank, Part Number, Description, Type, Approved, Reason
- Add form: Item dropdown, Type, Rank, Reason, Approved checkbox
- Actions: Remove

### Supersession Tab
- Shows current supersession status
- If superseded: Shows replacement item with remove option
- If not superseded: Form to set new supersession
- ResolveCurrentItem helper shows terminal item in chain

## Smoke Tests

### Test #24: AVL Single Preferred Vendor Enforcement
Verifies that only one vendor can be preferred at a time.

### Test #25: Alternates Deterministic Best Selection
Verifies GetBestAlternate returns approved item with lowest rank, stable tie-break.

### Test #26: Supersession Chain Integrity + Cycle Prevention
Verifies chain following and cycle detection.

## Tenant Scope

**Critical:** Preferred vendor resolution and all AVL/alternate/supersession operations are **tenant-scoped**. All tenant-scoped entities must carry `TenantId`.

### Tenant-Scoped Entities
These entities require TenantId for visibility in tenant-scoped queries:
- `ItemApprovedVendor` (AVL) - Has TenantId
- `ItemAlternate` - Has TenantId
- `ItemSupersession` - Has TenantId

### Non-Tenant-Scoped Entities
These entities are globally visible without tenant filtering:
- `VendorItemPart` - No TenantId, linked via Item/Vendor relationships
- `ItemManufacturerPart` - No TenantId

### Query Filtering
All service methods filter by the current tenant context:
```csharp
// Example: GetPreferredVendorAsync filters by TenantId
var tenantId = _tenantContext.TenantId;
return await _db.ItemApprovedVendors
    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ItemId == itemId && x.IsPreferred);
```

### Smoke Test Requirements
When creating tenant-scoped entities in smoke tests, **always use `SmokeTestDataFactory`** which automatically stamps `TenantId`:
```csharp
// Correct: Use factory
var avl = await _dataFactory.CreateAvlAsync(itemId, vendorId, isPreferred: true);

// Incorrect: Manual creation without TenantId will fail tenant-scoped queries
var avl = new ItemApprovedVendor { ... }; // Missing TenantId!
```

## Audit Log Actions

| Action | When Logged |
|--------|-------------|
| ITEM.AVL.UPDATED | Vendor added/updated in AVL |
| ITEM.AVL.PREFERRED.SET | Preferred vendor changed |
| ITEM.ALTERNATE.ADDED | Alternate added/updated |
| ITEM.ALTERNATE.REMOVED | Alternate removed |
| ITEM.SUPERSESSION.SET | Supersession created/updated |
| ITEM.SUPERSESSION.REMOVED | Supersession removed |
