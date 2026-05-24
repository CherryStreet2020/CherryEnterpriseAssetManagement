// =============================================================================
// Sprint 13.5 PRA-8 — Employee + WageGroup + LaborRate + Department GL extension.
// Master Files Baseline cascade ship #6 of 10.
//
// THREE NEW TABLES:
//   - Employees      tenant-owned HR master (CompanyId NOT NULL)
//   - WageGroups     system templates + tenant overrides (CompanyId nullable)
//   - LaborRates     tenant-owned effective-dated rate matrix
//
// EXTENSIONS:
//   - Departments    additive nullable GL FK columns (DefaultLaborGlAccountId,
//                    DefaultOhGlAccountId, DefaultAbsorbedGlAccountId) +
//                    DefaultWageGroupId
//   - (Technician.EmployeeId FK deferred to cleanup PR — keeps PRA-8 tight)
//
// SEEDS — all CompanyId IS NULL system templates:
//   - 10 WageGroup templates (manufacturing-leaning baseline)
//   - 5 PostingProfile rows for labor transaction types
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md §6.6
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md
//
// IDEMPOTENT — CREATE TABLE IF NOT EXISTS + ALTER TABLE ADD COLUMN IF NOT
// EXISTS + INSERT ON CONFLICT DO NOTHING. No ALTER on populated columns.
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524210000_AddEmployeeWageGroupLaborRatePRA8")]
    public partial class AddEmployeeWageGroupLaborRatePRA8 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) WageGroups
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WageGroups"" (
                    ""Id""                              serial          PRIMARY KEY,
                    ""CompanyId""                       integer         NULL,
                    ""Code""                            varchar(32)     NOT NULL,
                    ""Name""                            varchar(200)    NOT NULL,
                    ""Description""                     varchar(500)    NULL,
                    ""GroupType""                       integer         NOT NULL DEFAULT 0,
                    ""IsExempt""                        boolean         NOT NULL DEFAULT FALSE,
                    ""StandardWeeklyHours""             integer         NOT NULL DEFAULT 40,
                    ""DefaultLaborGlAccountId""         integer         NULL,
                    ""DefaultOhGlAccountId""            integer         NULL,
                    ""DefaultAbsorbedGlAccountId""      integer         NULL,
                    ""IndicativeRatePerHour""           numeric(12,4)   NULL,
                    ""CurrencyId""                      integer         NULL,
                    ""IsActive""                        boolean         NOT NULL DEFAULT TRUE,
                    ""IsSystem""                        boolean         NOT NULL DEFAULT FALSE,
                    ""SortOrder""                       integer         NOT NULL DEFAULT 100,
                    ""CreatedAt""                       timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                       varchar(100)    NULL,
                    ""ModifiedAt""                      timestamptz     NULL,
                    ""ModifiedBy""                      varchar(100)    NULL,
                    CONSTRAINT ck_wage_groups_type CHECK (""GroupType"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_wage_groups_hours CHECK (""StandardWeeklyHours"" BETWEEN 0 AND 168)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_wage_groups_system_code
                    ON ""WageGroups"" (""Code"") WHERE ""CompanyId"" IS NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_wage_groups_company_code
                    ON ""WageGroups"" (""CompanyId"", ""Code"") WHERE ""CompanyId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_wage_groups_type
                    ON ""WageGroups"" (""GroupType"");
            ");

            // ================================================================
            // 2) Employees
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Employees"" (
                    ""Id""                              serial          PRIMARY KEY,
                    ""CompanyId""                       integer         NOT NULL,
                    ""EmployeeNumber""                  varchar(32)     NOT NULL,
                    ""FirstName""                       varchar(100)    NOT NULL,
                    ""LastName""                        varchar(100)    NOT NULL,
                    ""MiddleName""                      varchar(100)    NULL,
                    ""PreferredName""                   varchar(150)    NULL,
                    ""FullName""                        varchar(300)    NOT NULL,
                    ""WorkEmail""                       varchar(200)    NULL,
                    ""WorkPhone""                       varchar(40)     NULL,
                    ""JobTitle""                        varchar(150)    NOT NULL,
                    ""EmployeeType""                    integer         NOT NULL DEFAULT 0,
                    ""Status""                          integer         NOT NULL DEFAULT 0,
                    ""HireDate""                        timestamptz     NOT NULL DEFAULT NOW(),
                    ""TerminationDate""                 timestamptz     NULL,
                    ""DepartmentId""                    integer         NULL,
                    ""ManagerId""                       integer         NULL REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT,
                    ""SiteId""                          integer         NULL,
                    ""LocationId""                      integer         NULL,
                    ""DefaultWageGroupId""              integer         NULL REFERENCES ""WageGroups""(""Id"") ON DELETE SET NULL,
                    ""DefaultLaborGlAccountId""         integer         NULL,
                    ""DefaultCurrencyId""               integer         NULL,
                    ""IsProductionResource""            boolean         NOT NULL DEFAULT FALSE,
                    ""IsMaintenanceResource""           boolean         NOT NULL DEFAULT FALSE,
                    ""IsActive""                        boolean         NOT NULL DEFAULT TRUE,
                    ""Notes""                           varchar(2000)   NULL,
                    ""CreatedAt""                       timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                       varchar(100)    NULL,
                    ""ModifiedAt""                      timestamptz     NULL,
                    ""ModifiedBy""                      varchar(100)    NULL,
                    CONSTRAINT ck_employees_type CHECK (""EmployeeType"" BETWEEN 0 AND 99),
                    CONSTRAINT ck_employees_status CHECK (""Status"" BETWEEN 0 AND 99)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_employees_company_employeenumber
                    ON ""Employees"" (""CompanyId"", ""EmployeeNumber"");
                CREATE INDEX IF NOT EXISTS ix_employees_company_name
                    ON ""Employees"" (""CompanyId"", ""LastName"", ""FirstName"");
                CREATE INDEX IF NOT EXISTS ix_employees_department
                    ON ""Employees"" (""DepartmentId"") WHERE ""DepartmentId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_employees_manager
                    ON ""Employees"" (""ManagerId"") WHERE ""ManagerId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_employees_site
                    ON ""Employees"" (""SiteId"") WHERE ""SiteId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_employees_wagegroup
                    ON ""Employees"" (""DefaultWageGroupId"") WHERE ""DefaultWageGroupId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_employees_status
                    ON ""Employees"" (""Status"");
            ");

            // ================================================================
            // 3) LaborRates
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""LaborRateMasters"" (
                    ""Id""                              serial          PRIMARY KEY,
                    ""CompanyId""                       integer         NOT NULL,
                    ""EmployeeId""                      integer         NULL REFERENCES ""Employees""(""Id"") ON DELETE CASCADE,
                    ""WageGroupId""                     integer         NOT NULL REFERENCES ""WageGroups""(""Id"") ON DELETE RESTRICT,
                    ""BaseRatePerHour""                 numeric(14,4)   NOT NULL,
                    ""OvertimeRatePerHour""             numeric(14,4)   NULL,
                    ""DoubleTimeRatePerHour""           numeric(14,4)   NULL,
                    ""ShiftDifferentialPerHour""        numeric(14,4)   NULL,
                    ""CurrencyId""                      integer         NULL,
                    ""EffectiveFromUtc""                timestamptz     NOT NULL DEFAULT NOW(),
                    ""EffectiveToUtc""                  timestamptz     NULL,
                    ""SourceReference""                 varchar(100)    NULL,
                    ""ChangeReason""                    varchar(500)    NULL,
                    ""IsActive""                        boolean         NOT NULL DEFAULT TRUE,
                    ""CreatedAt""                       timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                       varchar(100)    NULL,
                    ""ModifiedAt""                      timestamptz     NULL,
                    ""ModifiedBy""                      varchar(100)    NULL,
                    CONSTRAINT ck_labor_rate_masters_dates CHECK (""EffectiveToUtc"" IS NULL OR ""EffectiveToUtc"" > ""EffectiveFromUtc"")
                );

                CREATE INDEX IF NOT EXISTS ix_labor_rate_masters_company_employee_effective
                    ON ""LaborRateMasters"" (""CompanyId"", ""EmployeeId"", ""EffectiveFromUtc"") WHERE ""EmployeeId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_labor_rate_masters_company_wagegroup_effective
                    ON ""LaborRateMasters"" (""CompanyId"", ""WageGroupId"", ""EffectiveFromUtc"") WHERE ""EmployeeId"" IS NULL;
                CREATE INDEX IF NOT EXISTS ix_labor_rate_masters_effective_to
                    ON ""LaborRateMasters"" (""EffectiveToUtc"") WHERE ""EffectiveToUtc"" IS NULL;
            ");

            // ================================================================
            // 4) Departments — additive GL FK columns + DefaultWageGroupId
            //    Per Lock 5: ALTER TABLE ADD COLUMN IF NOT EXISTS on populated
            //    table; nullable additions are SAFE (no backfill needed,
            //    no NOT NULL constraint added, no Replit autodiff destruction).
            // ================================================================
            mb.Sql(@"
                ALTER TABLE ""Departments"" ADD COLUMN IF NOT EXISTS ""DefaultLaborGlAccountId""    integer NULL;
                ALTER TABLE ""Departments"" ADD COLUMN IF NOT EXISTS ""DefaultOhGlAccountId""       integer NULL;
                ALTER TABLE ""Departments"" ADD COLUMN IF NOT EXISTS ""DefaultAbsorbedGlAccountId"" integer NULL;
                ALTER TABLE ""Departments"" ADD COLUMN IF NOT EXISTS ""DefaultWageGroupId""         integer NULL;
            ");

            // ================================================================
            // 5) Seed system-template WageGroups (10 rows)
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""WageGroups"" (""CompanyId"", ""Code"", ""Name"", ""Description"", ""GroupType"", ""IsExempt"", ""StandardWeeklyHours"", ""IndicativeRatePerHour"", ""IsSystem"", ""SortOrder"")
                VALUES
                    (NULL, 'HR-EXEMPT',     'Salaried Exempt',          'FLSA-exempt salaried — no overtime accrual',                            1, TRUE,  40, NULL,    TRUE,  10),
                    (NULL, 'HR-NONEXEMPT',  'Salaried Non-Exempt',      'FLSA non-exempt salaried — overtime applies above 40hr',                1, FALSE, 40, NULL,    TRUE,  20),
                    (NULL, 'OP-LVL1',       'Operator Level I',         'Entry-level production operator (hourly)',                              0, FALSE, 40, 22.00,   TRUE,  30),
                    (NULL, 'OP-LVL2',       'Operator Level II',        'Mid-level production operator (hourly)',                                0, FALSE, 40, 28.00,   TRUE,  40),
                    (NULL, 'OP-LVL3',       'Operator Level III',       'Senior production operator / multi-machine (hourly)',                   0, FALSE, 40, 36.00,   TRUE,  50),
                    (NULL, 'SETUP',         'Setup Operator',           'Machine setup / changeover specialist — premium hourly band',           0, FALSE, 40, 42.00,   TRUE,  60),
                    (NULL, 'LEAD',          'Lead / Foreman',           'Floor lead / shift foreman — supervises operators',                     0, FALSE, 40, 48.00,   TRUE,  70),
                    (NULL, 'TECH-MAINT',    'Maintenance Technician',   'EAM-side maintenance technician — wired via Technician satellite',      0, FALSE, 40, 52.00,   TRUE,  80),
                    (NULL, 'INSP-QC',       'Quality Inspector',        'AS9102 / ISO 9001 FAI + dimensional inspection',                        0, FALSE, 40, 45.00,   TRUE,  90),
                    (NULL, 'CONTRACT',      '1099 Contractor',          'External contractor — no payroll relationship, no FLSA',                4, TRUE,   0, NULL,    TRUE, 100)
                ON CONFLICT DO NOTHING;
            ");

            // ================================================================
            // 6) Seed system-template PostingProfile labor rows.
            //    Add labor InventoryTransactionType rows that wire the future
            //    LaborEntry → JE flow. The new transaction-type integers
            //    (LaborApply, LaborAbsorb, OvertimePremium) are conceptual —
            //    the existing InventoryTransactionType enum doesn't have those
            //    slots yet (they belong in a future PRA-8.1 enum extension
            //    PR). Until then, we wire labor postings via the existing
            //    Production-related transaction types as overrides keyed by
            //    a tenant-set ItemGroup mapping.
            //
            //    For PRA-8 ship: seed 5 PostingProfile rows targeting the
            //    existing IssueToProduction (1) and ProductionComplete (10)
            //    transaction types with the new ProductionLabor + ProductionOH
            //    GL category values from PRA-5a. Tenant onboarding flow can
            //    refine per-Department.
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
                    -- WIP labor apply (ProductionLaborExpense=520, falls into WIP via PostingProfile resolution)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='WIP'),  1, NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=520), NULL,                                          200, 'PRA-8: Production labor apply — Dr ProductionLaborExpense (Cr accrued payroll wired by tenant)',  TRUE, 200),
                    -- ProductionOverhead apply (when MES system applies OH on production complete)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='WIP'),  10, NULL, (SELECT ""Id"" FROM gl WHERE ""Category""=530), NULL,                                          200, 'PRA-8: Production OH apply on ProductionComplete — Dr ProductionOverhead',                        TRUE, 210),
                    -- OverheadApplied credit (variance side)
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='WIP'),  10, NULL, NULL,                                            (SELECT ""Id"" FROM gl WHERE ""Category""=590), 200, 'PRA-8: OverheadApplied credit (subtractive) on production complete',                              TRUE, 220),
                    -- OverheadSpendingVariance
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='WIP'),  14, NULL, NULL,                                            (SELECT ""Id"" FROM gl WHERE ""Category""=591), 200, 'PRA-8: OverheadSpendingVariance on revaluation',                                                  TRUE, 230),
                    -- OverheadVolumeVariance
                    (NULL, (SELECT ""Id"" FROM ig WHERE ""Code""='WIP'),  14, NULL, NULL,                                            (SELECT ""Id"" FROM gl WHERE ""Category""=592), 100, 'PRA-8: OverheadVolumeVariance on revaluation',                                                    TRUE, 240)
                ON CONFLICT DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"
                ALTER TABLE ""Departments"" DROP COLUMN IF EXISTS ""DefaultWageGroupId"";
                ALTER TABLE ""Departments"" DROP COLUMN IF EXISTS ""DefaultAbsorbedGlAccountId"";
                ALTER TABLE ""Departments"" DROP COLUMN IF EXISTS ""DefaultOhGlAccountId"";
                ALTER TABLE ""Departments"" DROP COLUMN IF EXISTS ""DefaultLaborGlAccountId"";
                DROP TABLE IF EXISTS ""LaborRateMasters"";
                DROP TABLE IF EXISTS ""Employees"";
                DROP TABLE IF EXISTS ""WageGroups"";
            ");
        }
    }
}
