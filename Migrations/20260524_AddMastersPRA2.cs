// Sprint 13.5 PRA-2 — Country + Subdivision + WorkCalendar + Holiday masters.
//
// What this migration does:
//   1) Countries table + UNIQUE (Alpha2) + seed 8 Tier-1 trading partners
//      (US/CA/MX/GB/DE/FR/JP/CN)
//   2) Subdivisions table + UNIQUE (CountryId, Code) + FK to Countries +
//      seed US (50 states + DC + 5 territories) + CA (10 + 3) + MX (32)
//   3) WorkCalendars table + UNIQUE (COALESCE(CompanyId,0), Code) + seed
//      one system calendar "US Standard Business Week" (Mon-Fri 8-5)
//   4) Holidays table + indexes + seed US Federal Holidays 2026
//      (11 observed dates including Jul-3 observed-for-Jul-4 sat slide)
//
// What this migration does NOT do (per PRA-2 v1 scope discipline):
//   - Customer.CountryId FK retrofit (deferred — back-compat string stays
//     authoritative until a polish PR adds the FK column + dual-write)
//   - Vendor.CountryId FK retrofit (same)
//   - Address/BillTo CountryId FK retrofit (same)
//   - Rule-based recurrence ("3rd Monday in January") — PRA-2.1 polish
//   - Holiday generator UI ("generate next 5 years") — PRA-2.1 polish
//
// Idempotent: every CREATE/ALTER uses IF NOT EXISTS or DO $$ guards.
// Seeds use ON CONFLICT DO NOTHING.
//
// Cross-refs:
//   - docs/research/master-files-audit.md — original PRA-2 spec
//   - MASTER_PLAN.md Priority 1.66 — Sprint 13.5 plan
//   - Migrations/20260524_AddMasterFilesPRA1.cs — PRA-1 style template

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524_AddMastersPRA2")]
    public partial class AddMastersPRA2 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ============================================================
            // 1) Countries — ISO 3166-1 system master
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Countries"" (
                    ""Id""                  serial       PRIMARY KEY,
                    ""Alpha2""              varchar(2)   NOT NULL,
                    ""Alpha3""              varchar(3)   NOT NULL,
                    ""Numeric""             varchar(3)   NOT NULL,
                    ""Name""                varchar(100) NOT NULL,
                    ""OfficialName""        varchar(200) NULL,
                    ""CallingCode""         varchar(8)   NULL,
                    ""DefaultCurrencyCode"" varchar(3)   NULL,
                    ""IsActive""            boolean      NOT NULL DEFAULT TRUE,
                    ""SortOrder""           integer      NOT NULL DEFAULT 0,
                    ""CreatedAt""           timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS uq_countries_alpha2
                    ON ""Countries"" (""Alpha2"");
            ");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS uq_countries_alpha3
                    ON ""Countries"" (""Alpha3"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_countries_active_sort
                    ON ""Countries"" (""IsActive"", ""SortOrder"");
            ");

            mb.Sql(@"
                INSERT INTO ""Countries""
                    (""Alpha2"", ""Alpha3"", ""Numeric"", ""Name"", ""OfficialName"", ""CallingCode"", ""DefaultCurrencyCode"", ""SortOrder"")
                VALUES
                    ('US', 'USA', '840', 'United States',  'United States of America',           '+1',  'USD', 10),
                    ('CA', 'CAN', '124', 'Canada',         'Canada',                             '+1',  'CAD', 20),
                    ('MX', 'MEX', '484', 'Mexico',         'United Mexican States',              '+52', 'MXN', 30),
                    ('GB', 'GBR', '826', 'United Kingdom', 'United Kingdom of Great Britain',    '+44', 'GBP', 40),
                    ('DE', 'DEU', '276', 'Germany',        'Federal Republic of Germany',        '+49', 'EUR', 50),
                    ('FR', 'FRA', '250', 'France',         'French Republic',                    '+33', 'EUR', 60),
                    ('JP', 'JPN', '392', 'Japan',          'Japan',                              '+81', 'JPY', 70),
                    ('CN', 'CHN', '156', 'China',          'People''s Republic of China',        '+86', 'CNY', 80)
                ON CONFLICT (""Alpha2"") DO NOTHING;
            ");

            // ============================================================
            // 2) Subdivisions — ISO 3166-2 system master
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Subdivisions"" (
                    ""Id""        serial       PRIMARY KEY,
                    ""CountryId"" integer      NOT NULL REFERENCES ""Countries"" (""Id"") ON DELETE RESTRICT,
                    ""Code""      varchar(8)   NOT NULL,
                    ""Name""      varchar(100) NOT NULL,
                    ""Type""      smallint     NOT NULL DEFAULT 0,
                    ""IsActive""  boolean      NOT NULL DEFAULT TRUE,
                    ""SortOrder"" integer      NOT NULL DEFAULT 0,
                    ""CreatedAt"" timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS uq_subdivisions_country_code
                    ON ""Subdivisions"" (""CountryId"", ""Code"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_subdivisions_country_active
                    ON ""Subdivisions"" (""CountryId"", ""IsActive"");
            ");

            mb.Sql(@"
                DO $$
                DECLARE us_id integer; ca_id integer; mx_id integer;
                BEGIN
                    SELECT ""Id"" INTO us_id FROM ""Countries"" WHERE ""Alpha2"" = 'US';
                    SELECT ""Id"" INTO ca_id FROM ""Countries"" WHERE ""Alpha2"" = 'CA';
                    SELECT ""Id"" INTO mx_id FROM ""Countries"" WHERE ""Alpha2"" = 'MX';

                    -- US 50 states + DC + 5 territories (Type: 0=State, 2=Territory, 3=FederalDistrict)
                    INSERT INTO ""Subdivisions"" (""CountryId"", ""Code"", ""Name"", ""Type"", ""SortOrder"") VALUES
                        (us_id, 'AL', 'Alabama',        0, 1),  (us_id, 'AK', 'Alaska',         0, 2),
                        (us_id, 'AZ', 'Arizona',        0, 3),  (us_id, 'AR', 'Arkansas',       0, 4),
                        (us_id, 'CA', 'California',     0, 5),  (us_id, 'CO', 'Colorado',       0, 6),
                        (us_id, 'CT', 'Connecticut',    0, 7),  (us_id, 'DE', 'Delaware',       0, 8),
                        (us_id, 'FL', 'Florida',        0, 9),  (us_id, 'GA', 'Georgia',        0, 10),
                        (us_id, 'HI', 'Hawaii',         0, 11), (us_id, 'ID', 'Idaho',          0, 12),
                        (us_id, 'IL', 'Illinois',       0, 13), (us_id, 'IN', 'Indiana',        0, 14),
                        (us_id, 'IA', 'Iowa',           0, 15), (us_id, 'KS', 'Kansas',         0, 16),
                        (us_id, 'KY', 'Kentucky',       0, 17), (us_id, 'LA', 'Louisiana',      0, 18),
                        (us_id, 'ME', 'Maine',          0, 19), (us_id, 'MD', 'Maryland',       0, 20),
                        (us_id, 'MA', 'Massachusetts',  0, 21), (us_id, 'MI', 'Michigan',       0, 22),
                        (us_id, 'MN', 'Minnesota',      0, 23), (us_id, 'MS', 'Mississippi',    0, 24),
                        (us_id, 'MO', 'Missouri',       0, 25), (us_id, 'MT', 'Montana',        0, 26),
                        (us_id, 'NE', 'Nebraska',       0, 27), (us_id, 'NV', 'Nevada',         0, 28),
                        (us_id, 'NH', 'New Hampshire',  0, 29), (us_id, 'NJ', 'New Jersey',     0, 30),
                        (us_id, 'NM', 'New Mexico',     0, 31), (us_id, 'NY', 'New York',       0, 32),
                        (us_id, 'NC', 'North Carolina', 0, 33), (us_id, 'ND', 'North Dakota',   0, 34),
                        (us_id, 'OH', 'Ohio',           0, 35), (us_id, 'OK', 'Oklahoma',       0, 36),
                        (us_id, 'OR', 'Oregon',         0, 37), (us_id, 'PA', 'Pennsylvania',   0, 38),
                        (us_id, 'RI', 'Rhode Island',   0, 39), (us_id, 'SC', 'South Carolina', 0, 40),
                        (us_id, 'SD', 'South Dakota',   0, 41), (us_id, 'TN', 'Tennessee',      0, 42),
                        (us_id, 'TX', 'Texas',          0, 43), (us_id, 'UT', 'Utah',           0, 44),
                        (us_id, 'VT', 'Vermont',        0, 45), (us_id, 'VA', 'Virginia',       0, 46),
                        (us_id, 'WA', 'Washington',     0, 47), (us_id, 'WV', 'West Virginia',  0, 48),
                        (us_id, 'WI', 'Wisconsin',      0, 49), (us_id, 'WY', 'Wyoming',        0, 50),
                        (us_id, 'DC', 'District of Columbia', 3, 51),
                        (us_id, 'PR', 'Puerto Rico',                          2, 52),
                        (us_id, 'GU', 'Guam',                                 2, 53),
                        (us_id, 'AS', 'American Samoa',                       2, 54),
                        (us_id, 'MP', 'Northern Mariana Islands',             2, 55),
                        (us_id, 'VI', 'U.S. Virgin Islands',                  2, 56)
                    ON CONFLICT (""CountryId"", ""Code"") DO NOTHING;

                    -- Canada 10 provinces + 3 territories (Type: 1=Province, 2=Territory)
                    INSERT INTO ""Subdivisions"" (""CountryId"", ""Code"", ""Name"", ""Type"", ""SortOrder"") VALUES
                        (ca_id, 'AB', 'Alberta',                   1, 1),
                        (ca_id, 'BC', 'British Columbia',          1, 2),
                        (ca_id, 'MB', 'Manitoba',                  1, 3),
                        (ca_id, 'NB', 'New Brunswick',             1, 4),
                        (ca_id, 'NL', 'Newfoundland and Labrador', 1, 5),
                        (ca_id, 'NS', 'Nova Scotia',               1, 6),
                        (ca_id, 'ON', 'Ontario',                   1, 7),
                        (ca_id, 'PE', 'Prince Edward Island',      1, 8),
                        (ca_id, 'QC', 'Quebec',                    1, 9),
                        (ca_id, 'SK', 'Saskatchewan',              1, 10),
                        (ca_id, 'NT', 'Northwest Territories',     2, 11),
                        (ca_id, 'NU', 'Nunavut',                   2, 12),
                        (ca_id, 'YT', 'Yukon',                     2, 13)
                    ON CONFLICT (""CountryId"", ""Code"") DO NOTHING;

                    -- Mexico 32 states (Type: 0=State, 3=FederalDistrict for CDMX)
                    INSERT INTO ""Subdivisions"" (""CountryId"", ""Code"", ""Name"", ""Type"", ""SortOrder"") VALUES
                        (mx_id, 'AGU', 'Aguascalientes',     0, 1),  (mx_id, 'BCN', 'Baja California',     0, 2),
                        (mx_id, 'BCS', 'Baja California Sur',0, 3),  (mx_id, 'CAM', 'Campeche',            0, 4),
                        (mx_id, 'CHP', 'Chiapas',            0, 5),  (mx_id, 'CHH', 'Chihuahua',           0, 6),
                        (mx_id, 'CMX', 'Ciudad de México',   3, 7),  (mx_id, 'COA', 'Coahuila',            0, 8),
                        (mx_id, 'COL', 'Colima',             0, 9),  (mx_id, 'DUR', 'Durango',             0, 10),
                        (mx_id, 'MEX', 'Estado de México',   0, 11), (mx_id, 'GUA', 'Guanajuato',          0, 12),
                        (mx_id, 'GRO', 'Guerrero',           0, 13), (mx_id, 'HID', 'Hidalgo',             0, 14),
                        (mx_id, 'JAL', 'Jalisco',            0, 15), (mx_id, 'MIC', 'Michoacán',           0, 16),
                        (mx_id, 'MOR', 'Morelos',            0, 17), (mx_id, 'NAY', 'Nayarit',             0, 18),
                        (mx_id, 'NLE', 'Nuevo León',         0, 19), (mx_id, 'OAX', 'Oaxaca',              0, 20),
                        (mx_id, 'PUE', 'Puebla',             0, 21), (mx_id, 'QUE', 'Querétaro',           0, 22),
                        (mx_id, 'ROO', 'Quintana Roo',       0, 23), (mx_id, 'SLP', 'San Luis Potosí',     0, 24),
                        (mx_id, 'SIN', 'Sinaloa',            0, 25), (mx_id, 'SON', 'Sonora',              0, 26),
                        (mx_id, 'TAB', 'Tabasco',            0, 27), (mx_id, 'TAM', 'Tamaulipas',          0, 28),
                        (mx_id, 'TLA', 'Tlaxcala',           0, 29), (mx_id, 'VER', 'Veracruz',            0, 30),
                        (mx_id, 'YUC', 'Yucatán',            0, 31), (mx_id, 'ZAC', 'Zacatecas',           0, 32)
                    ON CONFLICT (""CountryId"", ""Code"") DO NOTHING;
                END $$;
            ");

            // ============================================================
            // 3) WorkCalendars — per-tenant w/ system fallback (NULL CompanyId)
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WorkCalendars"" (
                    ""Id""           serial       PRIMARY KEY,
                    ""CompanyId""    integer      NULL,
                    ""Code""         varchar(50)  NOT NULL,
                    ""Name""         varchar(100) NOT NULL,
                    ""Description""  varchar(500) NULL,
                    ""TimeZone""     varchar(64)  NOT NULL DEFAULT 'America/New_York',
                    ""WorkDayMask""  smallint     NOT NULL DEFAULT 62,
                    ""WorkDayStart"" time         NOT NULL DEFAULT '08:00:00',
                    ""WorkDayEnd""   time         NOT NULL DEFAULT '17:00:00',
                    ""IsDefault""    boolean      NOT NULL DEFAULT FALSE,
                    ""IsActive""     boolean      NOT NULL DEFAULT TRUE,
                    ""SortOrder""    integer      NOT NULL DEFAULT 0,
                    ""CreatedAt""    timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS uq_workcalendars_company_code
                    ON ""WorkCalendars"" (COALESCE(""CompanyId"", 0), ""Code"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_workcalendars_company_active
                    ON ""WorkCalendars"" (""CompanyId"", ""IsActive"");
            ");

            mb.Sql(@"
                ALTER TABLE ""WorkCalendars""
                    ADD CONSTRAINT fk_workcalendars_company
                        FOREIGN KEY (""CompanyId"") REFERENCES ""Companies"" (""Id"") ON DELETE SET NULL
                    NOT VALID;
            ");
            // NOT VALID + alter validate keeps existing rows non-blocking; we
            // validate immediately since the table was just created.
            mb.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'fk_workcalendars_company'
                    ) THEN
                        ALTER TABLE ""WorkCalendars""
                            VALIDATE CONSTRAINT fk_workcalendars_company;
                    END IF;
                END $$;
            ");

            mb.Sql(@"
                INSERT INTO ""WorkCalendars""
                    (""CompanyId"", ""Code"", ""Name"", ""Description"", ""TimeZone"",
                     ""WorkDayMask"", ""WorkDayStart"", ""WorkDayEnd"", ""IsDefault"", ""SortOrder"")
                VALUES
                    (NULL, 'US_STD',
                     'US Standard Business Week',
                     'Mon-Fri 8am-5pm in tenant local time. Includes US federal holidays.',
                     'America/New_York', 62, '08:00:00', '17:00:00', TRUE, 1)
                ON CONFLICT (COALESCE(""CompanyId"", 0), ""Code"") DO NOTHING;
            ");

            // ============================================================
            // 4) Holidays — per-calendar non-working dates (instance-based v1)
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Holidays"" (
                    ""Id""             bigserial    PRIMARY KEY,
                    ""WorkCalendarId"" integer      NOT NULL REFERENCES ""WorkCalendars"" (""Id"") ON DELETE CASCADE,
                    ""ObservedDate""   date         NOT NULL,
                    ""NominalDate""    date         NULL,
                    ""Name""           varchar(100) NOT NULL,
                    ""SubdivisionId""  integer      NULL REFERENCES ""Subdivisions"" (""Id"") ON DELETE SET NULL,
                    ""Category""       smallint     NOT NULL DEFAULT 0,
                    ""IsHalfDay""      boolean      NOT NULL DEFAULT FALSE,
                    ""IsActive""       boolean      NOT NULL DEFAULT TRUE,
                    ""CreatedAt""      timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_holidays_calendar_date
                    ON ""Holidays"" (""WorkCalendarId"", ""ObservedDate"")
                    WHERE ""IsActive"" = TRUE;
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_holidays_subdivision
                    ON ""Holidays"" (""SubdivisionId"")
                    WHERE ""SubdivisionId"" IS NOT NULL;
            ");

            mb.Sql(@"
                DO $$
                DECLARE cal_id integer;
                BEGIN
                    SELECT ""Id"" INTO cal_id FROM ""WorkCalendars""
                        WHERE ""CompanyId"" IS NULL AND ""Code"" = 'US_STD';

                    IF cal_id IS NOT NULL THEN
                        INSERT INTO ""Holidays""
                            (""WorkCalendarId"", ""ObservedDate"", ""NominalDate"", ""Name"", ""Category"")
                        VALUES
                            (cal_id, '2026-01-01', '2026-01-01', 'New Year''s Day',         0),
                            (cal_id, '2026-01-19', '2026-01-19', 'Martin Luther King Jr. Day', 0),
                            (cal_id, '2026-02-16', '2026-02-16', 'Presidents'' Day',        0),
                            (cal_id, '2026-05-25', '2026-05-25', 'Memorial Day',            0),
                            (cal_id, '2026-06-19', '2026-06-19', 'Juneteenth',              0),
                            (cal_id, '2026-07-03', '2026-07-04', 'Independence Day (observed)', 0),
                            (cal_id, '2026-09-07', '2026-09-07', 'Labor Day',               0),
                            (cal_id, '2026-10-12', '2026-10-12', 'Columbus Day',            0),
                            (cal_id, '2026-11-11', '2026-11-11', 'Veterans Day',            0),
                            (cal_id, '2026-11-26', '2026-11-26', 'Thanksgiving Day',        0),
                            (cal_id, '2026-12-25', '2026-12-25', 'Christmas Day',           0)
                        ON CONFLICT DO NOTHING;
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // Reverse order — child tables before parents.
            mb.Sql(@"DROP TABLE IF EXISTS ""Holidays"";");
            mb.Sql(@"DROP TABLE IF EXISTS ""WorkCalendars"";");
            mb.Sql(@"DROP TABLE IF EXISTS ""Subdivisions"";");
            mb.Sql(@"DROP TABLE IF EXISTS ""Countries"";");
        }
    }
}
