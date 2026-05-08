# ADR-003: Central GL account resolver

**Status:** proposed (2026-05-08).
**Closes audit finding:** S2-7 from [`docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md`](../audit-2026-05-08-followup/STRUCTURAL_AUDIT.md).
**Referenced by:** ADR-001, ADR-002, S1-4 (CIP capitalization).

---

## Context

GL accounts are scattered across the codebase with inconsistent resolution:

- `Services/Cip/CipCapitalizationService.cs:124,131` — string literals `"1500"` (Asset Cost) and `"1400"` (CIP Pending). No tenant override possible.
- `Pages/Assets/Dispose.cshtml.cs:170-188` — ad-hoc cascade across `Asset.GLAssetAccount` → `BookGlAccount` → `Book.GlAccount*`.
- `Services/JournalGenerator.cs:74-86` — depreciation uses `BookGlAccount` then falls back to `Book.GlAccountDepExp / GlAccountAccumDep`.
- `Models/Asset.cs:121-127` — `Asset.GLAssetAccount`, `Asset.GLAccumDepAccount`, `Asset.GLDepExpenseAccount` are denormalized strings.
- `Models/ConstructionInProgress.cs:127` — `CipCost.GlAccount` is a string set ad-hoc.

Onboarding a new tenant with a different chart of accounts requires editing each posting site's logic. Sprint 0.5's Receiving and AP work would compound the problem unless we centralize first.

## Decisions

### D-003-1. Single resolver interface

```csharp
public interface IGlAccountResolver
{
    /// <summary>Resolves the GL account string for a posting purpose.
    /// Throws GlAccountResolutionException with diagnostic detail
    /// when the cascade exhausts without a match — fail fast, never
    /// post to a wrong-because-blank account.</summary>
    Task<string> ResolveAsync(int companyId, GlAccountKind kind, GlResolveContext context);
}

public enum GlAccountKind
{
    // Asset side
    AssetCost = 100,
    AccumulatedDepreciation = 110,
    DepreciationExpense = 120,
    GainOnDisposal = 130,
    LossOnDisposal = 140,

    // Inventory / receiving side
    Inventory = 200,
    GrAccrued = 210,                   // Goods received not yet invoiced
    DirectExpense = 220,               // Non-stock item direct charge
    WipExpense = 230,                  // Work-in-progress (when PO line ties to a WO)

    // AP side
    AccountsPayable = 300,
    Cash = 310,
    PurchasePriceVariance = 320,

    // CIP side
    CipPending = 400,                  // Construction-in-progress accumulator

    // Maintenance side
    MaintenanceLabor = 500,
    MaintenanceMaterials = 510,
    MaintenanceOutsideVendor = 520,
}
```

`GlResolveContext` carries the optional inputs that change the resolution:

```csharp
public sealed record GlResolveContext(
    int? AssetId = null,
    int? BookId = null,
    int? PurchaseOrderLineId = null,
    int? VendorInvoiceLineId = null,
    int? WorkOrderId = null,
    int? CipProjectId = null);
```

### D-003-2. Cascade order

For each `(CompanyId, GlAccountKind)` resolve attempt, the resolver tries sources in this order, returning the first non-empty match:

1. **Per-entity explicit override** (when context carries a relevant entity). For example, `kind = AssetCost` with `context.AssetId` → checks `Asset.GLAssetAccount`. `kind = DepreciationExpense` with `context.BookId` and `context.AssetId` → checks `BookGlAccount` for that asset+book.
2. **Per-book defaults** when `context.BookId` set: `Book.GlAccountDepExp` etc.
3. **Per-company configuration** (a NEW table — see D-003-3): `CompanyGlAccountConfig` row for `(CompanyId, GlAccountKind)`.
4. **Industry default** (hard-coded fallback chart of accounts seeded for the demo tenant). Returns null → throws `GlAccountResolutionException` with the cascade history.

### D-003-3. New `CompanyGlAccountConfig` table

Per-tenant configuration of which GL account string to use for each `GlAccountKind`. One row per `(CompanyId, AccountKind)` combination.

```sql
CREATE TABLE "CompanyGlAccountConfigs" (
    "Id"           serial PRIMARY KEY,
    "CompanyId"    int NOT NULL REFERENCES "Companies"("Id"),
    "AccountKind"  int NOT NULL,         -- GlAccountKind enum int
    "GlAccount"    varchar(20) NOT NULL,
    "Notes"        varchar(500),
    "CreatedAt"    timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt"    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "UX_CompanyGlAccountConfigs_CompanyKind"
        UNIQUE ("CompanyId", "AccountKind")
);
```

Seeded for tenant 1 with the industry-default chart of accounts (the strings currently hardcoded in `CipCapitalizationService` and `DepreciationBackfillService`).

### D-003-4. Industry-default chart-of-accounts (seed values)

