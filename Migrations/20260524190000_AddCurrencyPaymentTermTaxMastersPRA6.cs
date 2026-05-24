// =============================================================================
// Sprint 13.5 PRA-6 — Currency / PaymentTerm / TaxAuthority / TaxCode masters.
// Master Files Baseline cascade ship #4.
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md §6
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md
//   - memory: feedback_no_shortcuts_multi_tenant_lineage.md
//
// IDEMPOTENT — CREATE TABLE IF NOT EXISTS + INSERT WHERE NOT EXISTS.
// All seed rows are CompanyId IS NULL (system).
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
    [Migration("20260524190000_AddCurrencyPaymentTermTaxMastersPRA6")]
    public partial class AddCurrencyPaymentTermTaxMastersPRA6 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) Currencies table + indexes
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""CurrencyMasters"" (
                    ""Id""              serial         PRIMARY KEY,
                    ""CompanyId""       integer        NULL,
                    ""IsoCode""         varchar(3)     NOT NULL,
                    ""NumericCode""     varchar(3)     NULL,
                    ""Name""            varchar(100)   NOT NULL,
                    ""Symbol""          varchar(8)     NULL,
                    ""DecimalPlaces""   integer        NOT NULL DEFAULT 2,
                    ""RoundingRule""    varchar(16)    NOT NULL DEFAULT 'HalfEven',
                    ""IsActive""        boolean        NOT NULL DEFAULT TRUE,
                    ""IsSystem""        boolean        NOT NULL DEFAULT FALSE,
                    ""SortOrder""       integer        NOT NULL DEFAULT 100,
                    ""CreatedAt""       timestamptz    NOT NULL DEFAULT NOW(),
                    ""CreatedBy""       varchar(100)   NULL,
                    ""ModifiedAt""      timestamptz    NULL,
                    ""ModifiedBy""      varchar(100)   NULL,
                    CONSTRAINT ck_currency_masters_dp CHECK (""DecimalPlaces"" BETWEEN 0 AND 4)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_currency_masters_system_iso
                    ON ""CurrencyMasters"" (""IsoCode"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_currency_masters_company_iso
                    ON ""CurrencyMasters"" (""CompanyId"", ""IsoCode"") WHERE ""CompanyId"" IS NOT NULL;
            ");

            // ================================================================
            // 2) PaymentTerms table + indexes
            //    NOTE: the existing tenant-data table from PR #5d's seed of
            //    "PaymentTermMasters" (the LookupValue rows) doesn't actually use
            //    this table name. We own the new master.
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PaymentTermMasters"" (
                    ""Id""                    serial          PRIMARY KEY,
                    ""CompanyId""             integer         NULL,
                    ""Code""                  varchar(32)     NOT NULL,
                    ""Name""                  varchar(100)    NOT NULL,
                    ""Description""           varchar(500)    NULL,
                    ""DueDays""               integer         NOT NULL DEFAULT 30,
                    ""DiscountPct""           numeric(7,4)    NOT NULL DEFAULT 0,
                    ""DiscountDays""          integer         NOT NULL DEFAULT 0,
                    ""BasisDate""             integer         NOT NULL DEFAULT 0,
                    ""MultiCutScheduleJson""  jsonb           NULL,
                    ""CurrencyId""            integer         NULL,
                    ""IsActive""              boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""              boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""             integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""             timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""             varchar(100)    NULL,
                    ""ModifiedAt""            timestamptz     NULL,
                    ""ModifiedBy""            varchar(100)    NULL,
                    CONSTRAINT ck_payment_term_masters_due_days CHECK (""DueDays"" BETWEEN 0 AND 3650),
                    CONSTRAINT ck_payment_term_masters_discount_pct CHECK (""DiscountPct"" BETWEEN 0 AND 100),
                    CONSTRAINT ck_payment_term_masters_basis_date CHECK (""BasisDate"" BETWEEN 0 AND 3)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_payment_term_masters_system_code
                    ON ""PaymentTermMasters"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_payment_term_masters_company_code
                    ON ""PaymentTermMasters"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_payment_term_masters_currency
                    ON ""PaymentTermMasters"" (""CurrencyId"") WHERE ""CurrencyId"" IS NOT NULL;
            ");

            // ================================================================
            // 3) TaxAuthorities table + indexes
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""TaxAuthorities"" (
                    ""Id""                    serial          PRIMARY KEY,
                    ""CompanyId""             integer         NULL,
                    ""Code""                  varchar(32)     NOT NULL,
                    ""Name""                  varchar(200)    NOT NULL,
                    ""CountryCode""           varchar(2)      NOT NULL DEFAULT 'US',
                    ""SubdivisionCode""       varchar(8)      NULL,
                    ""AdministrativeLevel""   integer         NOT NULL DEFAULT 0,
                    ""FilingFrequency""       integer         NOT NULL DEFAULT 1,
                    ""AgencyUrl""             varchar(500)    NULL,
                    ""IsActive""              boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""              boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""             integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""             timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""             varchar(100)    NULL,
                    ""ModifiedAt""            timestamptz     NULL,
                    ""ModifiedBy""            varchar(100)    NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_tax_authorities_system_code
                    ON ""TaxAuthorities"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_tax_authorities_company_code
                    ON ""TaxAuthorities"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_tax_authorities_country
                    ON ""TaxAuthorities"" (""CountryCode"");
            ");

            // ================================================================
            // 4) TaxCodes table + indexes
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""TaxCodeMasters"" (
                    ""Id""                    serial          PRIMARY KEY,
                    ""CompanyId""             integer         NULL,
                    ""Code""                  varchar(64)     NOT NULL,
                    ""Name""                  varchar(200)    NOT NULL,
                    ""Description""           varchar(500)    NULL,
                    ""TaxAuthorityId""        integer         NULL REFERENCES ""TaxAuthorities""(""Id"") ON DELETE SET NULL,
                    ""IsRecoverable""         boolean         NOT NULL DEFAULT FALSE,
                    ""IsInclusive""           boolean         NOT NULL DEFAULT FALSE,
                    ""IsReverseCharge""       boolean         NOT NULL DEFAULT FALSE,
                    ""InputGlAccountId""      integer         NULL,
                    ""OutputGlAccountId""     integer         NULL,
                    ""IsActive""              boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""              boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""             integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""             timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""             varchar(100)    NULL,
                    ""ModifiedAt""            timestamptz     NULL,
                    ""ModifiedBy""            varchar(100)    NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_tax_code_masters_system_code
                    ON ""TaxCodeMasters"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_tax_code_masters_company_code
                    ON ""TaxCodeMasters"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_tax_code_masters_authority
                    ON ""TaxCodeMasters"" (""TaxAuthorityId"") WHERE ""TaxAuthorityId"" IS NOT NULL;
            ");

            // ================================================================
            // 5) Seed system Currencies (30 ISO 4217 rows)
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""CurrencyMasters"" (""CompanyId"", ""IsoCode"", ""NumericCode"", ""Name"", ""Symbol"", ""DecimalPlaces"", ""IsSystem"", ""SortOrder"")
                VALUES
                    (NULL, 'USD', '840', 'US Dollar',           '$',    2, TRUE,  10),
                    (NULL, 'CAD', '124', 'Canadian Dollar',     'C$',   2, TRUE,  20),
                    (NULL, 'EUR', '978', 'Euro',                '€',    2, TRUE,  30),
                    (NULL, 'GBP', '826', 'British Pound',       '£',    2, TRUE,  40),
                    (NULL, 'JPY', '392', 'Japanese Yen',        '¥',    0, TRUE,  50),
                    (NULL, 'CHF', '756', 'Swiss Franc',         'CHF',  2, TRUE,  60),
                    (NULL, 'AUD', '036', 'Australian Dollar',   'A$',   2, TRUE,  70),
                    (NULL, 'NZD', '554', 'New Zealand Dollar',  'NZ$',  2, TRUE,  80),
                    (NULL, 'MXN', '484', 'Mexican Peso',        'Mex$', 2, TRUE,  90),
                    (NULL, 'CNY', '156', 'Chinese Yuan',        '¥',    2, TRUE, 100),
                    (NULL, 'INR', '356', 'Indian Rupee',        '₹',    2, TRUE, 110),
                    (NULL, 'BRL', '986', 'Brazilian Real',      'R$',   2, TRUE, 120),
                    (NULL, 'SEK', '752', 'Swedish Krona',       'kr',   2, TRUE, 130),
                    (NULL, 'NOK', '578', 'Norwegian Krone',     'kr',   2, TRUE, 140),
                    (NULL, 'DKK', '208', 'Danish Krone',        'kr',   2, TRUE, 150),
                    (NULL, 'KRW', '410', 'South Korean Won',    '₩',    0, TRUE, 160),
                    (NULL, 'ZAR', '710', 'South African Rand',  'R',    2, TRUE, 170),
                    (NULL, 'HKD', '344', 'Hong Kong Dollar',    'HK$',  2, TRUE, 180),
                    (NULL, 'SGD', '702', 'Singapore Dollar',    'S$',   2, TRUE, 190),
                    (NULL, 'TWD', '901', 'Taiwan Dollar',       'NT$',  2, TRUE, 200),
                    (NULL, 'THB', '764', 'Thai Baht',           '฿',    2, TRUE, 210),
                    (NULL, 'PLN', '985', 'Polish Zloty',        'zł',   2, TRUE, 220),
                    (NULL, 'CZK', '203', 'Czech Koruna',        'Kč',   2, TRUE, 230),
                    (NULL, 'HUF', '348', 'Hungarian Forint',    'Ft',   2, TRUE, 240),
                    (NULL, 'TRY', '949', 'Turkish Lira',        '₺',    2, TRUE, 250),
                    (NULL, 'ILS', '376', 'Israeli Shekel',      '₪',    2, TRUE, 260),
                    (NULL, 'AED', '784', 'UAE Dirham',          'AED',  2, TRUE, 270),
                    (NULL, 'SAR', '682', 'Saudi Riyal',         'SAR',  2, TRUE, 280),
                    (NULL, 'ARS', '032', 'Argentine Peso',      'Arg$', 2, TRUE, 290),
                    (NULL, 'CLP', '152', 'Chilean Peso',        'Ch$',  0, TRUE, 300)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 6) Seed system PaymentTerms (12 rows)
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""PaymentTermMasters"" (""CompanyId"", ""Code"", ""Name"", ""Description"", ""DueDays"", ""DiscountPct"", ""DiscountDays"", ""BasisDate"", ""IsSystem"", ""SortOrder"")
                VALUES
                    (NULL, 'DUE_ON_RCPT', 'Due on Receipt',     'Payment due immediately upon invoice receipt',                        0,   0,      0, 0, TRUE,  10),
                    (NULL, 'NET10',       'Net 10',             'Payment due 10 days after invoice date',                             10,   0,      0, 0, TRUE,  20),
                    (NULL, 'NET15',       'Net 15',             'Payment due 15 days after invoice date',                             15,   0,      0, 0, TRUE,  30),
                    (NULL, 'NET30',       'Net 30',             'Payment due 30 days after invoice date',                             30,   0,      0, 0, TRUE,  40),
                    (NULL, 'NET45',       'Net 45',             'Payment due 45 days after invoice date',                             45,   0,      0, 0, TRUE,  50),
                    (NULL, 'NET60',       'Net 60',             'Payment due 60 days after invoice date',                             60,   0,      0, 0, TRUE,  60),
                    (NULL, 'NET90',       'Net 90',             'Payment due 90 days after invoice date',                             90,   0,      0, 0, TRUE,  70),
                    (NULL, '2_10_N30',    '2/10 Net 30',        '2% discount if paid within 10 days; otherwise full payment in 30',   30, 2.0000, 10, 0, TRUE,  80),
                    (NULL, '1_10_N30',    '1/10 Net 30',        '1% discount if paid within 10 days; otherwise full payment in 30',   30, 1.0000, 10, 0, TRUE,  90),
                    (NULL, '2_20_N60',    '2/20 Net 60',        '2% discount if paid within 20 days; otherwise full payment in 60',   60, 2.0000, 20, 0, TRUE, 100),
                    (NULL, 'EOM_30',      'Net 30 EOM',         'Net 30 from end of invoice month',                                   30,   0,      0, 1, TRUE, 110),
                    (NULL, 'PREPAID',     'Prepaid',            'Payment required before goods/services delivered',                    0,   0,      0, 0, TRUE, 120),
                    (NULL, 'COD',         'Cash on Delivery',   'Payment due at time of physical delivery',                            0,   0,      0, 0, TRUE, 130)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 7) Seed system TaxAuthorities (6 rows)
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""TaxAuthorities"" (""CompanyId"", ""Code"", ""Name"", ""CountryCode"", ""SubdivisionCode"", ""AdministrativeLevel"", ""FilingFrequency"", ""AgencyUrl"", ""IsSystem"", ""SortOrder"")
                VALUES
                    (NULL, 'US-IRS',      'Internal Revenue Service',                          'US', NULL,    0, 1, 'https://www.irs.gov',                             TRUE,  10),
                    (NULL, 'US-CA-CDTFA', 'California Department of Tax and Fee Administration','US','US-CA', 1, 0, 'https://www.cdtfa.ca.gov',                        TRUE,  20),
                    (NULL, 'US-NY-DTF',   'New York Department of Taxation and Finance',       'US','US-NY', 1, 0, 'https://www.tax.ny.gov',                          TRUE,  30),
                    (NULL, 'CA-CRA',      'Canada Revenue Agency',                             'CA', NULL,    0, 1, 'https://www.canada.ca/en/revenue-agency.html',    TRUE,  40),
                    (NULL, 'CA-ON-MOF',   'Ontario Ministry of Finance',                       'CA','CA-ON', 2, 1, 'https://www.fin.gov.on.ca',                       TRUE,  50),
                    (NULL, 'EU-VAT',      'European Union VAT (member states)',                'EU', NULL,    0, 1, 'https://taxation-customs.ec.europa.eu',           TRUE,  60)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 8) Seed system TaxCodes (10 rows)
            // ================================================================
            mb.Sql(@"
                WITH auth AS (
                    SELECT ""Id"", ""Code"" FROM ""TaxAuthorities"" WHERE ""CompanyId"" IS NULL
                )
                INSERT INTO ""TaxCodeMasters"" (""CompanyId"", ""Code"", ""Name"", ""Description"", ""TaxAuthorityId"", ""IsRecoverable"", ""IsInclusive"", ""IsReverseCharge"", ""IsSystem"", ""SortOrder"")
                VALUES
                    (NULL, 'NOTAX',              'No Tax',                       'Sentinel — no tax applies',                                    NULL,                                                  FALSE, FALSE, FALSE, TRUE,  10),
                    (NULL, 'ZERO_RATED',         'Zero Rated',                   'Taxable at 0% (e.g. exports, food)',                           (SELECT ""Id"" FROM auth WHERE ""Code""='US-IRS'),         FALSE, FALSE, FALSE, TRUE,  20),
                    (NULL, 'EXEMPT',             'Exempt',                       'Exempt from tax (e.g. nonprofit, government)',                 (SELECT ""Id"" FROM auth WHERE ""Code""='US-IRS'),         FALSE, FALSE, FALSE, TRUE,  30),
                    (NULL, 'US-CA-SALES',        'California State Sales Tax',   'California state base sales tax — rate per PRA-10',            (SELECT ""Id"" FROM auth WHERE ""Code""='US-CA-CDTFA'),     FALSE, FALSE, FALSE, TRUE,  40),
                    (NULL, 'US-NY-SALES',        'New York State Sales Tax',     'New York state base sales tax — rate per PRA-10',              (SELECT ""Id"" FROM auth WHERE ""Code""='US-NY-DTF'),       FALSE, FALSE, FALSE, TRUE,  50),
                    (NULL, 'CA-GST',             'Canada GST (5%)',              'Canadian federal Goods and Services Tax — recoverable',        (SELECT ""Id"" FROM auth WHERE ""Code""='CA-CRA'),          TRUE,  FALSE, FALSE, TRUE,  60),
                    (NULL, 'CA-ON-HST',          'Ontario HST (13%)',            'Ontario Harmonized Sales Tax — recoverable',                   (SELECT ""Id"" FROM auth WHERE ""Code""='CA-ON-MOF'),       TRUE,  FALSE, FALSE, TRUE,  70),
                    (NULL, 'EU-VAT-STANDARD',    'EU VAT Standard',              'EU VAT standard rate — rate per member state and PRA-10',      (SELECT ""Id"" FROM auth WHERE ""Code""='EU-VAT'),          TRUE,  FALSE, FALSE, TRUE,  80),
                    (NULL, 'EU-VAT-REDUCED',     'EU VAT Reduced',               'EU VAT reduced rate (food, books, etc.)',                       (SELECT ""Id"" FROM auth WHERE ""Code""='EU-VAT'),          TRUE,  FALSE, FALSE, TRUE,  90),
                    (NULL, 'EU-VAT-REVERSE',     'EU VAT Reverse Charge',        'Intra-community supply — buyer self-assesses',                  (SELECT ""Id"" FROM auth WHERE ""Code""='EU-VAT'),          TRUE,  FALSE, TRUE,  TRUE, 100)
                ON CONFLICT DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"
                DROP TABLE IF EXISTS ""TaxCodeMasters"";
                DROP TABLE IF EXISTS ""TaxAuthorities"";
                DROP TABLE IF EXISTS ""PaymentTermMasters"";
                DROP TABLE IF EXISTS ""CurrencyMasters"";
            ");
        }
    }
}
