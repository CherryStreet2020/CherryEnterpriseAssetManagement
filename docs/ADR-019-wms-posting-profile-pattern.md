# ADR-019 — WMS Hierarchy + Posting Profile Pattern

**Status:** Accepted
**Date:** 2026-05-24
**Sprint:** 13.5 PRA-7 (Master Files Baseline cascade ship #5 of 8)
**Authors:** Dean Dunagan (architectural directive), Claude (drafting)
**Supersedes:** N/A
**Related:**
- ADR-003 central-gl-account-resolver (legacy GL routing — PostingProfile becomes the primary path; ADR-003 stays as fallback)
- ADR-011 industrial-sensor-data-architecture (UOM master alignment with PRA-4)
- ADR-015 industry-agnostic-receipt-schema (the consumer of PostingProfile on inbound)
- ADR-022 chain-of-custody-graph-layer (Bin / Lot / Serial movement edges)
- ADR-025 service-layer-standard (every service that posts inventory hits PostingProfile)
- `docs/research/master-files-baseline-2026-05-24.md` §6.4 + §6.5
- memory: `reference_master_files_baseline.md`, `reference_bic_entity_checklist.md`
- memory: `feedback_no_shortcuts_multi_tenant_lineage.md`

---

## Context

Before PRA-7 the IndustryOS schema collapsed three distinct concerns onto the single existing `Location` entity:

1. **Premises** — addresses, time zones, site managers (real shape lives on `Site`, fine).
2. **Asset hierarchy** — Building / Floor / Bay / Rack / Shelf / Station for maintenance + cost-center attachment (`Location` carries it cleanly).
3. **Inventory accounting** — the warehouse-or-stock-room view that anchors GL posting, mixing rules, capacity, and put-away/picking logic.

That third concern was missing as a first-class entity. `Warehouse` was a string column on a handful of receiving + production tables; `Bin` was a string column on the inventory snapshots; lot and serial numbers were flat strings with no traceability backbone. There was no posting-profile matrix — every inventory event called into `ADR-003 central-gl-account-resolver`, which used hard-coded conditional logic to pick a GL account from a few standing categories. That worked for a single-vertical FAR-asset demo (the original IndustryOS scope), but it falls over the moment a tenant has:

- More than one warehouse type at one site (a plant with a separate consignment cage, a DC with a returns area).
- Items in groups that need different posting per warehouse (raw materials at the receiving DC post differently than the same items issued at a satellite plant).
- Sub-assemblies, subcontract inventory, consignment inventory, scrap inventory, in-transit inventory — all of which got new GL category buckets in PRA-5a but had nothing wiring them to events.

Dean called the question directly on the 2026-05-24 morning call: *"We need to make sure once again that we have the infrastructure set up first so we don't chase our tails finding stuff we missed."* (Memory: `reference_master_files_baseline.md`.) PRA-7 is the answer to that for the inventory + traceability + GL-routing slice.

The question that needed a decision before writing code: **what hierarchy do we use for warehouse + bin + lot + serial?** The industry isn't unanimous. Two strong-and-incompatible patterns exist.

## Decision

Adopt **Option D — the SAP S/4HANA + Microsoft Dynamics 365 Finance & Operations separation-of-concerns model:**

```
Site (EXISTING — premises, address, time zone, manager)
  ├── Location (EXISTING — asset hierarchy: Building/Floor/Bay/Rack/Shelf/Station)
  │       └── Assets, PM templates, cost centers attach here
  └── WarehouseMaster (NEW — financial inventory unit, anchors PostingProfile)
        ├── WarehouseType: DistributionCenter / Plant / ThirdPartyLogistics
        │                  / Consignment / Quarantine / Returns / Scrap
        │                  / WorkInProcess / InTransit / Other
        ├── DefaultInventoryGlAccountId, DefaultCogsGlAccountId,
        │   DefaultScrapGlAccountId, DefaultVarianceGlAccountId
        ├── IsConsignment / IsBonded / IsTaxOnReceipt / IsQuarantine flags
        └── BinMaster (NEW — physical leaf inventory location)
              ├── Zone / Aisle / Bay / Level / Position
              ├── BinType: Pallet / Case / Each / Bulk / Receiving / Staging
              │             / Shipping / Quarantine / Returns / Scrap
              ├── MixingRule: Mixed / SingleSku / SingleLot / SingleSerial
              │                / SingleSkuSingleLot
              ├── MaxWeightKg / MaxVolumeM3 capacity ceilings
              └── Lot + Serial inventory snapshots live here
```

Alongside the hierarchy, ship two routing tables:

- **`ItemGroup`** — coarse classification of an item (RAW / WIP / FG / CONSUMABLE / SERVICE / ASSET / SUBASSY / SUBCONTRACT / TOOLING / SPAREPART / PACKAGING / OTHER). Carries default GL accounts + behavioral flags (`ExpenseOnIssue`, `CapitalizesAsAsset`, `RequiresSerialTracking`, `RequiresLotTracking`, `RequiresFai`).
- **`PostingProfile`** — the matrix that resolves `(CompanyId, ItemGroup, InventoryTransactionType, Warehouse)` → `(DebitGl, CreditGl, OffsetGl)`. Look-up cascade goes most-specific (with WarehouseId) → ItemGroup-default → Warehouse-default → hard error.

`LotMaster` and `SerialMaster` round out the traceability backbone: each owns its own lifecycle status, dates, supplier references, and (for serials) an optional `AssetId` bridge into the EAM hierarchy when the unit lands under a maintenance program.

EAM `Location` stays **untouched** as the asset-hierarchy entity. No fields renamed, no columns added, no foreign keys flipped. Maintenance, PM scheduling, work-order cost centers, asset attachments, the entire fixed-asset side keeps pointing at `Location` exactly as it does today.

## Considered alternatives

### Option A — NetSuite single-`Location` pattern *(REJECTED)*

NetSuite folds premises + asset hierarchy + inventory accounting onto one `Location` table with type flags. A single record can be a building, a stockroom, or a bin depending on `locationType`. The hierarchy is one tree expressed by `parent`.

Pros: Schema is simple. Joins are shallow. Migration from a flat ERP is cheap.

Cons (the reasons it loses):
- **GL posting flexibility is poor.** One column for "default inventory account" can't model "this warehouse posts consignment receipts to a memo account, regular receipts to inventory, and quarantine receipts to a hold account."
- **No clean separation between maintenance and inventory teams.** A change to the bin layout edits the same row a PM template depends on. Future role-based UI gets to gate by `locationType` rather than by table.
- **Reporting cuts collide.** "Inventory by warehouse" and "asset count by location" share a column. Aggregations have to filter by type everywhere.
- **Dean's directive on this call was explicit:** *"Option D it is, I like that you are putting extra thought into these things."* The NetSuite collapse is exactly the "going backwards" he called out in `user_dean.md`.

### Option B — Oracle Fusion `OrganizationUnit` + `Subinventory` *(REJECTED)*

Oracle uses `Inventory Organization` (warehouse-ish) → `Subinventory` (logical stock category, e.g. "GOOD" / "QUARANTINE" / "DAMAGE") → `Locator` (physical bin). The subinventory layer captures inventory-accounting state separately from the physical bin.

Pros: Excellent for sub-org reporting cuts. Subinventory carries its own asset/non-asset flag.

Cons:
- The three-layer split (org → subinv → locator) is one layer too many for IndustryOS scope. Most tenants don't need a logical inventory-status layer in the schema; we capture that via `BinMaster.MixingRule` + `LotStatus`.
- Subinventory's semantic burden duplicates flags we're already putting on `WarehouseMaster.IsConsignment` / `IsQuarantine`.
- Adds a JOIN to every inventory query. Drag on the receiving + production hot paths the demos exercise.

### Option C — Manhattan SCALE / Blue Yonder pure-WMS shape *(REJECTED)*

Modern WMS systems use `Facility` → `Zone` → `Aisle` → `Bay` → `Level` → `Position` as actual entities, each a row with its own attributes. The bin is the leaf of a 6-deep tree.

Pros: Industry-leading for high-volume DCs that care about slotting heatmaps + travel-time optimization.

Cons:
- Wildly overweight for IndustryOS's manufacturing-leaning multi-vertical target. ABS Machining has ~50 bins total; modeling 6 entity levels for that is theater.
- The Zone/Aisle/Bay/Level/Position attributes are well-handled as columns on `BinMaster` (which we did). Tenants that want hierarchy reporting can group on those columns.
- Heatmap + slotting is on the IndustryOS roadmap but it can pull from `BinMaster` columns + movement events; it doesn't need a tree of entity tables to function.

### Option D — SAP S/4HANA + Dynamics 365 separation *(ACCEPTED)*

Both SAP S/4HANA EWM (Extended Warehouse Management) and Microsoft Dynamics 365 F&O use a clean two-axis split:

- **Asset / premises axis** — Plant → Storage Location (SAP) or Site → Warehouse (Dynamics). Used for cost-center attachment, plant maintenance, premises addressing.
- **Inventory axis** — Warehouse + Storage Bin (SAP) or Warehouse + Location (Dynamics). Used for inventory accounting, mixing rules, put-away strategy, GL posting.

Both systems ship a posting-profile matrix (SAP "OBYC" + "Account Determination Rules"; Dynamics "Inventory posting profile") that maps item group × transaction type → GL account, with warehouse-level overrides.

Pros (the reasons it wins):
- **Clean separation of concerns.** Maintenance team owns `Site` + `Location`. Inventory team owns `WarehouseMaster` + `BinMaster` + `PostingProfile`. Roles don't collide on the same rows.
- **Posting flexibility scales.** Every dimension that ever needs a different GL account (item group, transaction type, warehouse, plus future segments) gets its own column in `PostingProfile`. New requirements add rows, not migrations.
- **Lifts directly from PRA-5a.** The 26 GL category values that landed (RawMaterialInventory / WipInventoryProduction / FinishedGoodsInventory / SubAssemblyInventory / SubcontractInventory / ConsignedInventory / InventoryInTransit / ScrapInventory + the variance accounts) have natural homes in `PostingProfile` rows.
- **Lifts directly from PR #5d.** The Operator Workbench already emits inventory-affecting events (LaborEntry → WIP, ReasonCode → Scrap/Rework). PostingProfile gives those events a deterministic GL routing without hard-coded conditionals.
- **Familiar to incoming customers.** ABS Thursday + EVS Wednesday demos both run accounting teams who recognize the SAP / Dynamics shape.

Cons (acknowledged, mitigated):
- Schema is slightly larger than Option A. Mitigated by the fact that `Site` + `Location` stay untouched — we add new tables rather than refactoring an existing one. Six new tables (`WarehouseMaster`, `BinMaster`, `LotMaster`, `SerialMaster`, `ItemGroup`, `PostingProfile`) plus three idempotent seed populations.
- Tenant onboarding has to wire warehouse defaults + posting profiles per company. Mitigated by shipping system-template skeleton rows (`DC-DEFAULT` / `PROD-DEFAULT` / `3PL-DEFAULT` warehouses + six `ItemGroup` shells + ~30 system-template `PostingProfile` rows) that tenant onboarding clones with one service call.

## Consequences

### Positive

- Inventory event stream gets deterministic GL routing without conditional code in services.
- New warehouse types (consignment, bonded, quarantine) are configuration changes, not schema migrations.
- `LotMaster` + `SerialMaster` provide the traceability spine that PR #5f (LotGenealogy + SerialGenealogy in the deferred MES cascade) will hang off.
- EAM Location stays out of inventory's way. No future contributor will "simplify" the schema by collapsing them — this ADR is the prophylactic against that.
- ABS aerospace FAI workflow (already in production via PR #1.75) keeps the same Location/Asset shape it relies on; the new Warehouse/Bin/Serial side just adds the inventory traceability that AS9102 requires.
- PRA-8 (Employee + WageGroup + Dept→GL) and PRA-9 (PriceList) can target `PostingProfile` directly for labor and revenue postings — same pattern.

### Negative / risks

- Six new tables means six new BIC-checklist gates per ship — extra rigor required (acknowledged + planned).
- Tenant onboarding service needs an additional step (`SeedTenantPostingProfilesAsync`) — not in PRA-7 scope; deferred to a follow-up cleanup PR after PRA-8 lands the labor-side profiles.
- `Bin` / `Lot` / `Serial` strings on existing inventory snapshot tables stay for back-compat (DEF-008 pattern). Service layer reads `BinId` / `LotId` / `SerialId` FK first; falls back to the string. Cleanup PR sweeps the strings out after all read paths migrate.

### Neutral

- The ADR-003 central GL-account-resolver service stays in place as the fallback when PostingProfile resolution returns no match. Both paths coexist; PostingProfile is the primary route for inventory events from PRA-7 onward.

## Validation

Definition-of-done for PRA-7:

1. Six entities (`WarehouseMaster` / `BinMaster` / `LotMaster` / `SerialMaster` / `ItemGroup` / `PostingProfile`) land in `Models/Masters/` following the PRA-6 file shape.
2. Each passes the 6-point BIC entity checklist (`reference_bic_entity_checklist.md`).
3. EF migration is idempotent (`CREATE TABLE IF NOT EXISTS` + `INSERT … ON CONFLICT DO NOTHING`).
4. Seed populates:
   - 3 system-template `WarehouseMaster` rows (`DC-DEFAULT` / `PROD-DEFAULT` / `3PL-DEFAULT`).
   - 6 system-template `ItemGroup` rows (`RAW` / `WIP` / `FG` / `CONSUMABLE` / `SERVICE` / `ASSET`).
   - Skeleton `PostingProfile` rows linking each `ItemGroup` × top-3 `InventoryTransactionType` combo to the PRA-5a GL category enum values.
   - Zero rows of `BinMaster` / `LotMaster` / `SerialMaster` (operational data — tenant-owned, not seeded).
5. `dotnet build` clean, CHERRY025 analyzer green.
6. EF migration pre-applied to prod DB via `psql` before Replit Republish (`feedback_replit_autodiff_destructive_on_populated_tables.md`).
7. `https://industryos.app/Production` returns 302 → `/Account/Login` with the v3.0 footer (EF `MigrateAsync` clean against prod).
8. Memory file `project_pra7_shipped.md` records merge SHA + verification trail.

## Future work (out of scope for PRA-7)

- **`SeedTenantPostingProfilesAsync`** — clone the system-template `PostingProfile` rows to a freshly-onboarded tenant. Deferred to the post-PRA-8 cleanup PR.
- **`WarehouseMaster.SiteId` enforcement** — make it `NOT NULL` for tenant rows (currently nullable to accommodate system templates). Done via a follow-up CHECK constraint after the first migration sees production traffic.
- **Bin → Lot → Serial inventory snapshot table** — the actual on-hand quantities per (warehouse, bin, lot, serial) tuple. Currently live on the legacy `ItemInventory` table; new snapshot table lands as part of the MES event cascade resumption (PR #5e+).
- **Bin capacity enforcement** — `MaxWeightKg` + `MaxVolumeM3` are stored but not enforced. Enforcement service lands when WMS put-away suggestion endpoint goes in (post-PRA-11).
- **`ItemGroup` ↔ `Item` wiring** — `Item.ItemGroupId` FK addition is a follow-up cleanup PR. PRA-7 ships the master + posting matrix; Item-side wiring lands once `ItemMaster` revision goes through DEF-008 (read group from FK if set, else fall back to current category enum).

## Appendix — column-level traceability to the COA expansion (PRA-5a)

Default seeded `PostingProfile` rows route the following `InventoryTransactionType` → `GlAccountCategory` value (all categories landed in PRA-5a):

| ItemGroup | TransactionType            | Default Dr Category         | Default Cr Category           |
|-----------|----------------------------|------------------------------|--------------------------------|
| RAW       | Receipt                    | `RawMaterialInventory`       | (GRNI clearing — system)       |
| RAW       | IssueToProduction          | `WipInventoryProduction`     | `RawMaterialInventory`         |
| WIP       | ProductionComplete         | `FinishedGoodsInventory`     | `WipToFgClearing`              |
| FG        | Sale                       | `CostOfGoodsSold` (existing) | `FinishedGoodsInventory`       |
| FG        | CustomerReturn             | `FinishedGoodsInventory`     | `CostOfGoodsSold`              |
| CONSUM    | IssueToExpense             | `Consumables`                | (Inventory — group default)    |
| ASSET     | CapitalizeToAsset          | (Asset GL — group default)   | (Inventory — group default)    |
| SUBASSY   | ProductionComplete         | `SubAssemblyInventory`       | `WipInventoryProduction`       |
| SUBCONTR  | SubcontractReceipt         | `SubcontractInventory`       | (Subcontract AP — system)      |
| —         | Scrap                      | `ScrapExpense`               | (Inventory — group default)    |
| —         | Rework                     | `Rework`                     | (Inventory — group default)    |

Migration body wires these via subselects against `GlAccountCategory` enum values seeded by the 28 system template GL accounts from PRA-5a.

---

*This ADR is the architectural prophylactic against future "let's simplify by collapsing Warehouse into Location" refactors. The collapse loses GL flexibility, role separation, and the SAP/Dynamics customer-familiarity benefit. Don't.*
