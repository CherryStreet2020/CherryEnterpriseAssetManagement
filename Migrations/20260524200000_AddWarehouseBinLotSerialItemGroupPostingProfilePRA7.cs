// =============================================================================
// Sprint 13.5 PRA-7 — Warehouse + Bin + Lot + Serial + ItemGroup +
// PostingProfile (Master Files Baseline cascade ship #5 of 8).
//
// SAP S/4 + Dynamics 365 separation-of-concerns shape — see
// docs/ADR-019-wms-posting-profile-pattern.md.
//
// AUTHORITY:
//   - docs/ADR-019-wms-posting-profile-pattern.md (THIS PR ships it)
//   - docs/research/master-files-baseline-2026-05-24.md §6.4 + §6.5
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md
//   - memory: feedback_no_shortcuts_multi_tenant_lineage.md
//   - memory: feedback_replit_autodiff_destructive_on_populated_tables.md
//
// IDEMPOTENT — CREATE TABLE IF NOT EXISTS + INSERT ON CONFLICT DO NOTHING.
// All seed rows are CompanyId IS NULL (system templates).
//
// NO HARDCODED TENANT DATA.
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524200000_AddWarehouseBinLotSerialItemGroupPostingProfilePRA7")]
    public partial class AddWarehouseBinLotSerialItemGroupPostingProfilePRA7 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) WarehouseMasters
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WarehouseMasters"" (
                    ""Id""                              serial          PRIMARY KEY,
                    ""CompanyId""                       integer         NULL,
                    ""SiteId""                          integer         NULL,
                    ""Code""                            varchar(32)     NOT NULL,
                    ""Name""                            varchar(200)    NOT NULL,
                    ""Description""                     varchar(500)    NULL,
                    ""WarehouseType""                   integer         NOT NULL DEFAULT 0,
                    ""DefaultInventoryGlAccountId""     integer         NULL,
                    ""DefaultCogsGlAccountId""          integer         NULL,
                    ""DefaultScrapGlAccountId""         integer         NULL,
                    ""DefaultVarianceGlAccountId""      integer         NULL,
                    ""DefaultCurrencyId""               integer         NULL,
                    ""IsConsignment""                   boolean         NOT NULL DEFAULT FALSE,
                    ""IsBonded""                        boolean         NOT NULL DEFAULT FALSE,
                    ""IsTaxOnReceipt""                  boolean         NOT NULL DEFAULT FALSE,
                    ""IsQuarantine""                    boolean         NOT NULL DEFAULT FALSE,
                    ""IsActive""                        boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""                        boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""                       integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""                       timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                       varchar(100)    NULL,
                    ""ModifiedAt""                      timestamptz     NULL,
                    ""ModifiedBy""                      varchar(100)    NULL,
                    CONSTRAINT ck_warehouse_masters_type CHECK (""WarehouseType"" BETWEEN 0 AND 99)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_warehouse_masters_system_code
                    ON ""WarehouseMasters"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_warehouse_masters_company_code
                    ON ""WarehouseMasters"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_warehouse_masters_site
                    ON ""WarehouseMasters"" (""SiteId"") WHERE ""SiteId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_warehouse_masters_type
                    ON ""WarehouseMasters"" (""WarehouseType"");
            ");

            // ================================================================
            // 2) BinMasters
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BinMasters"" (
                    ""Id""              serial          PRIMARY KEY,
                    ""CompanyId""       integer         NULL,
                    ""WarehouseId""     integer         NOT NULL REFERENCES ""WarehouseMasters""(""Id"") ON DELETE RESTRICT,
                    ""Code""            varchar(64)     NOT NULL,
                    ""Name""            varchar(200)    NULL,
                    ""Description""     varchar(500)    NULL,
                    ""Zone""            varchar(32)     NULL,
                    ""Aisle""           varchar(32)     NULL,
                    ""Bay""             varchar(32)     NULL,
                    ""Level""           varchar(32)     NULL,
                    ""Position""        varchar(32)     NULL,
                    ""BinType""         integer         NOT NULL DEFAULT 0,
                    ""MixingRule""      integer         NOT NULL DEFAULT 0,
                    ""MaxWeightKg""     numeric(14,4)   NULL,
                    ""MaxVolumeM3""     numeric(14,6)   NULL,
                    ""IsTransient""     boolean         NOT NULL DEFAULT FALSE,
                    ""IsPickFace""      boolean         NOT NULL DEFAULT FALSE,
                    ""IsActive""        boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""        boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""       integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""       timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""       varchar(100)    NULL,
                    ""ModifiedAt""      timestamptz     NULL,
                    ""ModifiedBy""      varchar(100)    NULL,
                    CONSTRAINT ck_bin_masters_type CHECK (""BinType"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_bin_masters_mixing CHECK (""MixingRule"" BETWEEN 0 AND 4)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_bin_masters_system_warehouse_code
                    ON ""BinMasters"" (""WarehouseId"", ""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_bin_masters_company_warehouse_code
                    ON ""BinMasters"" (""CompanyId"", ""WarehouseId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_bin_masters_warehouse
                    ON ""BinMasters"" (""WarehouseId"");
            ");

            // ================================================================
            // 3) ItemGroups
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ItemGroups"" (
                    ""Id""                              serial          PRIMARY KEY,
                    ""CompanyId""                       integer         NULL,
                    ""Code""                            varchar(32)     NOT NULL,
                    ""Name""                            varchar(200)    NOT NULL,
                    ""Description""                     varchar(500)    NULL,
                    ""GroupType""                       integer         NOT NULL DEFAULT 0,
                    ""DefaultInventoryGlAccountId""     integer         NULL,
                    ""DefaultCogsGlAccountId""          integer         NULL,
                    ""DefaultRevenueGlAccountId""       integer         NULL,
                    ""DefaultScrapGlAccountId""         integer         NULL,
                    ""DefaultVarianceGlAccountId""      integer         NULL,
                    ""DefaultFixedAssetGlAccountId""    integer         NULL,
                    ""ExpenseOnIssue""                  boolean         NOT NULL DEFAULT FALSE,
                    ""CapitalizesAsAsset""              boolean         NOT NULL DEFAULT FALSE,
                    ""RequiresSerialTracking""          boolean         NOT NULL DEFAULT FALSE,
                    ""RequiresLotTracking""             boolean         NOT NULL DEFAULT FALSE,
                    ""RequiresFai""                     boolean         NOT NULL DEFAULT FALSE,
                    ""IsActive""                        boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""                        boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""                       integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""                       timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                       varchar(100)    NULL,
                    ""ModifiedAt""                      timestamptz     NULL,
                    ""ModifiedBy""                      varchar(100)    NULL,
                    CONSTRAINT ck_item_groups_type CHECK (""GroupType"" BETWEEN 0 AND 99)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_item_groups_system_code
                    ON ""ItemGroups"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_item_groups_company_code
                    ON ""ItemGroups"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_item_groups_type
                    ON ""ItemGroups"" (""GroupType"");
            ");

            // ================================================================
            // 4) PostingProfiles
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PostingProfiles"" (
                    ""Id""                  serial          PRIMARY KEY,
                    ""CompanyId""           integer         NULL,
                    ""ItemGroupId""         integer         NOT NULL REFERENCES ""ItemGroups""(""Id"") ON DELETE RESTRICT,
                    ""TransactionType""     integer         NOT NULL DEFAULT 0,
                    ""WarehouseId""         integer         NULL REFERENCES ""WarehouseMasters""(""Id"") ON DELETE RESTRICT,
                    ""DebitGlAccountId""    integer         NULL,
                    ""CreditGlAccountId""   integer         NULL,
                    ""OffsetGlAccountId""   integer         NULL,
                    ""Priority""            integer         NOT NULL DEFAULT 100,
                    ""Notes""               varchar(500)    NULL,
                    ""IsActive""            boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""            boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""           integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""           timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""           varchar(100)    NULL,
                    ""ModifiedAt""          timestamptz     NULL,
                    ""ModifiedBy""          varchar(100)    NULL,
                    CONSTRAINT ck_posting_profiles_tx CHECK (""TransactionType"" BETWEEN 0 AND 99)
                );

                -- Four partial UNIQUE indexes covering each (CompanyId NULL?, WarehouseId NULL?) combo.
                CREATE UNIQUE INDEX IF NOT EXISTS ix_posting_profiles_system_full
                    ON ""PostingProfiles"" (""ItemGroupId"", ""TransactionType"", ""WarehouseId"")
                    WHERE ""CompanyId"" IS NULL AND ""WarehouseId"" IS NOT NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_posting_profiles_system_nowh
                    ON ""PostingProfiles"" (""ItemGroupId"", ""TransactionType"")
                    WHERE ""CompanyId"" IS NULL AND ""WarehouseId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_posting_profiles_company_full
                    ON ""PostingProfiles"" (""CompanyId"", ""ItemGroupId"", ""TransactionType"", ""WarehouseId"")
                    WHERE ""CompanyId"" IS NOT NULL AND ""WarehouseId"" IS NOT NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_posting_profiles_company_nowh
                    ON ""PostingProfiles"" (""CompanyId"", ""ItemGroupId"", ""TransactionType"")
                    WHERE ""CompanyId"" IS NOT NULL AND ""WarehouseId"" IS NULL;

                CREATE INDEX IF NOT EXISTS ix_posting_profiles_warehouse
                    ON ""PostingProfiles"" (""WarehouseId"") WHERE ""WarehouseId"" IS NOT NULL;
            ");

            // ================================================================
            // 5) LotMasters
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""LotMasters"" (
                    ""Id""                          serial          PRIMARY KEY,
                    ""CompanyId""                   integer         NOT NULL,
                    ""ItemId""                      integer         NOT NULL,
                    ""LotNumber""                   varchar(64)     NOT NULL,
                    ""SupplierLotNumber""           varchar(64)     NULL,
                    ""VendorId""                    integer         NULL,
                    ""ManufactureDate""             timestamptz     NULL,
                    ""ReceiptDate""                 timestamptz     NULL,
                    ""ExpiryDate""                  timestamptz     NULL,
                    ""BestByDate""                  timestamptz     NULL,
                    ""ShelfLifeWarningDays""        integer         NULL,
                    ""CoaFileRef""                  varchar(500)    NULL,
                    ""Notes""                       varchar(2000)   NULL,
                    ""ParentLotId""                 integer         NULL REFERENCES ""LotMasters""(""Id"") ON DELETE RESTRICT,
                    ""Status""                      integer         NOT NULL DEFAULT 0,
                    ""OriginalQuantity""            numeric(18,4)   NULL,
                    ""UomId""                       integer         NULL,
                    ""IsActive""                    boolean         NOT NULL DEFAULT TRUE,
                    ""CreatedAt""                   timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                   varchar(100)    NULL,
                    ""ModifiedAt""                  timestamptz     NULL,
                    ""ModifiedBy""                  varchar(100)    NULL,
                    CONSTRAINT ck_lot_masters_status CHECK (""Status"" BETWEEN 0 AND 6)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_lot_masters_company_item_lot
                    ON ""LotMasters"" (""CompanyId"", ""ItemId"", ""LotNumber"");
                CREATE INDEX IF NOT EXISTS ix_lot_masters_company_item
                    ON ""LotMasters"" (""CompanyId"", ""ItemId"");
                CREATE INDEX IF NOT EXISTS ix_lot_masters_expiry
                    ON ""LotMasters"" (""ExpiryDate"") WHERE ""ExpiryDate"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_lot_masters_status
                    ON ""LotMasters"" (""Status"");
                CREATE INDEX IF NOT EXISTS ix_lot_masters_parent
                    ON ""LotMasters"" (""ParentLotId"") WHERE ""ParentLotId"" IS NOT NULL;
            ");

            // ================================================================
            // 6) SerialMasters
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""SerialMasters"" (
                    ""Id""                          serial          PRIMARY KEY,
                    ""CompanyId""                   integer         NOT NULL,
                    ""ItemId""                      integer         NOT NULL,
                    ""SerialNumber""                varchar(64)     NOT NULL,
                    ""LotId""                       integer         NULL REFERENCES ""LotMasters""(""Id"") ON DELETE SET NULL,
                    ""CurrentWarehouseId""          integer         NULL REFERENCES ""WarehouseMasters""(""Id"") ON DELETE SET NULL,
                    ""CurrentBinId""                integer         NULL REFERENCES ""BinMasters""(""Id"") ON DELETE SET NULL,
                    ""LifecycleStatus""             integer         NOT NULL DEFAULT 0,
                    ""IsOutOfInventory""            boolean         NOT NULL DEFAULT FALSE,
                    ""AssetId""                     integer         NULL REFERENCES ""Assets""(""Id"") ON DELETE SET NULL,
                    ""ManufactureDate""             timestamptz     NULL,
                    ""ReceiptDate""                 timestamptz     NULL,
                    ""ShipDate""                    timestamptz     NULL,
                    ""WarrantyStartDate""           timestamptz     NULL,
                    ""WarrantyEndDate""             timestamptz     NULL,
                    ""OriginVendorId""              integer         NULL,
                    ""CurrentCustomerId""           integer         NULL,
                    ""OriginReceiptId""             integer         NULL,
                    ""OriginProductionOrderId""     integer         NULL,
                    ""Notes""                       varchar(2000)   NULL,
                    ""IsActive""                    boolean         NOT NULL DEFAULT TRUE,
                    ""CreatedAt""                   timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                   varchar(100)    NULL,
                    ""ModifiedAt""                  timestamptz     NULL,
                    ""ModifiedBy""                  varchar(100)    NULL,
                    CONSTRAINT ck_serial_masters_lifecycle CHECK (""LifecycleStatus"" BETWEEN 0 AND 10)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_serial_masters_company_item_serial
                    ON ""SerialMasters"" (""CompanyId"", ""ItemId"", ""SerialNumber"");
                CREATE INDEX IF NOT EXISTS ix_serial_masters_company_item
                    ON ""SerialMasters"" (""CompanyId"", ""ItemId"");
                CREATE INDEX IF NOT EXISTS ix_serial_masters_lot
                    ON ""SerialMasters"" (""LotId"") WHERE ""LotId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_serial_masters_current_warehouse
                    ON ""SerialMasters"" (""CurrentWarehouseId"") WHERE ""CurrentWarehouseId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_serial_masters_current_bin
                    ON ""SerialMasters"" (""CurrentBinId"") WHERE ""CurrentBinId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_serial_masters_lifecycle
                    ON ""SerialMasters"" (""LifecycleStatus"");
                CREATE INDEX IF NOT EXISTS ix_serial_masters_asset
                    ON ""SerialMasters"" (""AssetId"") WHERE ""AssetId"" IS NOT NULL;
            ");

            // ================================================================
            // 7) Seed system-template WarehouseMasters (3 rows)
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""WarehouseMasters"" (""CompanyId"", ""Code"", ""Name"", ""Description"", ""WarehouseType"", ""IsConsignment"", ""IsBonded"", ""IsTaxOnReceipt"", ""IsQuarantine"", ""IsSystem"", ""SortOrder"")
                VALUES
                    (NULL, 'DC-DEFAULT',    'Default Distribution Center', 'System template — distribution / fulfillment node',           0, FALSE, FALSE, FALSE, FALSE, TRUE,  10),
                    (NULL, 'PROD-DEFAULT',  'Default Production Plant',    'System template — manufacturing plant (RM/WIP/FG)',           1, FALSE, FALSE, FALSE, FALSE, TRUE,  20),
                    (NULL, '3PL-DEFAULT',   'Default 3PL Warehouse',       'System template — outsourced logistics partner',              2, FALSE, FALSE, FALSE, FALSE, TRUE,  30),
                    (NULL, 'CONSIGN-DEFAULT','Default Consignment Warehouse','System template — goods here but title remains with supplier',3, TRUE,  FALSE, FALSE, FALSE, TRUE,  40),
                    (NULL, 'QUAR-DEFAULT',  'Default Quarantine Warehouse','System template — failed inspection / regulatory hold',       4, FALSE, FALSE, FALSE, TRUE,  TRUE,  50),
                    (NULL, 'RTN-DEFAULT',   'Default Returns Warehouse',   'System template — customer RMA receiving',                    5, FALSE, FALSE, FALSE, FALSE, TRUE,  60),
                    (NULL, 'SCRAP-DEFAULT', 'Default Scrap Warehouse',     'System template — scrap / write-off staging',                 6, FALSE, FALSE, FALSE, FALSE, TRUE,  70),
                    (NULL, 'WIP-DEFAULT',   'Default WIP Staging',         'System template — work-in-process between work centers',      7, FALSE, FALSE, FALSE, FALSE, TRUE,  80),
                    (NULL, 'TRANSIT-DEFAULT','Default In-Transit Pool',    'System template — inventory leaving but not yet received',    8, FALSE, FALSE, FALSE, FALSE, TRUE,  90)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 8) Seed system-template ItemGroups (12 rows)
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""ItemGroups"" (""CompanyId"", ""Code"", ""Name"", ""Description"", ""GroupType"", ""ExpenseOnIssue"", ""CapitalizesAsAsset"", ""RequiresSerialTracking"", ""RequiresLotTracking"", ""RequiresFai"", ""IsSystem"", ""SortOrder"")
                VALUES
                    (NULL, 'RAW',        'Raw Material',        'Inputs to manufacturing — bar stock, sheet, electronics, chemicals',       0, FALSE, FALSE, FALSE, FALSE, FALSE, TRUE,  10),
                    (NULL, 'WIP',        'Work In Process',     'Items in-process between work centers',                                    1, FALSE, FALSE, FALSE, FALSE, FALSE, TRUE,  20),
                    (NULL, 'FG',         'Finished Goods',      'Outputs of manufacturing — sellable units',                                2, FALSE, FALSE, FALSE, FALSE, FALSE, TRUE,  30),
                    (NULL, 'CONSUMABLE', 'Consumable',          'Items expensed on issue (cutting fluid, gloves, lubricant)',               3, TRUE,  FALSE, FALSE, FALSE, FALSE, TRUE,  40),
                    (NULL, 'SERVICE',    'Service',             'Services — labor, freight, third-party — no inventory side',               4, TRUE,  FALSE, FALSE, FALSE, FALSE, TRUE,  50),
                    (NULL, 'ASSET',      'Capitalizable Asset', 'Items that capitalize into the fixed-asset ledger',                        5, FALSE, TRUE,  TRUE,  FALSE, FALSE, TRUE,  60),
                    (NULL, 'SUBASSY',    'Sub-Assembly',        'Internally produced inputs to higher-level FG',                            6, FALSE, FALSE, FALSE, FALSE, FALSE, TRUE,  70),
                    (NULL, 'SUBCONTR',   'Subcontract',         'Items produced by external subcontractor (we own IP + materials)',         7, FALSE, FALSE, FALSE, FALSE, FALSE, TRUE,  80),
                    (NULL, 'TOOLING',    'Tooling',             'Tooling, dies, fixtures — capitalized or expensed per tenant policy',      8, FALSE, FALSE, FALSE, FALSE, FALSE, TRUE,  90),
                    (NULL, 'SPAREPART',  'Spare Part',          'Spare parts for maintenance (not for resale)',                             9, FALSE, FALSE, FALSE, FALSE, FALSE, TRUE, 100),
                    (NULL, 'PACKAGING',  'Packaging',           'Packaging materials — cartons, pallets, dunnage',                         10, FALSE, FALSE, FALSE, FALSE, FALSE, TRUE, 110),
                    (NULL, 'AS9102-FG',  'AS9102 Finished Goods','Aerospace finished goods requiring First Article Inspection',             2, FALSE, FALSE, TRUE,  TRUE,  TRUE,  TRUE, 120)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 9) Seed system-template PostingProfile skeleton rows.
            //
            //    Each row points to an ItemGroup by Code and resolves the
            //    GlAccount via a subselect against the existing GlAccounts
            //    table seeded by PRA-5a (CompanyId IS NULL system template
            //    accounts at the GlAccountCategory enum positions).
            //
            //    Subselects return NULL when no matching system GL exists
            //    (some tenants extend the COA with their own accounts at
            //    onboarding time) — tenant cloning service fills those in
            //    per-tenant in a follow-up PR.
            // ================================================================
            mb.Sql(@"
                WITH ig AS (
                    SELECT ""Id"", ""Code"" FROM ""ItemGroups"" WHERE ""CompanyId"" IS NULL
                ),
                gl AS (
                    SELECT ""Id"", ""Category"" FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""IsSystemAccount"" = TRUE
                )
                INSERT INTO ""PostingProfiles"" (""CompanyId"", ""ItemGroupId"", ""TransactionType"", ""WarehouseId"", ""DebitGlAccountId"", ""CreditGlAccountId"", ""Priority"", ""Notes"", ""IsSystem"", ""SortOrder"")
                VALUES
                    -- RAW: Receipt → Dr RawMaterialInventory (111)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='RAW'),        0,  NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=111), NULL,                                          100, 'System template: raw material receipt',          TRUE,  10),
                    -- RAW: IssueToProduction → Dr WIP (112), Cr RM (111)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='RAW'),        1,  NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=112), (SELECT ""Id"" FROM gl WHERE ""Category""=111), 100, 'System template: raw issue to production',       TRUE,  20),
                    -- WIP: ProductionComplete → Dr FG (113), Cr WipToFgClearing (use WipInventoryProduction 112 as fallback)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='WIP'),        10, NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=113), (SELECT ""Id"" FROM gl WHERE ""Category""=112), 100, 'System template: WIP → FG on production complete',TRUE,  30),
                    -- FG: Sale → Dr COGS-product (legacy 425 ProductSales reverse — defer to onboarding), Cr FG (113)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='FG'),         3,  NULL, NULL,                                            (SELECT ""Id"" FROM gl WHERE ""Category""=113), 100, 'System template: FG sale — Dr COGS wired by tenant onboarding', TRUE,  40),
                    -- FG: CustomerReturn → Dr FG (113), Cr COGS (wired by tenant)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='FG'),         4,  NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=113), NULL,                                          100, 'System template: FG customer return',            TRUE,  50),
                    -- CONSUMABLE: IssueToExpense → Dr Consumables, Cr Inventory (group default)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='CONSUMABLE'), 2,  NULL, NULL,                                            NULL,                                          100, 'System template: consumable issue to expense — accounts wired by tenant', TRUE,  60),
                    -- ASSET: CapitalizeToAsset → Dr FixedAsset (group default), Cr Inventory (group default)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='ASSET'),      16, NULL, NULL,                                            NULL,                                          100, 'System template: asset capitalization — accounts wired by tenant',         TRUE,  70),
                    -- SUBASSY: ProductionComplete → Dr SubAssemblyInventory (114), Cr WIP (112)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='SUBASSY'),    10, NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=114), (SELECT ""Id"" FROM gl WHERE ""Category""=112), 100, 'System template: sub-assembly production complete',TRUE,  80),
                    -- SUBCONTR: SubcontractReceipt → Dr SubcontractInventory (115), Cr (GRNI/AP — wired by tenant)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='SUBCONTR'),   11, NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=115), NULL,                                          100, 'System template: subcontract receipt',           TRUE,  90),
                    -- RAW: Scrap → Dr ScrapInventory (118), Cr RM (111)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='RAW'),        8,  NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=118), (SELECT ""Id"" FROM gl WHERE ""Category""=111), 100, 'System template: raw scrap',                     TRUE, 100),
                    -- WIP: Scrap → Dr ScrapInventory (118), Cr WIP (112)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='WIP'),        8,  NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=118), (SELECT ""Id"" FROM gl WHERE ""Category""=112), 100, 'System template: WIP scrap',                     TRUE, 110),
                    -- FG: Scrap → Dr ScrapInventory (118), Cr FG (113)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='FG'),         8,  NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=118), (SELECT ""Id"" FROM gl WHERE ""Category""=113), 100, 'System template: FG scrap',                      TRUE, 120),
                    -- WIP: Rework → Dr WIP (112) (rework re-enters WIP), no clear default Cr (defer)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='WIP'),        9,  NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=112), NULL,                                          100, 'System template: WIP rework',                    TRUE, 130),
                    -- RAW: ConsignmentReceipt → memo-only, both NULL (status change)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='RAW'),        12, NULL, NULL,                                            NULL,                                          100, 'System template: consignment receipt — memo only (no GL)', TRUE, 140),
                    -- Transfer (any item group): use RAW as the pivot — Dr/Cr both nullable
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='RAW'),        6,  NULL, NULL,                                            NULL,                                          100, 'System template: transfer — same-account both sides',     TRUE, 150)
                ON CONFLICT DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"
                DROP TABLE IF EXISTS ""SerialMasters"";
                DROP TABLE IF EXISTS ""LotMasters"";
                DROP TABLE IF EXISTS ""PostingProfiles"";
                DROP TABLE IF EXISTS ""ItemGroups"";
                DROP TABLE IF EXISTS ""BinMasters"";
                DROP TABLE IF EXISTS ""WarehouseMasters"";
            ");
        }
    }
}
