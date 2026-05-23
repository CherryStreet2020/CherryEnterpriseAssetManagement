using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 13.5 PRA-1 — Master Files: IndustryVertical + Carriers +
    // Customer/Vendor/Manufacturer regulator IDs + Customer defaults.
    //
    // ADDITIVE-ONLY. Zero breakage. Safe to apply against production.
    // Every new column is nullable EXCEPT Company.IndustryVertical which
    // has a safe DEFAULT 0 (Unspecified) so existing rows stay valid
    // without backfill.
    //
    // SQL source-of-truth: .ship/drafts/sprint-13.5-PRA1-master-files.sql
    //
    // What this migration creates:
    //   1. Companies +3 cols (IndustryVertical / CageCode / DunsNumber) + CHECK
    //   2. Customers +12 cols (4 defaults + 2 IDs + CreditLimit + TaxCodeId FK + 5 bill-to) + CHECK + FK
    //   3. Vendors +9 cols (regulator IDs + PaymentTermId FK retrofit) + 4 partial indexes
    //   4. Manufacturers +2 cols (CageCode + DunsNumber) + 1 partial index
    //   5. Carriers NEW table + 12 system-wide seed rows (UPS / FedEx / DHL / USPS / etc.)
    //   6. AdvancedShippingNotices.CarrierId + FK + partial index
    //   7. ShippingMethods.CarrierId + FK + partial index
    //   8. Cockpit-sort indexes on Customer.DefaultQualityProgram / DefaultExportControl
    //   9. Companies.IndustryVertical index for cockpit branching
    //  10. Demo-tenant IndustryVertical seed UPDATE (ABS → Machining, EVS → PrecisionEto)
    //
    // What this migration does NOT do:
    //   - Item regulatory fields (DeaSchedule / MslLevel / Eccn / RoHS) → Sprint 14
    //   - Allergen / Ingredient / SOP / NDC catalogs → V2
    //   - METRC / cannabis-specific masters → V2
    //   - Currency.IsBaseTenantCurrency lookup → PRA-3
    //   - Item.UoMId FK retrofit → out of PRA-1 scope
    //
    // Idempotent: every CREATE/ALTER uses IF NOT EXISTS or DO $$ guards.
    //
    // Cross-refs:
    //   - .ship/drafts/sprint-13.5-PRA1-master-files.sql — source SQL (311 lines)
    //   - docs/research/master-files-audit.md — audit + design rationale
    //   - docs/research/luxury-cockpit-ux.md — UX baseline (vertical-chip Tag Helper sources from these columns)
    //   - Migrations/20260523_AddCustomerProjectFieldExpansion.cs — PR #1.5 style template
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524_AddMasterFilesPRA1")]
    public partial class AddMasterFilesPRA1 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) Companies — IndustryVertical + CageCode + DunsNumber
            // ================================================================
            mb.Sql(@"
                ALTER TABLE ""Companies""
                    ADD COLUMN IF NOT EXISTS ""IndustryVertical"" smallint    NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS ""CageCode""         varchar(10) NULL,
                    ADD COLUMN IF NOT EXISTS ""DunsNumber""       varchar(13) NULL;
            ");

            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_companies_industryvertical_range') THEN
                        ALTER TABLE ""Companies""
                            ADD CONSTRAINT ck_companies_industryvertical_range
                            CHECK (""IndustryVertical"" BETWEEN 0 AND 31);
                    END IF;
                END $$;
            ");

            // ================================================================
            // 2) Customers — 4 defaults + 2 IDs + Credit + TaxCode FK + 5 bill-to
            // ================================================================
            mb.Sql(@"
                ALTER TABLE ""Customers""
                    ADD COLUMN IF NOT EXISTS ""DefaultQualityProgram"" smallint    NULL,
                    ADD COLUMN IF NOT EXISTS ""DefaultExportControl""  smallint    NULL,
                    ADD COLUMN IF NOT EXISTS ""DefaultContractType""   smallint    NULL,
                    ADD COLUMN IF NOT EXISTS ""DefaultRevenueMode""    smallint    NULL,
                    ADD COLUMN IF NOT EXISTS ""CageCode""              varchar(10) NULL,
                    ADD COLUMN IF NOT EXISTS ""DunsNumber""            varchar(13) NULL,
                    ADD COLUMN IF NOT EXISTS ""CreditLimit""           numeric(18,2) NULL,
                    ADD COLUMN IF NOT EXISTS ""TaxCodeId""             int         NULL,
                    ADD COLUMN IF NOT EXISTS ""BillToAddress""         varchar(200) NULL,
                    ADD COLUMN IF NOT EXISTS ""BillToCity""            varchar(100) NULL,
                    ADD COLUMN IF NOT EXISTS ""BillToStateProvince""   varchar(50)  NULL,
                    ADD COLUMN IF NOT EXISTS ""BillToPostalCode""      varchar(20)  NULL,
                    ADD COLUMN IF NOT EXISTS ""BillToCountry""         varchar(50)  NULL;
            ");

            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'fk_customers_taxcodeid_taxcodes') THEN
                        ALTER TABLE ""Customers""
                            ADD CONSTRAINT fk_customers_taxcodeid_taxcodes
                            FOREIGN KEY (""TaxCodeId"") REFERENCES ""TaxCodes"" (""Id"") ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customers_defaultqualityprogram_range') THEN
                        ALTER TABLE ""Customers""
                            ADD CONSTRAINT ck_customers_defaultqualityprogram_range
                            CHECK (""DefaultQualityProgram"" IS NULL OR ""DefaultQualityProgram"" BETWEEN 0 AND 5);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customers_defaultexportcontrol_range') THEN
                        ALTER TABLE ""Customers""
                            ADD CONSTRAINT ck_customers_defaultexportcontrol_range
                            CHECK (""DefaultExportControl"" IS NULL OR ""DefaultExportControl"" BETWEEN 0 AND 3);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customers_defaultcontracttype_range') THEN
                        ALTER TABLE ""Customers""
                            ADD CONSTRAINT ck_customers_defaultcontracttype_range
                            CHECK (""DefaultContractType"" IS NULL OR ""DefaultContractType"" BETWEEN 0 AND 5);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customers_defaultrevenuemode_range') THEN
                        ALTER TABLE ""Customers""
                            ADD CONSTRAINT ck_customers_defaultrevenuemode_range
                            CHECK (""DefaultRevenueMode"" IS NULL OR ""DefaultRevenueMode"" BETWEEN 0 AND 3);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customers_creditlimit_nonneg') THEN
                        ALTER TABLE ""Customers""
                            ADD CONSTRAINT ck_customers_creditlimit_nonneg
                            CHECK (""CreditLimit"" IS NULL OR ""CreditLimit"" >= 0);
                    END IF;
                END $$;
            ");

            // ================================================================
            // 3) Vendors — 9 regulator IDs + PaymentTermId FK retrofit
            // ================================================================
            mb.Sql(@"
                ALTER TABLE ""Vendors""
                    ADD COLUMN IF NOT EXISTS ""CageCode""           varchar(10) NULL,
                    ADD COLUMN IF NOT EXISTS ""DunsNumber""         varchar(13) NULL,
                    ADD COLUMN IF NOT EXISTS ""UeiNumber""          varchar(12) NULL,
                    ADD COLUMN IF NOT EXISTS ""FdaEstablishmentId"" varchar(20) NULL,
                    ADD COLUMN IF NOT EXISTS ""DeaRegistration""    varchar(20) NULL,
                    ADD COLUMN IF NOT EXISTS ""ItarRegistration""   varchar(40) NULL,
                    ADD COLUMN IF NOT EXISTS ""As9100CertRef""      varchar(60) NULL,
                    ADD COLUMN IF NOT EXISTS ""Iso9001CertRef""     varchar(60) NULL,
                    ADD COLUMN IF NOT EXISTS ""Iso13485CertRef""    varchar(60) NULL,
                    ADD COLUMN IF NOT EXISTS ""PaymentTermId""      int         NULL;
            ");

            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'fk_vendors_paymenttermid_paymentterms') THEN
                        ALTER TABLE ""Vendors""
                            ADD CONSTRAINT fk_vendors_paymenttermid_paymentterms
                            FOREIGN KEY (""PaymentTermId"") REFERENCES ""PaymentTerms"" (""Id"") ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_vendors_cagecode ON ""Vendors"" (""CageCode"") WHERE ""CageCode"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_vendors_duns     ON ""Vendors"" (""DunsNumber"") WHERE ""DunsNumber"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_vendors_fda      ON ""Vendors"" (""FdaEstablishmentId"") WHERE ""FdaEstablishmentId"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_vendors_dea      ON ""Vendors"" (""DeaRegistration"") WHERE ""DeaRegistration"" IS NOT NULL;");

            // ================================================================
            // 4) Manufacturers — CageCode + DunsNumber
            // ================================================================
            mb.Sql(@"
                ALTER TABLE ""Manufacturers""
                    ADD COLUMN IF NOT EXISTS ""CageCode""   varchar(10) NULL,
                    ADD COLUMN IF NOT EXISTS ""DunsNumber"" varchar(13) NULL;
            ");

            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_manufacturers_cagecode ON ""Manufacturers"" (""CageCode"") WHERE ""CageCode"" IS NOT NULL;");

            // ================================================================
            // 5) Carriers — first-class master + 12 system-wide seed rows
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Carriers"" (
                    ""Id""                  serial       PRIMARY KEY,
                    ""CompanyId""           int          NULL,
                    ""Code""                varchar(10)  NOT NULL,
                    ""ScacCode""            varchar(4)   NULL,
                    ""Name""                varchar(100) NOT NULL,
                    ""ContactName""         varchar(100) NULL,
                    ""ContactEmail""        varchar(100) NULL,
                    ""ContactPhone""        varchar(30)  NULL,
                    ""WebsiteUrl""          varchar(200) NULL,
                    ""TrackingUrlTemplate"" varchar(300) NULL,
                    ""ApiEndpoint""         varchar(300) NULL,
                    ""ApiAuthRef""          varchar(100) NULL,
                    ""IsActive""            boolean      NOT NULL DEFAULT TRUE,
                    ""SortOrder""           int          NOT NULL DEFAULT 0,
                    ""CreatedAt""           timestamptz  NOT NULL DEFAULT now(),
                    ""ModifiedAt""          timestamptz  NULL,
                    CONSTRAINT fk_carriers_companyid_companies
                        FOREIGN KEY (""CompanyId"") REFERENCES ""Companies"" (""Id"") ON DELETE SET NULL
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS uq_carriers_company_code
                    ON ""Carriers"" (COALESCE(""CompanyId"", 0), ""Code"");
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS uq_carriers_company_scac
                    ON ""Carriers"" (COALESCE(""CompanyId"", 0), ""ScacCode"")
                    WHERE ""ScacCode"" IS NOT NULL;
            ");

            // Seed 12 system-wide carriers (CompanyId NULL).
            mb.Sql(@"
                INSERT INTO ""Carriers""
                    (""CompanyId"", ""Code"", ""ScacCode"", ""Name"", ""WebsiteUrl"", ""TrackingUrlTemplate"", ""IsActive"", ""SortOrder"")
                VALUES
                    (NULL, 'UPS',     'UPSN', 'United Parcel Service',  'https://www.ups.com',
                        'https://www.ups.com/track?tracknum={0}', TRUE, 10),
                    (NULL, 'FEDEX',   'FDEG', 'FedEx Express',           'https://www.fedex.com',
                        'https://www.fedex.com/fedextrack/?trknbr={0}', TRUE, 20),
                    (NULL, 'FEDEXG',  'FXFE', 'FedEx Ground',            'https://www.fedex.com',
                        'https://www.fedex.com/fedextrack/?trknbr={0}', TRUE, 25),
                    (NULL, 'DHL',     'DHLC', 'DHL Express',             'https://www.dhl.com',
                        'https://www.dhl.com/track?awb={0}', TRUE, 30),
                    (NULL, 'USPS',    'USPS', 'United States Postal Service', 'https://www.usps.com',
                        'https://tools.usps.com/go/TrackConfirmAction?tLabels={0}', TRUE, 40),
                    (NULL, 'ONTRAC',  'OTRC', 'OnTrac',                   'https://www.ontrac.com',
                        'https://www.ontrac.com/tracking?number={0}', TRUE, 50),
                    (NULL, 'XPO',     'CNWY', 'XPO Logistics (LTL)',     'https://www.xpo.com',
                        NULL, TRUE, 60),
                    (NULL, 'OD',      'ODFL', 'Old Dominion Freight Line', 'https://www.odfl.com',
                        'https://www.odfl.com/Trace/standardResults.faces?proNumber={0}', TRUE, 70),
                    (NULL, 'YRC',     'RDWY', 'YRC Freight',              'https://yrc.com',
                        NULL, TRUE, 80),
                    (NULL, 'SAIA',    'SAIA', 'Saia LTL Freight',         'https://www.saia.com',
                        NULL, TRUE, 90),
                    (NULL, 'PICKUP',  NULL,   'Customer Pickup',          NULL, NULL, TRUE, 900),
                    (NULL, 'WILLCALL',NULL,   'Will Call',                NULL, NULL, TRUE, 910)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 6) AdvancedShippingNotices + ShippingMethods — wire CarrierId FK
            // ================================================================
            mb.Sql(@"
                ALTER TABLE ""AdvancedShippingNotices""
                    ADD COLUMN IF NOT EXISTS ""CarrierId"" int NULL;
            ");

            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_asn_carrierid_carriers') THEN
                        ALTER TABLE ""AdvancedShippingNotices""
                            ADD CONSTRAINT fk_asn_carrierid_carriers
                            FOREIGN KEY (""CarrierId"") REFERENCES ""Carriers"" (""Id"") ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_asn_carrierid ON ""AdvancedShippingNotices"" (""CarrierId"") WHERE ""CarrierId"" IS NOT NULL;");

            mb.Sql(@"
                ALTER TABLE ""ShippingMethods""
                    ADD COLUMN IF NOT EXISTS ""CarrierId"" int NULL;
            ");

            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_shippingmethods_carrierid_carriers') THEN
                        ALTER TABLE ""ShippingMethods""
                            ADD CONSTRAINT fk_shippingmethods_carrierid_carriers
                            FOREIGN KEY (""CarrierId"") REFERENCES ""Carriers"" (""Id"") ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_shippingmethods_carrierid ON ""ShippingMethods"" (""CarrierId"") WHERE ""CarrierId"" IS NOT NULL;");

            // ================================================================
            // 7) Cockpit-sort indexes for default-inheritance + IndustryVertical
            // ================================================================
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_customers_defaultqualityprogram ON ""Customers"" (""DefaultQualityProgram"") WHERE ""DefaultQualityProgram"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_customers_defaultexportcontrol  ON ""Customers"" (""DefaultExportControl"")  WHERE ""DefaultExportControl"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_companies_industryvertical     ON ""Companies"" (""IndustryVertical"");");

            // ================================================================
            // 8) Demo-tenant IndustryVertical seed — safe: only stamps rows
            //    still showing IndustryVertical=0 (Unspecified) AND matching
            //    by name. Won't override explicit picks.
            // ================================================================
            mb.Sql(@"
                UPDATE ""Companies""
                   SET ""IndustryVertical"" = 1   -- Machining
                 WHERE ""IndustryVertical"" = 0
                   AND ""Name"" ILIKE '%ABS%';
            ");

            mb.Sql(@"
                UPDATE ""Companies""
                   SET ""IndustryVertical"" = 3   -- PrecisionEto
                 WHERE ""IndustryVertical"" = 0
                   AND (""Name"" ILIKE '%EVS%' OR ""Name"" ILIKE '%Edmonton Valve%');
            ");

            mb.Sql(@"
                UPDATE ""Companies""
                   SET ""IndustryVertical"" = 1,  -- Machining (placeholder for tenant operating company)
                       ""CageCode"" = COALESCE(""CageCode"", '0HAY4')
                 WHERE ""IndustryVertical"" = 0
                   AND (""Name"" ILIKE '%Cherry%' OR ""Name"" ILIKE '%IndustryOS%');
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // Reverse — DROP order respects FK dependencies.
            mb.Sql(@"ALTER TABLE ""ShippingMethods"" DROP CONSTRAINT IF EXISTS fk_shippingmethods_carrierid_carriers;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_shippingmethods_carrierid;");
            mb.Sql(@"ALTER TABLE ""ShippingMethods"" DROP COLUMN IF EXISTS ""CarrierId"";");

            mb.Sql(@"ALTER TABLE ""AdvancedShippingNotices"" DROP CONSTRAINT IF EXISTS fk_asn_carrierid_carriers;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_asn_carrierid;");
            mb.Sql(@"ALTER TABLE ""AdvancedShippingNotices"" DROP COLUMN IF EXISTS ""CarrierId"";");

            mb.Sql(@"DROP INDEX IF EXISTS ix_companies_industryvertical;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_customers_defaultexportcontrol;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_customers_defaultqualityprogram;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_manufacturers_cagecode;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_vendors_dea;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_vendors_fda;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_vendors_duns;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_vendors_cagecode;");

            mb.Sql(@"DROP TABLE IF EXISTS ""Carriers"" CASCADE;");

            mb.Sql(@"
                ALTER TABLE ""Manufacturers""
                    DROP COLUMN IF EXISTS ""DunsNumber"",
                    DROP COLUMN IF EXISTS ""CageCode"";
            ");

            mb.Sql(@"ALTER TABLE ""Vendors"" DROP CONSTRAINT IF EXISTS fk_vendors_paymenttermid_paymentterms;");
            mb.Sql(@"
                ALTER TABLE ""Vendors""
                    DROP COLUMN IF EXISTS ""PaymentTermId"",
                    DROP COLUMN IF EXISTS ""Iso13485CertRef"",
                    DROP COLUMN IF EXISTS ""Iso9001CertRef"",
                    DROP COLUMN IF EXISTS ""As9100CertRef"",
                    DROP COLUMN IF EXISTS ""ItarRegistration"",
                    DROP COLUMN IF EXISTS ""DeaRegistration"",
                    DROP COLUMN IF EXISTS ""FdaEstablishmentId"",
                    DROP COLUMN IF EXISTS ""UeiNumber"",
                    DROP COLUMN IF EXISTS ""DunsNumber"",
                    DROP COLUMN IF EXISTS ""CageCode"";
            ");

            mb.Sql(@"ALTER TABLE ""Customers"" DROP CONSTRAINT IF EXISTS ck_customers_creditlimit_nonneg;");
            mb.Sql(@"ALTER TABLE ""Customers"" DROP CONSTRAINT IF EXISTS ck_customers_defaultrevenuemode_range;");
            mb.Sql(@"ALTER TABLE ""Customers"" DROP CONSTRAINT IF EXISTS ck_customers_defaultcontracttype_range;");
            mb.Sql(@"ALTER TABLE ""Customers"" DROP CONSTRAINT IF EXISTS ck_customers_defaultexportcontrol_range;");
            mb.Sql(@"ALTER TABLE ""Customers"" DROP CONSTRAINT IF EXISTS ck_customers_defaultqualityprogram_range;");
            mb.Sql(@"ALTER TABLE ""Customers"" DROP CONSTRAINT IF EXISTS fk_customers_taxcodeid_taxcodes;");
            mb.Sql(@"
                ALTER TABLE ""Customers""
                    DROP COLUMN IF EXISTS ""BillToCountry"",
                    DROP COLUMN IF EXISTS ""BillToPostalCode"",
                    DROP COLUMN IF EXISTS ""BillToStateProvince"",
                    DROP COLUMN IF EXISTS ""BillToCity"",
                    DROP COLUMN IF EXISTS ""BillToAddress"",
                    DROP COLUMN IF EXISTS ""TaxCodeId"",
                    DROP COLUMN IF EXISTS ""CreditLimit"",
                    DROP COLUMN IF EXISTS ""DunsNumber"",
                    DROP COLUMN IF EXISTS ""CageCode"",
                    DROP COLUMN IF EXISTS ""DefaultRevenueMode"",
                    DROP COLUMN IF EXISTS ""DefaultContractType"",
                    DROP COLUMN IF EXISTS ""DefaultExportControl"",
                    DROP COLUMN IF EXISTS ""DefaultQualityProgram"";
            ");

            mb.Sql(@"ALTER TABLE ""Companies"" DROP CONSTRAINT IF EXISTS ck_companies_industryvertical_range;");
            mb.Sql(@"
                ALTER TABLE ""Companies""
                    DROP COLUMN IF EXISTS ""DunsNumber"",
                    DROP COLUMN IF EXISTS ""CageCode"",
                    DROP COLUMN IF EXISTS ""IndustryVertical"";
            ");
        }
    }
}
