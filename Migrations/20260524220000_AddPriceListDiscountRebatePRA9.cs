// =============================================================================
// Sprint 13.5 PRA-9 — PriceListMaster + PriceListLine + DiscountSchema +
// RebateAgreement. Master Files Baseline cascade ship #7 of 10.
//
// Ships alongside ADR-027 (docs/ADR-027-sales-order-line-and-release-shape.md)
// which locks the future SalesOrder→Line→Release shape so Sprint 19 can't
// accidentally couple ProductionOrder to a SalesOrder header.
//
// FOUR NEW TABLES:
//   - PriceListMasters    customer-facing price list header (CompanyId nullable)
//   - PriceListLines      per-Item pricing within a list (no direct CompanyId — flows via FK)
//   - DiscountSchemas     promotional / contract / scale discounts (CompanyId nullable)
//   - RebateAgreements    customer back-end rebate contracts (CompanyId NOT NULL)
//
// SEEDS — all CompanyId IS NULL system templates:
//   - 4 PriceListMaster templates (DEFAULT-WHOLESALE / DEFAULT-DISTRIBUTION /
//     DEFAULT-RETAIL / DEFAULT-GOVERNMENT) — each in USD, no customer, tier
//     scope set. Tenants clone into per-tenant lists during onboarding.
//
// IDEMPOTENT — CREATE TABLE IF NOT EXISTS + INSERT ON CONFLICT DO NOTHING.
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md §6.7
//   - docs/ADR-027-sales-order-line-and-release-shape.md (THIS PR ships it)
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524220000_AddPriceListDiscountRebatePRA9")]
    public partial class AddPriceListDiscountRebatePRA9 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) PriceListMasters
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PriceListMasters"" (
                    ""Id""                  serial          PRIMARY KEY,
                    ""CompanyId""           integer         NULL,
                    ""Code""                varchar(64)     NOT NULL,
                    ""Name""                varchar(200)    NOT NULL,
                    ""Description""         varchar(500)    NULL,
                    ""CustomerTier""        integer         NULL,
                    ""CustomerId""          integer         NULL,
                    ""CurrencyId""          integer         NOT NULL REFERENCES ""CurrencyMasters""(""Id"") ON DELETE RESTRICT,
                    ""EffectiveFromUtc""    timestamptz     NOT NULL DEFAULT NOW(),
                    ""EffectiveToUtc""      timestamptz     NULL,
                    ""IsPriceLocked""       boolean         NOT NULL DEFAULT FALSE,
                    ""AllowsDiscounts""     boolean         NOT NULL DEFAULT TRUE,
                    ""IsActive""            boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""            boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""           integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""           timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""           varchar(100)    NULL,
                    ""ModifiedAt""          timestamptz     NULL,
                    ""ModifiedBy""          varchar(100)    NULL,
                    CONSTRAINT ck_price_list_masters_tier CHECK (""CustomerTier"" IS NULL OR ""CustomerTier"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_price_list_masters_dates CHECK (""EffectiveToUtc"" IS NULL OR ""EffectiveToUtc"" > ""EffectiveFromUtc"")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_price_list_masters_system_code
                    ON ""PriceListMasters"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_price_list_masters_company_code
                    ON ""PriceListMasters"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_price_list_masters_customer
                    ON ""PriceListMasters"" (""CustomerId"") WHERE ""CustomerId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_price_list_masters_tier
                    ON ""PriceListMasters"" (""CustomerTier"") WHERE ""CustomerTier"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_price_list_masters_effective_to
                    ON ""PriceListMasters"" (""EffectiveToUtc"") WHERE ""EffectiveToUtc"" IS NULL;
            ");

            // ================================================================
            // 2) PriceListLines
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PriceListLines"" (
                    ""Id""                  serial          PRIMARY KEY,
                    ""PriceListMasterId""   integer         NOT NULL REFERENCES ""PriceListMasters""(""Id"") ON DELETE CASCADE,
                    ""ItemId""              integer         NOT NULL,
                    ""UomId""               integer         NULL,
                    ""UnitPrice""           numeric(18,4)   NOT NULL,
                    ""ListPrice""           numeric(18,4)   NULL,
                    ""VolumeBreaksJson""    jsonb           NULL,
                    ""MinimumQuantity""     numeric(18,4)   NULL,
                    ""MaximumQuantity""     numeric(18,4)   NULL,
                    ""EffectiveFromUtc""    timestamptz     NOT NULL DEFAULT NOW(),
                    ""EffectiveToUtc""      timestamptz     NULL,
                    ""PriceLockUntilUtc""   timestamptz     NULL,
                    ""DiscountAllowed""     boolean         NOT NULL DEFAULT TRUE,
                    ""IsActive""            boolean         NOT NULL DEFAULT TRUE,
                    ""Notes""               varchar(500)    NULL,
                    ""CreatedAt""           timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""           varchar(100)    NULL,
                    ""ModifiedAt""          timestamptz     NULL,
                    ""ModifiedBy""          varchar(100)    NULL,
                    CONSTRAINT ck_price_list_lines_dates CHECK (""EffectiveToUtc"" IS NULL OR ""EffectiveToUtc"" > ""EffectiveFromUtc""),
                    CONSTRAINT ck_price_list_lines_qty CHECK (""MaximumQuantity"" IS NULL OR ""MinimumQuantity"" IS NULL OR ""MaximumQuantity"" >= ""MinimumQuantity"")
                );

                CREATE INDEX IF NOT EXISTS ix_price_list_lines_list_item_uom_effective
                    ON ""PriceListLines"" (""PriceListMasterId"", ""ItemId"", ""UomId"", ""EffectiveFromUtc"");
                CREATE INDEX IF NOT EXISTS ix_price_list_lines_item
                    ON ""PriceListLines"" (""ItemId"");
                CREATE INDEX IF NOT EXISTS ix_price_list_lines_effective_to
                    ON ""PriceListLines"" (""EffectiveToUtc"") WHERE ""EffectiveToUtc"" IS NULL;
            ");

            // ================================================================
            // 3) DiscountSchemas
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""DiscountSchemas"" (
                    ""Id""                          serial          PRIMARY KEY,
                    ""CompanyId""                   integer         NULL,
                    ""Code""                        varchar(64)     NOT NULL,
                    ""Name""                        varchar(200)    NOT NULL,
                    ""Description""                 varchar(500)    NULL,
                    ""DiscountType""                integer         NOT NULL DEFAULT 0,
                    ""DiscountValue""               numeric(18,6)   NULL,
                    ""CurrencyId""                  integer         NULL,
                    ""TiersJson""                   jsonb           NULL,
                    ""AppliesToScope""              integer         NOT NULL DEFAULT 0,
                    ""AppliesToEntityId""           integer         NULL,
                    ""AppliesToCustomerTier""       integer         NULL,
                    ""StackingRule""                integer         NOT NULL DEFAULT 2,
                    ""Priority""                    integer         NOT NULL DEFAULT 100,
                    ""MinPurchaseAmount""           numeric(18,4)   NULL,
                    ""MinQuantity""                 numeric(18,4)   NULL,
                    ""MaxApplicationsPerCustomer""  integer         NULL,
                    ""EffectiveFromUtc""            timestamptz     NOT NULL DEFAULT NOW(),
                    ""EffectiveToUtc""              timestamptz     NULL,
                    ""IsActive""                    boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""                    boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""                   integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""                   timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                   varchar(100)    NULL,
                    ""ModifiedAt""                  timestamptz     NULL,
                    ""ModifiedBy""                  varchar(100)    NULL,
                    CONSTRAINT ck_discount_schemas_type CHECK (""DiscountType"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_discount_schemas_scope CHECK (""AppliesToScope"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_discount_schemas_stacking CHECK (""StackingRule"" BETWEEN 0 AND 2),
                    CONSTRAINT ck_discount_schemas_dates CHECK (""EffectiveToUtc"" IS NULL OR ""EffectiveToUtc"" > ""EffectiveFromUtc"")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_discount_schemas_system_code
                    ON ""DiscountSchemas"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_discount_schemas_company_code
                    ON ""DiscountSchemas"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_discount_schemas_scope
                    ON ""DiscountSchemas"" (""AppliesToScope"");
                CREATE INDEX IF NOT EXISTS ix_discount_schemas_scope_entity
                    ON ""DiscountSchemas"" (""AppliesToScope"", ""AppliesToEntityId"") WHERE ""AppliesToEntityId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_discount_schemas_effective_to
                    ON ""DiscountSchemas"" (""EffectiveToUtc"") WHERE ""EffectiveToUtc"" IS NULL;
            ");

            // ================================================================
            // 4) RebateAgreements
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""RebateAgreements"" (
                    ""Id""                              serial          PRIMARY KEY,
                    ""CompanyId""                       integer         NOT NULL,
                    ""Code""                            varchar(64)     NOT NULL,
                    ""Name""                            varchar(200)    NOT NULL,
                    ""Description""                     varchar(2000)   NULL,
                    ""CustomerId""                      integer         NOT NULL,
                    ""Basis""                           integer         NOT NULL DEFAULT 0,
                    ""Period""                          integer         NOT NULL DEFAULT 3,
                    ""CustomPeriodStartUtc""            timestamptz     NULL,
                    ""CustomPeriodEndUtc""              timestamptz     NULL,
                    ""TiersJson""                       jsonb           NOT NULL DEFAULT '[]',
                    ""PayoutMethod""                    integer         NOT NULL DEFAULT 0,
                    ""AccrualGlAccountId""              integer         NULL REFERENCES ""GlAccounts""(""Id"") ON DELETE SET NULL,
                    ""PayoutGlAccountId""               integer         NULL REFERENCES ""GlAccounts""(""Id"") ON DELETE SET NULL,
                    ""CurrencyId""                      integer         NULL REFERENCES ""CurrencyMasters""(""Id"") ON DELETE SET NULL,
                    ""RestrictedToItemGroupId""         integer         NULL REFERENCES ""ItemGroups""(""Id"") ON DELETE SET NULL,
                    ""RestrictedToPriceListMasterId""   integer         NULL REFERENCES ""PriceListMasters""(""Id"") ON DELETE SET NULL,
                    ""EffectiveFromUtc""                timestamptz     NOT NULL DEFAULT NOW(),
                    ""EffectiveToUtc""                  timestamptz     NULL,
                    ""Status""                          integer         NOT NULL DEFAULT 0,
                    ""IsActive""                        boolean         NOT NULL DEFAULT TRUE,
                    ""Notes""                           varchar(2000)   NULL,
                    ""CreatedAt""                       timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                       varchar(100)    NULL,
                    ""ModifiedAt""                      timestamptz     NULL,
                    ""ModifiedBy""                      varchar(100)    NULL,
                    CONSTRAINT ck_rebate_agreements_basis CHECK (""Basis"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_rebate_agreements_period CHECK (""Period"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_rebate_agreements_payout CHECK (""PayoutMethod"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_rebate_agreements_status CHECK (""Status"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_rebate_agreements_dates CHECK (""EffectiveToUtc"" IS NULL OR ""EffectiveToUtc"" > ""EffectiveFromUtc"")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_rebate_agreements_company_code
                    ON ""RebateAgreements"" (""CompanyId"", ""Code"");
                CREATE INDEX IF NOT EXISTS ix_rebate_agreements_customer
                    ON ""RebateAgreements"" (""CustomerId"");
                CREATE INDEX IF NOT EXISTS ix_rebate_agreements_status
                    ON ""RebateAgreements"" (""Status"");
                CREATE INDEX IF NOT EXISTS ix_rebate_agreements_effective_to
                    ON ""RebateAgreements"" (""EffectiveToUtc"") WHERE ""EffectiveToUtc"" IS NULL;
            ");

            // ================================================================
            // 5) Seed system-template PriceListMasters — 4 rows, one per
            //    common CustomerTier. All in USD. Tenants clone into per-
            //    tenant lists during onboarding.
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""PriceListMasters"" (""CompanyId"", ""Code"", ""Name"", ""Description"", ""CustomerTier"", ""CurrencyId"", ""AllowsDiscounts"", ""IsSystem"", ""SortOrder"")
                SELECT
                    NULL                                  AS ""CompanyId"",
                    t.code                                AS ""Code"",
                    t.name                                AS ""Name"",
                    t.description                         AS ""Description"",
                    t.tier                                AS ""CustomerTier"",
                    (SELECT ""Id"" FROM ""CurrencyMasters"" WHERE ""IsoCode""='USD' AND ""CompanyId"" IS NULL) AS ""CurrencyId"",
                    t.allows_discounts                    AS ""AllowsDiscounts"",
                    TRUE                                  AS ""IsSystem"",
                    t.sort_order                          AS ""SortOrder""
                FROM (VALUES
                    ('DEFAULT-WHOLESALE',    'Default Wholesale Price List',    'System template — bulk B2B wholesale pricing, discounts allowed',                  0, TRUE,   10),
                    ('DEFAULT-DISTRIBUTION', 'Default Distribution Price List', 'System template — distributor pricing, deeper discounts, contract-locked option', 1, TRUE,   20),
                    ('DEFAULT-RETAIL',       'Default Retail Price List',       'System template — direct retail / end-customer pricing, full margin',              2, TRUE,   30),
                    ('DEFAULT-GOVERNMENT',   'Default Government Price List',   'System template — GSA / federal / municipal pricing, audit-locked',                3, FALSE,  40)
                ) AS t(code, name, description, tier, allows_discounts, sort_order)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""PriceListMasters""
                    WHERE ""CompanyId"" IS NULL AND ""Code"" = t.code
                );
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"
                DROP TABLE IF EXISTS ""RebateAgreements"";
                DROP TABLE IF EXISTS ""DiscountSchemas"";
                DROP TABLE IF EXISTS ""PriceListLines"";
                DROP TABLE IF EXISTS ""PriceListMasters"";
            ");
        }
    }
}