| Kind | Default | Source of truth today |
|---|---|---|
| `AssetCost` | `1500` | `CipCapitalizationService.cs:124` |
| `AccumulatedDepreciation` | `1510` | `DepreciationBackfillService.cs:46` |
| `DepreciationExpense` | `6500` | `DepreciationBackfillService.cs:47` |
| `GainOnDisposal` | `4500` | `DepreciationBackfillService.cs:49` |
| `LossOnDisposal` | `6510` | `DepreciationBackfillService.cs:50` |
| `Inventory` | `1300` | New |
| `GrAccrued` | `2150` | New |
| `DirectExpense` | `6000` | New |
| `WipExpense` | `1400` | Reused from CIP (TBD: split into WIP-1410 and CIP-1400) |
| `AccountsPayable` | `2000` | New |
| `Cash` | `1000` / `1110` | `DepreciationBackfillService.cs:48` (clearing) |
| `PurchasePriceVariance` | `5900` | New |
| `CipPending` | `1400` | `CipCapitalizationService.cs:131` |
| `MaintenanceLabor` | `6200` | New |
| `MaintenanceMaterials` | `6210` | New |
| `MaintenanceOutsideVendor` | `6220` | New |

A migration seeds these for every existing `Companies` row; new companies get them seeded by `MasterDataBootstrapService`.

### D-003-5. Caching

In-memory cache with 10-minute TTL keyed by `(CompanyId, GlAccountKind)`. Same pattern as `LookupService`. Cache invalidated when an admin updates `CompanyGlAccountConfigs` (event-driven invalidation via the existing service-layer pattern).

### D-003-6. Failure mode

`GlAccountResolutionException` is a structural error (configuration missing). It MUST surface to the user with enough detail to fix:

```
GL account resolution failed for CompanyId=100, Kind=Inventory.
Cascade: per-entity (n/a), per-book (n/a), per-company (no row), industry-default (1300).
This should not happen — please check that CompanyGlAccountConfigs has been seeded
for company 100 (run MasterDataBootstrapService).
```

In code paths that trigger from a user action (Approve, Receive), the exception bubbles to the page handler and shows in TempData["Error"] (PR [#25](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/25) pattern).

### D-003-7. Migration of existing call sites

This ADR's PR ships ONLY the resolver + seed. **It does NOT migrate existing call sites in this PR.** Each S1/S2 follow-up that needs a GL account migrates its own site to `IGlAccountResolver`:

- S1-4 PR migrates `CipCapitalizationService` (string literals → resolver call).
- S1-1 PR uses the resolver from day 1 in `ReceivingPostingService`.
- S1-5 PR uses the resolver in `ApPostingService`.
- The existing `DepreciationBackfillService` migration is the last step (~30 LOC change), in its own focused PR.

This minimizes blast radius and keeps each PR independently reviewable.

## Implementation phases

| Phase | Scope | Sizing |
|---|---|---|
| 1 | `IGlAccountResolver` interface + impl, `GlAccountKind` enum, `CompanyGlAccountConfig` model + migration + seed, DI registration | ~300 LOC |
| 2 | Tests: cascade ordering, per-entity overrides, per-company config, industry default, missing-config exception, cache invalidation | ~250 LOC |
| 3 | Migrate existing call sites (separate PRs, one per S1/S2 finding) | ~30 LOC each |
| 4 | Admin UI for editing `CompanyGlAccountConfigs` (deferred — admins can edit via psql for now) | ~150 LOC, follow-up PR |

## Migration

- **New table:** `CompanyGlAccountConfigs` (see D-003-3).
- **Seed:** for every existing `Companies` row, insert one row per enum value with the industry-default account string.
- **Hooks:** `MasterDataBootstrapService.SeedTenantAsync` extends to seed for new tenants.

## Open questions

1. **Multi-currency.** The resolver returns a string account code; the JE is currency-stamped on the entry, not the account. **Resolution: defer.** Multi-currency lands as its own ADR.
2. **Sub-accounts / departments / cost centers.** Some charts of accounts have segments like `1500-100-MAINT`. **Resolution: defer to a follow-up ADR.** The resolver returns a flat string today; segmentation lands later by extending the return type.
3. **Per-asset GL account override priority.** Today `Asset.GLAssetAccount` is denormalized. Should the resolver prefer it over `BookGlAccount`? **Recommend: yes** — explicit asset overrides win over book defaults. Documented in D-003-2.
4. **Cache invalidation across instances.** A single Replit instance is fine. When/if we scale to multiple, the existing `SeedGuardService` advisory-lock pattern can be extended for cache-bust events. **Resolution: defer** — single-instance for now.

## Tests

- `GlAccountResolverTests`:
  - `Resolve_PerEntityAssetOverride_TakesPrecedenceOverBook`
  - `Resolve_PerBookOverride_UsedWhenAssetOverrideMissing`
  - `Resolve_PerCompanyConfig_UsedWhenBookOverrideMissing`
  - `Resolve_IndustryDefault_UsedWhenAllElseMissing`
  - `Resolve_NoMatchAnywhere_ThrowsWithCascadeDetail`
  - `Resolve_Cache_HitsOnSecondCall_PerCompanyKind`
  - `Resolve_DifferentKindsForSameCompany_ResolvedIndependently`
  - `Seed_NewTenant_HasAllKindsConfigured`

## Schema diagram

```
Companies (1) ──< CompanyGlAccountConfigs (CompanyId, AccountKind, GlAccount)
                                              │
                                  IGlAccountResolver cascade:
                                   1. Asset.GLAssetAccount, etc.       (per-entity)
                                   2. BookGlAccount                    (per-book)
                                   3. Book.GlAccount* defaults         (per-book fallback)
                                   4. CompanyGlAccountConfigs          (per-tenant)
                                   5. Industry-default constants       (final fallback)
```
