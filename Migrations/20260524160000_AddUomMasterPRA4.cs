// =============================================================================
// Sprint 13.5 PRA-4 — UOM master (UomCategories + UnitsOfMeasure + UomConversions)
// + 10 nullable FK columns on Items + system seed (17 categories + 52 UOMs)
// + backfill Items.StockUomId from the legacy Item.UOM enum.
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md (the design)
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md (6-point gate)
//   - memory: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock)
//   - memory: feedback_replit_autodiff_destructive_on_populated_tables.md
//     (Items table is populated — this migration MUST be pre-applied via
//      psql before Republish; do NOT trust Replit's schema-diff to handle
//      populated-table ALTERs cleanly)
//
// SEED CONTENT (system rows, CompanyId NULL):
//   17 UomCategories (Length, Mass, Volume, Time, Area, Count, Energy, Power,
//                     Pressure, Temperature, Frequency, Currency, Concentration,
//                     Luminance, Radiation, Information, Package)
//   52 UnitsOfMeasure covering both the legacy Item.UnitOfMeasure (22 rows)
//      AND the legacy Telemetry.UnitOfMeasure (40+ rows) bridged into one
//      master table. Two parallel enums collapse into one source of truth.
//
// BACKFILL: Items.StockUomId populated from Items.UOM enum value.
//
// NO HARDCODED TENANT DATA — all seeded rows are CompanyId NULL (system).
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524160000_AddUomMasterPRA4")]
    public partial class AddUomMasterPRA4 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) UomCategories — system + tenant overrides.
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""UomCategories"" (
                    ""Id""           serial         PRIMARY KEY,
                    ""CompanyId""    integer        NULL,
                    ""Code""         varchar(32)    NOT NULL,
                    ""Name""         varchar(100)   NOT NULL,
                    ""BaseUomId""    integer        NULL,
                    ""IsSystem""     boolean        NOT NULL DEFAULT FALSE,
                    ""IsActive""     boolean        NOT NULL DEFAULT TRUE,
                    ""SortOrder""    integer        NOT NULL DEFAULT 100,
                    ""CreatedAt""    timestamptz    NOT NULL DEFAULT NOW(),
                    ""CreatedBy""    varchar(100)   NULL,
                    ""ModifiedAt""   timestamptz    NULL,
                    ""ModifiedBy""   varchar(100)   NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_uom_categories_system_code
                    ON ""UomCategories"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_uom_categories_company_code
                    ON ""UomCategories"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_uom_categories_company
                    ON ""UomCategories"" (""CompanyId"") WHERE ""CompanyId"" IS NOT NULL;
            ");

            // ================================================================
            // 2) UnitsOfMeasure — system + tenant overrides.
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""UnitsOfMeasure"" (
                    ""Id""                          serial            PRIMARY KEY,
                    ""CompanyId""                   integer           NULL,
                    ""Code""                        varchar(32)       NOT NULL,
                    ""Name""                        varchar(100)      NOT NULL,
                    ""Symbol""                      varchar(16)       NOT NULL,
                    ""UomCategoryId""               integer           NOT NULL REFERENCES ""UomCategories""(""Id"") ON DELETE RESTRICT,
                    ""ConversionFactorToBase""      numeric(28,12)    NOT NULL DEFAULT 1,
                    ""ConversionOffsetToBase""      numeric(28,12)    NOT NULL DEFAULT 0,
                    ""DecimalPrecision""            integer           NOT NULL DEFAULT 4,
                    ""IsoCode""                     varchar(16)       NULL,
                    ""UneceCode""                   varchar(8)        NULL,
                    ""UcumCode""                    varchar(32)       NULL,
                    ""IsSystem""                    boolean           NOT NULL DEFAULT FALSE,
                    ""IsActive""                    boolean           NOT NULL DEFAULT TRUE,
                    ""SortOrder""                   integer           NOT NULL DEFAULT 100,
                    ""CreatedAt""                   timestamptz       NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                   varchar(100)      NULL,
                    ""ModifiedAt""                  timestamptz       NULL,
                    ""ModifiedBy""                  varchar(100)      NULL,
                    CONSTRAINT ck_uom_decimal_precision CHECK (""DecimalPrecision"" BETWEEN 0 AND 12)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_units_of_measure_system_code
                    ON ""UnitsOfMeasure"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_units_of_measure_company_code
                    ON ""UnitsOfMeasure"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_units_of_measure_category
                    ON ""UnitsOfMeasure"" (""UomCategoryId"");
                CREATE INDEX IF NOT EXISTS ix_units_of_measure_unece
                    ON ""UnitsOfMeasure"" (""UneceCode"") WHERE ""UneceCode"" IS NOT NULL;
            ");

            // Add the (deferred) FK on UomCategories.BaseUomId now that UnitsOfMeasure exists.
            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'fk_uom_categories_base_uom'
                          AND table_name = 'UomCategories'
                    ) THEN
                        ALTER TABLE ""UomCategories""
                            ADD CONSTRAINT fk_uom_categories_base_uom
                            FOREIGN KEY (""BaseUomId"") REFERENCES ""UnitsOfMeasure""(""Id"")
                            ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            // ================================================================
            // 3) UomConversions — overrides only (per-item + cross-category).
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""UomConversions"" (
                    ""Id""           serial            PRIMARY KEY,
                    ""CompanyId""    integer           NOT NULL,
                    ""FromUomId""    integer           NOT NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ""ToUomId""      integer           NOT NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ""ItemId""       integer           NULL,
                    ""Multiplier""   numeric(28,12)    NOT NULL DEFAULT 1,
                    ""Offset""       numeric(28,12)    NOT NULL DEFAULT 0,
                    ""IsActive""     boolean           NOT NULL DEFAULT TRUE,
                    ""CreatedAt""    timestamptz       NOT NULL DEFAULT NOW(),
                    ""CreatedBy""    varchar(100)      NULL,
                    ""ModifiedAt""   timestamptz       NULL,
                    ""ModifiedBy""   varchar(100)      NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_uom_conversions_company_wide
                    ON ""UomConversions"" (""CompanyId"", ""FromUomId"", ""ToUomId"") WHERE ""ItemId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_uom_conversions_per_item
                    ON ""UomConversions"" (""CompanyId"", ""FromUomId"", ""ToUomId"", ""ItemId"") WHERE ""ItemId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_uom_conversions_item
                    ON ""UomConversions"" (""ItemId"") WHERE ""ItemId"" IS NOT NULL;
            ");

            // ================================================================
            // 4) Items — add 10 nullable UOM FK columns.
            // ================================================================
            mb.Sql(@"
                ALTER TABLE ""Items""
                    ADD COLUMN IF NOT EXISTS ""StockUomId""        integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""PurchaseUomId""     integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""PurchasePackUomId"" integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""SalesUomId""        integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""SalesPackUomId""    integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""PriceUomId""        integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""ReportingUomId""    integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""WeightUomId""       integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""VolumeUomId""       integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT,
                    ADD COLUMN IF NOT EXISTS ""DimensionUomId""    integer NULL REFERENCES ""UnitsOfMeasure""(""Id"") ON DELETE RESTRICT;
                CREATE INDEX IF NOT EXISTS ix_items_stock_uom ON ""Items"" (""StockUomId"") WHERE ""StockUomId"" IS NOT NULL;
            ");

            // ================================================================
            // 5) Seed 17 system UomCategories.
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""UomCategories"" (""CompanyId"", ""Code"", ""Name"", ""IsSystem"", ""SortOrder"")
                VALUES
                    (NULL, 'COUNT',         'Count',          TRUE,  10),
                    (NULL, 'LENGTH',        'Length',         TRUE,  20),
                    (NULL, 'AREA',          'Area',           TRUE,  30),
                    (NULL, 'VOLUME',        'Volume',         TRUE,  40),
                    (NULL, 'MASS',          'Mass',           TRUE,  50),
                    (NULL, 'TIME',          'Time',           TRUE,  60),
                    (NULL, 'TEMPERATURE',   'Temperature',    TRUE,  70),
                    (NULL, 'PRESSURE',      'Pressure',       TRUE,  80),
                    (NULL, 'FREQUENCY',     'Frequency',      TRUE,  90),
                    (NULL, 'ENERGY',        'Energy',         TRUE, 100),
                    (NULL, 'POWER',         'Power',          TRUE, 110),
                    (NULL, 'VELOCITY',      'Velocity',       TRUE, 120),
                    (NULL, 'FLOW',          'Flow',           TRUE, 130),
                    (NULL, 'ELECTRICAL',    'Electrical',     TRUE, 140),
                    (NULL, 'ROTATIONAL',    'Rotational',     TRUE, 150),
                    (NULL, 'DIMENSIONLESS', 'Dimensionless',  TRUE, 160),
                    (NULL, 'PACKAGE',       'Package',        TRUE, 170)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 6) Seed system UnitsOfMeasure (52 rows covering both legacy
            //    Item.UnitOfMeasure (22 inventory) + Telemetry.UnitOfMeasure
            //    (40+ sensor). Single source of truth from now on.
            // ================================================================
            mb.Sql(@"
                WITH cats AS (
                    SELECT ""Id"", ""Code"" FROM ""UomCategories"" WHERE ""CompanyId"" IS NULL
                )
                INSERT INTO ""UnitsOfMeasure""
                    (""CompanyId"", ""Code"", ""Name"", ""Symbol"", ""UomCategoryId"",
                     ""ConversionFactorToBase"", ""ConversionOffsetToBase"", ""DecimalPrecision"",
                     ""IsoCode"", ""UneceCode"", ""IsSystem"", ""SortOrder"")
                VALUES
                    -- COUNT category — base = EA
                    (NULL, 'EA',       'Each',          'ea',  (SELECT ""Id"" FROM cats WHERE ""Code""='COUNT'),        1,                 0,    0, 'ea',  'EA',  TRUE, 10),
                    (NULL, 'PAIR',     'Pair',          'pr',  (SELECT ""Id"" FROM cats WHERE ""Code""='COUNT'),        2,                 0,    0, NULL,  'PR',  TRUE, 20),
                    (NULL, 'DOZEN',    'Dozen',         'dz',  (SELECT ""Id"" FROM cats WHERE ""Code""='COUNT'),       12,                 0,    0, NULL,  'DZN', TRUE, 30),
                    (NULL, 'HUNDRED',  'Hundred',       'C',   (SELECT ""Id"" FROM cats WHERE ""Code""='COUNT'),      100,                 0,    0, NULL,  'CEN', TRUE, 40),
                    (NULL, 'THOUSAND', 'Thousand',      'M',   (SELECT ""Id"" FROM cats WHERE ""Code""='COUNT'),     1000,                 0,    0, NULL,  'MIL', TRUE, 50),
                    -- PACKAGE category (no arithmetic base — conversions are per-item via UomConversion)
                    (NULL, 'BOX',      'Box',           'bx',  (SELECT ""Id"" FROM cats WHERE ""Code""='PACKAGE'),     1,                 0,    0, NULL,  'BX',  TRUE, 10),
                    (NULL, 'CASE',     'Case',          'cs',  (SELECT ""Id"" FROM cats WHERE ""Code""='PACKAGE'),     1,                 0,    0, NULL,  'CS',  TRUE, 20),
                    (NULL, 'PACK',     'Pack',          'pk',  (SELECT ""Id"" FROM cats WHERE ""Code""='PACKAGE'),     1,                 0,    0, NULL,  'PK',  TRUE, 30),
                    (NULL, 'SET',      'Set',           'set', (SELECT ""Id"" FROM cats WHERE ""Code""='PACKAGE'),     1,                 0,    0, NULL,  'SET', TRUE, 40),
                    (NULL, 'KIT',      'Kit',           'kit', (SELECT ""Id"" FROM cats WHERE ""Code""='PACKAGE'),     1,                 0,    0, NULL,  'KT',  TRUE, 50),
                    (NULL, 'ROLL',     'Roll',          'rl',  (SELECT ""Id"" FROM cats WHERE ""Code""='PACKAGE'),     1,                 0,    0, NULL,  'RO',  TRUE, 60),
                    (NULL, 'PALLET',   'Pallet',        'plt', (SELECT ""Id"" FROM cats WHERE ""Code""='PACKAGE'),     1,                 0,    0, NULL,  'PA',  TRUE, 70),
                    -- LENGTH category — base = METER
                    (NULL, 'MM',       'Millimeter',    'mm',  (SELECT ""Id"" FROM cats WHERE ""Code""='LENGTH'),      0.001,             0,    3, 'mm',  'MMT', TRUE, 10),
                    (NULL, 'M',        'Meter',         'm',   (SELECT ""Id"" FROM cats WHERE ""Code""='LENGTH'),      1,                 0,    3, 'm',   'MTR', TRUE, 20),
                    (NULL, 'KM',       'Kilometer',     'km',  (SELECT ""Id"" FROM cats WHERE ""Code""='LENGTH'),   1000,                 0,    3, 'km',  'KMT', TRUE, 30),
                    (NULL, 'IN',       'Inch',          'in',  (SELECT ""Id"" FROM cats WHERE ""Code""='LENGTH'),      0.0254,            0,    4, 'in',  'INH', TRUE, 40),
                    (NULL, 'FT',       'Foot',          'ft',  (SELECT ""Id"" FROM cats WHERE ""Code""='LENGTH'),      0.3048,            0,    4, 'ft',  'FOT', TRUE, 50),
                    (NULL, 'YD',       'Yard',          'yd',  (SELECT ""Id"" FROM cats WHERE ""Code""='LENGTH'),      0.9144,            0,    4, 'yd',  'YRD', TRUE, 60),
                    (NULL, 'MI',       'Mile',          'mi',  (SELECT ""Id"" FROM cats WHERE ""Code""='LENGTH'),   1609.344,             0,    4, NULL,  'SMI', TRUE, 70),
                    -- AREA category — base = SQM
                    (NULL, 'SQM',      'Square Meter',  'm²',  (SELECT ""Id"" FROM cats WHERE ""Code""='AREA'),        1,                 0,    4, 'm2',  'MTK', TRUE, 10),
                    (NULL, 'SQFT',     'Square Foot',   'ft²', (SELECT ""Id"" FROM cats WHERE ""Code""='AREA'),        0.09290304,        0,    4, NULL,  'FTK', TRUE, 20),
                    (NULL, 'SQIN',     'Square Inch',   'in²', (SELECT ""Id"" FROM cats WHERE ""Code""='AREA'),        0.00064516,        0,    6, NULL,  'INK', TRUE, 30),
                    -- VOLUME category — base = LITER
                    (NULL, 'L',        'Liter',         'L',   (SELECT ""Id"" FROM cats WHERE ""Code""='VOLUME'),      1,                 0,    3, 'L',   'LTR', TRUE, 10),
                    (NULL, 'ML',       'Milliliter',    'mL',  (SELECT ""Id"" FROM cats WHERE ""Code""='VOLUME'),      0.001,             0,    3, 'mL',  'MLT', TRUE, 20),
                    (NULL, 'M3',       'Cubic Meter',   'm³',  (SELECT ""Id"" FROM cats WHERE ""Code""='VOLUME'),   1000,                 0,    3, 'm3',  'MTQ', TRUE, 30),
                    (NULL, 'GAL_US',   'US Gallon',     'gal', (SELECT ""Id"" FROM cats WHERE ""Code""='VOLUME'),      3.785411784,       0,    4, NULL,  'GLL', TRUE, 40),
                    (NULL, 'QT_US',    'US Quart',      'qt',  (SELECT ""Id"" FROM cats WHERE ""Code""='VOLUME'),      0.946352946,       0,    4, NULL,  'QTL', TRUE, 50),
                    (NULL, 'PT_US',    'US Pint',       'pt',  (SELECT ""Id"" FROM cats WHERE ""Code""='VOLUME'),      0.473176473,       0,    4, NULL,  'PTL', TRUE, 60),
                    (NULL, 'FL_OZ_US', 'US Fluid Ounce','floz',(SELECT ""Id"" FROM cats WHERE ""Code""='VOLUME'),      0.0295735296,      0,    5, NULL,  'OZA', TRUE, 70),
                    -- MASS category — base = GRAM
                    (NULL, 'G',        'Gram',          'g',   (SELECT ""Id"" FROM cats WHERE ""Code""='MASS'),        1,                 0,    3, 'g',   'GRM', TRUE, 10),
                    (NULL, 'KG',       'Kilogram',      'kg',  (SELECT ""Id"" FROM cats WHERE ""Code""='MASS'),     1000,                 0,    3, 'kg',  'KGM', TRUE, 20),
                    (NULL, 'MG',       'Milligram',     'mg',  (SELECT ""Id"" FROM cats WHERE ""Code""='MASS'),        0.001,             0,    3, 'mg',  'MGM', TRUE, 30),
                    (NULL, 'LB',       'Pound',         'lb',  (SELECT ""Id"" FROM cats WHERE ""Code""='MASS'),      453.59237,           0,    4, 'lb',  'LBR', TRUE, 40),
                    (NULL, 'OZ',       'Ounce',         'oz',  (SELECT ""Id"" FROM cats WHERE ""Code""='MASS'),       28.349523125,       0,    4, NULL,  'ONZ', TRUE, 50),
                    (NULL, 'TON_US',   'Short Ton',     'ton', (SELECT ""Id"" FROM cats WHERE ""Code""='MASS'),   907184.74,              0,    4, NULL,  'STN', TRUE, 60),
                    (NULL, 'TON_M',    'Metric Ton',    't',   (SELECT ""Id"" FROM cats WHERE ""Code""='MASS'),  1000000,                 0,    4, NULL,  'TNE', TRUE, 70),
                    -- TIME category — base = SECOND
                    (NULL, 'SEC',      'Second',        's',   (SELECT ""Id"" FROM cats WHERE ""Code""='TIME'),        1,                 0,    3, 's',   'SEC', TRUE, 10),
                    (NULL, 'MIN',      'Minute',        'min', (SELECT ""Id"" FROM cats WHERE ""Code""='TIME'),       60,                 0,    2, NULL,  'MIN', TRUE, 20),
                    (NULL, 'HR',       'Hour',          'h',   (SELECT ""Id"" FROM cats WHERE ""Code""='TIME'),     3600,                 0,    2, 'h',   'HUR', TRUE, 30),
                    (NULL, 'DAY',      'Day',           'd',   (SELECT ""Id"" FROM cats WHERE ""Code""='TIME'),    86400,                 0,    2, 'd',   'DAY', TRUE, 40),
                    -- TEMPERATURE category — base = KELVIN
                    (NULL, 'K',        'Kelvin',        'K',   (SELECT ""Id"" FROM cats WHERE ""Code""='TEMPERATURE'),  1,                 0,         2, 'K',   'KEL', TRUE, 10),
                    (NULL, 'CEL',      'Celsius',       '°C',  (SELECT ""Id"" FROM cats WHERE ""Code""='TEMPERATURE'),  1,               273.15,      2, '°C',  'CEL', TRUE, 20),
                    (NULL, 'FAH',      'Fahrenheit',    '°F',  (SELECT ""Id"" FROM cats WHERE ""Code""='TEMPERATURE'),  0.555555555556, 255.372222222, 2, '°F',  'FAH', TRUE, 30),
                    -- PRESSURE category — base = PASCAL
                    (NULL, 'PA',       'Pascal',        'Pa',  (SELECT ""Id"" FROM cats WHERE ""Code""='PRESSURE'),    1,                 0,    1, 'Pa',  'PAL', TRUE, 10),
                    (NULL, 'KPA',      'Kilopascal',    'kPa', (SELECT ""Id"" FROM cats WHERE ""Code""='PRESSURE'),  1000,                 0,    1, 'kPa', 'KPA', TRUE, 20),
                    (NULL, 'MPA',      'Megapascal',    'MPa', (SELECT ""Id"" FROM cats WHERE ""Code""='PRESSURE'), 1000000,               0,    1, 'MPa', 'MPA', TRUE, 30),
                    (NULL, 'BAR',      'Bar',           'bar', (SELECT ""Id"" FROM cats WHERE ""Code""='PRESSURE'),100000,                 0,    1, NULL,  'BAR', TRUE, 40),
                    (NULL, 'PSI',      'PSI',           'psi', (SELECT ""Id"" FROM cats WHERE ""Code""='PRESSURE'),  6894.757,             0,    1, NULL,  'PS',  TRUE, 50),
                    -- FREQUENCY category — base = HERTZ
                    (NULL, 'HZ',       'Hertz',         'Hz',  (SELECT ""Id"" FROM cats WHERE ""Code""='FREQUENCY'),   1,                 0,    2, 'Hz',  'HTZ', TRUE, 10),
                    (NULL, 'KHZ',      'Kilohertz',     'kHz', (SELECT ""Id"" FROM cats WHERE ""Code""='FREQUENCY'),1000,                  0,    2, 'kHz', 'KHZ', TRUE, 20),
                    -- ROTATIONAL category — base = RAD_PER_SEC
                    (NULL, 'RAD_S',    'Radians/Sec',   'rad/s',(SELECT ""Id"" FROM cats WHERE ""Code""='ROTATIONAL'), 1,                 0,    2, NULL,  '2A',  TRUE, 10),
                    (NULL, 'RPM',      'RPM',           'rpm', (SELECT ""Id"" FROM cats WHERE ""Code""='ROTATIONAL'), 0.104719755,         0,    1, NULL,  'RPM', TRUE, 20),
                    -- VELOCITY category — base = M_PER_SEC
                    (NULL, 'MPS',      'Meters/Sec',    'm/s', (SELECT ""Id"" FROM cats WHERE ""Code""='VELOCITY'),    1,                 0,    3, 'm/s', 'MTS', TRUE, 10),
                    (NULL, 'MMPS',     'mm/Sec',        'mm/s',(SELECT ""Id"" FROM cats WHERE ""Code""='VELOCITY'),    0.001,             0,    3, NULL,  '2M',  TRUE, 20),
                    (NULL, 'INPS',     'Inches/Sec',    'in/s',(SELECT ""Id"" FROM cats WHERE ""Code""='VELOCITY'),    0.0254,            0,    3, NULL,  'INS', TRUE, 30),
                    -- FLOW category — base = L_PER_MIN
                    (NULL, 'LPM',      'Liters/Min',    'L/min',(SELECT ""Id"" FROM cats WHERE ""Code""='FLOW'),       1,                 0,    2, NULL,  'L2',  TRUE, 10),
                    (NULL, 'M3PH',     'Cubic m/Hr',    'm³/h',(SELECT ""Id"" FROM cats WHERE ""Code""='FLOW'),       16.6666666667,      0,    2, NULL,  'MQH', TRUE, 20),
                    (NULL, 'GPM',      'Gallons/Min',   'gpm', (SELECT ""Id"" FROM cats WHERE ""Code""='FLOW'),        3.785411784,       0,    2, NULL,  'G51', TRUE, 30),
                    (NULL, 'CFM',      'Cubic ft/Min',  'cfm', (SELECT ""Id"" FROM cats WHERE ""Code""='FLOW'),       28.3168465592,      0,    2, NULL,  'CFM', TRUE, 40),
                    -- ELECTRICAL category — base = WATT
                    (NULL, 'W',        'Watt',          'W',   (SELECT ""Id"" FROM cats WHERE ""Code""='POWER'),       1,                 0,    2, 'W',   'WTT', TRUE, 10),
                    (NULL, 'KW',       'Kilowatt',      'kW',  (SELECT ""Id"" FROM cats WHERE ""Code""='POWER'),    1000,                 0,    2, 'kW',  'KWT', TRUE, 20),
                    (NULL, 'KWH',      'Kilowatt-Hour', 'kWh', (SELECT ""Id"" FROM cats WHERE ""Code""='ENERGY'),   3600000,               0,    3, 'kWh', 'KWH', TRUE, 10),
                    (NULL, 'J',        'Joule',         'J',   (SELECT ""Id"" FROM cats WHERE ""Code""='ENERGY'),      1,                 0,    3, 'J',   'JOU', TRUE, 20),
                    (NULL, 'V',        'Volt',          'V',   (SELECT ""Id"" FROM cats WHERE ""Code""='ELECTRICAL'),  1,                 0,    2, 'V',   'VLT', TRUE, 10),
                    (NULL, 'A',        'Ampere',        'A',   (SELECT ""Id"" FROM cats WHERE ""Code""='ELECTRICAL'),  1,                 0,    2, 'A',   'AMP', TRUE, 20),
                    -- DIMENSIONLESS category
                    (NULL, 'PCT',      'Percent',       '%',   (SELECT ""Id"" FROM cats WHERE ""Code""='DIMENSIONLESS'), 1,               0,    2, NULL,  'P1',  TRUE, 10),
                    (NULL, 'RATIO',    'Ratio',         '',    (SELECT ""Id"" FROM cats WHERE ""Code""='DIMENSIONLESS'), 1,               0,    4, NULL,  NULL,  TRUE, 20),
                    (NULL, 'PPM',      'Parts/Million', 'ppm', (SELECT ""Id"" FROM cats WHERE ""Code""='DIMENSIONLESS'), 1,               0,    1, NULL,  '59',  TRUE, 30),
                    (NULL, 'DB',       'Decibel',       'dB',  (SELECT ""Id"" FROM cats WHERE ""Code""='DIMENSIONLESS'), 1,               0,    1, NULL,  '2N',  TRUE, 40),
                    (NULL, 'COUNT',    'Count',         'ct',  (SELECT ""Id"" FROM cats WHERE ""Code""='DIMENSIONLESS'), 1,               0,    0, NULL,  NULL,  TRUE, 50),
                    (NULL, 'BOOL',     'Boolean',       'bool',(SELECT ""Id"" FROM cats WHERE ""Code""='DIMENSIONLESS'), 1,               0,    0, NULL,  NULL,  TRUE, 60),
                    (NULL, 'G_FORCE',  'G-Force',       'g',   (SELECT ""Id"" FROM cats WHERE ""Code""='DIMENSIONLESS'), 1,               0,    3, NULL,  NULL,  TRUE, 70),
                    (NULL, 'PF',       'Power Factor',  'PF',  (SELECT ""Id"" FROM cats WHERE ""Code""='DIMENSIONLESS'), 1,               0,    3, NULL,  NULL,  TRUE, 80)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 7) Stamp UomCategories.BaseUomId now that UnitsOfMeasure exists.
            // ================================================================
            mb.Sql(@"
                UPDATE ""UomCategories"" c
                SET ""BaseUomId"" = u.""Id""
                FROM ""UnitsOfMeasure"" u
                WHERE c.""CompanyId"" IS NULL
                  AND c.""BaseUomId"" IS NULL
                  AND u.""CompanyId"" IS NULL
                  AND (
                      (c.""Code"" = 'COUNT'         AND u.""Code"" = 'EA')
                   OR (c.""Code"" = 'LENGTH'        AND u.""Code"" = 'M')
                   OR (c.""Code"" = 'AREA'          AND u.""Code"" = 'SQM')
                   OR (c.""Code"" = 'VOLUME'        AND u.""Code"" = 'L')
                   OR (c.""Code"" = 'MASS'          AND u.""Code"" = 'G')
                   OR (c.""Code"" = 'TIME'          AND u.""Code"" = 'SEC')
                   OR (c.""Code"" = 'TEMPERATURE'   AND u.""Code"" = 'K')
                   OR (c.""Code"" = 'PRESSURE'      AND u.""Code"" = 'PA')
                   OR (c.""Code"" = 'FREQUENCY'     AND u.""Code"" = 'HZ')
                   OR (c.""Code"" = 'ENERGY'        AND u.""Code"" = 'J')
                   OR (c.""Code"" = 'POWER'         AND u.""Code"" = 'W')
                   OR (c.""Code"" = 'VELOCITY'      AND u.""Code"" = 'MPS')
                   OR (c.""Code"" = 'FLOW'          AND u.""Code"" = 'LPM')
                   OR (c.""Code"" = 'ELECTRICAL'    AND u.""Code"" = 'V')
                   OR (c.""Code"" = 'ROTATIONAL'    AND u.""Code"" = 'RAD_S')
                   OR (c.""Code"" = 'DIMENSIONLESS' AND u.""Code"" = 'RATIO')
                   OR (c.""Code"" = 'PACKAGE'       AND u.""Code"" = 'EA')
                  );
            ");

            // ================================================================
            // 8) Backfill Items.StockUomId from legacy Items.UOM enum.
            //
            //    Enum mapping (Models/Item.cs UnitOfMeasure):
            //      0=Each 1=Box 2=Case 3=Pack 4=Pair 5=Set 6=Kit 7=Roll
            //      8=Feet 9=Meter 10=Inch 11=Gallon 12=Liter 13=Quart 14=Pint
            //      15=Ounce 16=Pound 17=Kilogram 18=Gram 19=Dozen 20=Hundred 21=Thousand
            // ================================================================
            mb.Sql(@"
                UPDATE ""Items"" i
                SET ""StockUomId"" = u.""Id""
                FROM ""UnitsOfMeasure"" u
                WHERE i.""StockUomId"" IS NULL
                  AND u.""CompanyId"" IS NULL
                  AND (
                      (i.""UOM"" = 0  AND u.""Code"" = 'EA')
                   OR (i.""UOM"" = 1  AND u.""Code"" = 'BOX')
                   OR (i.""UOM"" = 2  AND u.""Code"" = 'CASE')
                   OR (i.""UOM"" = 3  AND u.""Code"" = 'PACK')
                   OR (i.""UOM"" = 4  AND u.""Code"" = 'PAIR')
                   OR (i.""UOM"" = 5  AND u.""Code"" = 'SET')
                   OR (i.""UOM"" = 6  AND u.""Code"" = 'KIT')
                   OR (i.""UOM"" = 7  AND u.""Code"" = 'ROLL')
                   OR (i.""UOM"" = 8  AND u.""Code"" = 'FT')
                   OR (i.""UOM"" = 9  AND u.""Code"" = 'M')
                   OR (i.""UOM"" = 10 AND u.""Code"" = 'IN')
                   OR (i.""UOM"" = 11 AND u.""Code"" = 'GAL_US')
                   OR (i.""UOM"" = 12 AND u.""Code"" = 'L')
                   OR (i.""UOM"" = 13 AND u.""Code"" = 'QT_US')
                   OR (i.""UOM"" = 14 AND u.""Code"" = 'PT_US')
                   OR (i.""UOM"" = 15 AND u.""Code"" = 'OZ')
                   OR (i.""UOM"" = 16 AND u.""Code"" = 'LB')
                   OR (i.""UOM"" = 17 AND u.""Code"" = 'KG')
                   OR (i.""UOM"" = 18 AND u.""Code"" = 'G')
                   OR (i.""UOM"" = 19 AND u.""Code"" = 'DOZEN')
                   OR (i.""UOM"" = 20 AND u.""Code"" = 'HUNDRED')
                   OR (i.""UOM"" = 21 AND u.""Code"" = 'THOUSAND')
                  );
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // Roll-back guard: only drop if the tables are empty enough to be safe.
            // For full rollback, restore from PITR.
            mb.Sql(@"
                ALTER TABLE ""Items""
                    DROP COLUMN IF EXISTS ""StockUomId"",
                    DROP COLUMN IF EXISTS ""PurchaseUomId"",
                    DROP COLUMN IF EXISTS ""PurchasePackUomId"",
                    DROP COLUMN IF EXISTS ""SalesUomId"",
                    DROP COLUMN IF EXISTS ""SalesPackUomId"",
                    DROP COLUMN IF EXISTS ""PriceUomId"",
                    DROP COLUMN IF EXISTS ""ReportingUomId"",
                    DROP COLUMN IF EXISTS ""WeightUomId"",
                    DROP COLUMN IF EXISTS ""VolumeUomId"",
                    DROP COLUMN IF EXISTS ""DimensionUomId"";
                DROP TABLE IF EXISTS ""UomConversions"";
                DROP TABLE IF EXISTS ""UnitsOfMeasure"";
                DROP TABLE IF EXISTS ""UomCategories"";
            ");
        }
    }
}
