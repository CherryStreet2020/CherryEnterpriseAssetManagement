// =============================================================================
// Sprint 13.5 PRA-10 — TaxRateMaster (effective-dated tax rate matrix).
// Master Files Baseline cascade ship #8 of 10.
//
// Closes the rate-side gap. PRA-6 shipped the SHAPE (TaxAuthority +
// TaxCodeMaster); PRA-10 ships the actual rate values at points in time
// and place. AvaTax / Vertex / SAP-FI-style resolution.
//
// ONE NEW TABLE:
//   - TaxRateMasters    effective-dated rates (CompanyId nullable for system
//                       templates + tenant-specific overrides)
//
// SEEDS — all CompanyId IS NULL system templates, 8 base rates:
//   - US-CA-SALES-7.25  California state base sales tax, eff 2017-01-01
//   - US-NY-SALES-4     New York state base sales tax, eff 2005-06-01
//   - CA-GST-5          Canada federal GST, eff 2008-01-01
//   - CA-ON-HST-13      Ontario HST (federal+provincial), eff 2010-07-01
//   - EU-VAT-DE-19      Germany VAT standard, eff 2007-01-01
//   - EU-VAT-DE-7       Germany VAT reduced (food/books), eff 2007-01-01
//   - EU-VAT-FR-20      France VAT standard, eff 2014-01-01
//   - EU-VAT-GB-20      UK VAT standard, eff 2011-01-04 (post-Brexit still 20%)
//
// All seed rates wire to the PRA-6 TaxCodeMaster + TaxAuthority rows via
// subselect against existing system records.
//
// IDEMPOTENT — CREATE TABLE IF NOT EXISTS + INSERT WHERE NOT EXISTS.
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md §6.8
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md
//   - PRA-6 (`7bde5e7`/#316) — TaxAuthority + TaxCodeMaster substrate
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524230000_AddTaxRateMasterPRA10")]
    public partial class AddTaxRateMasterPRA10 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) TaxRateMasters
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""TaxRateMasters"" (
                    ""Id""                          serial          PRIMARY KEY,
                    ""CompanyId""                   integer         NULL,
                    ""Code""                        varchar(64)     NOT NULL,
                    ""Name""                        varchar(200)    NOT NULL,
                    ""Description""                 varchar(500)    NULL,
                    ""TaxCodeMasterId""             integer         NOT NULL REFERENCES ""TaxCodeMasters""(""Id"") ON DELETE RESTRICT,
                    ""TaxAuthorityId""              integer         NULL REFERENCES ""TaxAuthorities""(""Id"") ON DELETE SET NULL,
                    ""CountryCode""                 varchar(2)      NOT NULL DEFAULT 'US',
                    ""SubdivisionCode""             varchar(8)      NULL,
                    ""PostalCodePrefix""            varchar(16)     NULL,
                    ""JurisdictionLevel""           integer         NOT NULL DEFAULT 1,
                    ""RateType""                    integer         NOT NULL DEFAULT 0,
                    ""Rate""                        numeric(7,6)    NOT NULL,
                    ""MinThresholdAmount""          numeric(18,4)   NULL,
                    ""MaxThresholdAmount""          numeric(18,4)   NULL,
                    ""IsCompounded""                boolean         NOT NULL DEFAULT FALSE,
                    ""AppliesToItemGroupId""        integer         NULL REFERENCES ""ItemGroups""(""Id"") ON DELETE SET NULL,
                    ""AppliesToProductClass""       varchar(64)     NULL,
                    ""EffectiveFromUtc""            timestamptz     NOT NULL DEFAULT NOW(),
                    ""EffectiveToUtc""              timestamptz     NULL,
                    ""IsActive""                    boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""                    boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""                   integer         NOT NULL DEFAULT 100,
                    ""Notes""                       varchar(500)    NULL,
                    ""CreatedAt""                   timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                   varchar(100)    NULL,
                    ""ModifiedAt""                  timestamptz     NULL,
                    ""ModifiedBy""                  varchar(100)    NULL,
                    CONSTRAINT ck_tax_rate_masters_jurisdiction_level CHECK (""JurisdictionLevel"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_tax_rate_masters_rate_type CHECK (""RateType"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_tax_rate_masters_rate_range CHECK (""Rate"" BETWEEN 0 AND 1),
                    CONSTRAINT ck_tax_rate_masters_thresholds CHECK (""MaxThresholdAmount"" IS NULL OR ""MinThresholdAmount"" IS NULL OR ""MaxThresholdAmount"" >= ""MinThresholdAmount""),
                    CONSTRAINT ck_tax_rate_masters_dates CHECK (""EffectiveToUtc"" IS NULL OR ""EffectiveToUtc"" > ""EffectiveFromUtc"")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_tax_rate_masters_system_code_effective
                    ON ""TaxRateMasters"" (""Code"", ""EffectiveFromUtc"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_tax_rate_masters_company_code_effective
                    ON ""TaxRateMasters"" (""CompanyId"", ""Code"", ""EffectiveFromUtc"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_tax_rate_masters_taxcode
                    ON ""TaxRateMasters"" (""TaxCodeMasterId"");
                CREATE INDEX IF NOT EXISTS ix_tax_rate_masters_jurisdiction_effective
                    ON ""TaxRateMasters"" (""CountryCode"", ""SubdivisionCode"", ""EffectiveFromUtc"");
                CREATE INDEX IF NOT EXISTS ix_tax_rate_masters_effective_to
                    ON ""TaxRateMasters"" (""EffectiveToUtc"") WHERE ""EffectiveToUtc"" IS NULL;
                CREATE INDEX IF NOT EXISTS ix_tax_rate_masters_itemgroup
                    ON ""TaxRateMasters"" (""AppliesToItemGroupId"") WHERE ""AppliesToItemGroupId"" IS NOT NULL;
            ");

            // ================================================================
            // 2) Seed 8 system-template tax rates.
            //
            // Subselects against PRA-6 TaxCodeMasters + TaxAuthorities. Tax
            // codes were seeded as US-CA-SALES / US-NY-SALES / CA-GST /
            // CA-ON-HST / EU-VAT-STANDARD / EU-VAT-REDUCED etc.
            // ================================================================
            mb.Sql(@"
                WITH tc AS (
                    SELECT ""Id"", ""Code"" FROM ""TaxCodeMasters"" WHERE ""CompanyId"" IS NULL
                ),
                ta AS (
                    SELECT ""Id"", ""Code"" FROM ""TaxAuthorities"" WHERE ""CompanyId"" IS NULL
                )
                INSERT INTO ""TaxRateMasters"" (
                    ""CompanyId"", ""Code"", ""Name"", ""Description"",
                    ""TaxCodeMasterId"", ""TaxAuthorityId"",
                    ""CountryCode"", ""SubdivisionCode"", ""JurisdictionLevel"",
                    ""RateType"", ""Rate"", ""EffectiveFromUtc"",
                    ""IsSystem"", ""SortOrder""
                )
                SELECT *
                FROM (VALUES
                    (NULL::int, 'US-CA-SALES-7.25', 'California State Sales Tax — Base 7.25%',
                        'California state base sales tax — effective from 2017-01-01. Local district taxes layer on top (county / city / special districts).',
                        (SELECT ""Id"" FROM tc WHERE ""Code""='US-CA-SALES'),
                        (SELECT ""Id"" FROM ta WHERE ""Code""='US-CA-CDTFA'),
                        'US', 'US-CA', 1,
                        0, 0.072500::numeric, '2017-01-01 00:00:00+00'::timestamptz,
                        TRUE, 10),

                    (NULL::int, 'US-NY-SALES-4', 'New York State Sales Tax — Base 4%',
                        'New York state base sales tax — effective from 2005-06-01. Local rates (NYC, county) layer on top.',
                        (SELECT ""Id"" FROM tc WHERE ""Code""='US-NY-SALES'),
                        (SELECT ""Id"" FROM ta WHERE ""Code""='US-NY-DTF'),
                        'US', 'US-NY', 1,
                        0, 0.040000::numeric, '2005-06-01 00:00:00+00'::timestamptz,
                        TRUE, 20),

                    (NULL::int, 'CA-GST-5', 'Canada Federal GST 5%',
                        'Canadian federal Goods and Services Tax — effective from 2008-01-01 (rate change from 6% to 5%).',
                        (SELECT ""Id"" FROM tc WHERE ""Code""='CA-GST'),
                        (SELECT ""Id"" FROM ta WHERE ""Code""='CA-CRA'),
                        'CA', NULL, 0,
                        0, 0.050000::numeric, '2008-01-01 00:00:00+00'::timestamptz,
                        TRUE, 30),

                    (NULL::int, 'CA-ON-HST-13', 'Ontario HST 13%',
                        'Ontario Harmonized Sales Tax (5% federal + 8% provincial) — effective from 2010-07-01.',
                        (SELECT ""Id"" FROM tc WHERE ""Code""='CA-ON-HST'),
                        (SELECT ""Id"" FROM ta WHERE ""Code""='CA-ON-MOF'),
                        'CA', 'CA-ON', 2,
                        0, 0.130000::numeric, '2010-07-01 00:00:00+00'::timestamptz,
                        TRUE, 40),

                    (NULL::int, 'EU-VAT-DE-19', 'Germany VAT Standard 19%',
                        'Germany VAT standard rate — effective from 2007-01-01 (rate change from 16% to 19%).',
                        (SELECT ""Id"" FROM tc WHERE ""Code""='EU-VAT-STANDARD'),
                        (SELECT ""Id"" FROM ta WHERE ""Code""='EU-VAT'),
                        'DE', NULL, 0,
                        0, 0.190000::numeric, '2007-01-01 00:00:00+00'::timestamptz,
                        TRUE, 50),

                    (NULL::int, 'EU-VAT-DE-7', 'Germany VAT Reduced 7%',
                        'Germany VAT reduced rate (food, books, hotels) — effective from 2007-01-01.',
                        (SELECT ""Id"" FROM tc WHERE ""Code""='EU-VAT-REDUCED'),
                        (SELECT ""Id"" FROM ta WHERE ""Code""='EU-VAT'),
                        'DE', NULL, 0,
                        1, 0.070000::numeric, '2007-01-01 00:00:00+00'::timestamptz,
                        TRUE, 60),

                    (NULL::int, 'EU-VAT-FR-20', 'France VAT Standard 20%',
                        'France VAT standard rate — effective from 2014-01-01.',
                        (SELECT ""Id"" FROM tc WHERE ""Code""='EU-VAT-STANDARD'),
                        (SELECT ""Id"" FROM ta WHERE ""Code""='EU-VAT'),
                        'FR', NULL, 0,
                        0, 0.200000::numeric, '2014-01-01 00:00:00+00'::timestamptz,
                        TRUE, 70),

                    (NULL::int, 'EU-VAT-GB-20', 'UK VAT Standard 20%',
                        'United Kingdom VAT standard rate — effective from 2011-01-04. Stayed at 20% post-Brexit.',
                        (SELECT ""Id"" FROM tc WHERE ""Code""='EU-VAT-STANDARD'),
                        (SELECT ""Id"" FROM ta WHERE ""Code""='EU-VAT'),
                        'GB', NULL, 0,
                        0, 0.200000::numeric, '2011-01-04 00:00:00+00'::timestamptz,
                        TRUE, 80)
                ) AS v(
                    ""CompanyId"", ""Code"", ""Name"", ""Description"",
                    ""TaxCodeMasterId"", ""TaxAuthorityId"",
                    ""CountryCode"", ""SubdivisionCode"", ""JurisdictionLevel"",
                    ""RateType"", ""Rate"", ""EffectiveFromUtc"",
                    ""IsSystem"", ""SortOrder""
                )
                WHERE v.""TaxCodeMasterId"" IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM ""TaxRateMasters""
                    WHERE ""CompanyId"" IS NULL AND ""Code"" = v.""Code""
                      AND ""EffectiveFromUtc"" = v.""EffectiveFromUtc""
                );
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"
                DROP TABLE IF EXISTS ""TaxRateMasters"";
            ");
        }
    }
}
