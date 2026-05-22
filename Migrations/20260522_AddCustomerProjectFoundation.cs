using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 13.5 PR #1 / ADR-026 — Customer-Project foundation (additive).
    //
    // ADDITIVE-ONLY. Zero breakage. Safe to apply against production.
    // Job-shop customers see no functional change — they never need
    // a CustomerProject row and ProductionOrder.CustomerProjectId stays
    // NULL for every row that does not opt in.
    //
    // Driven by:
    //   - ABS Thursday demo (precision machining shop, most work ETO)
    //   - EVS June 3 pitch ("why is job X late" — fundamentally an
    //     ETO concern with multi-job Project convergence)
    //   - 11-ERP industry survey converging on the same pattern:
    //     SAP S/4HANA PS, Oracle Project Mfg, D365 Project Ops, IFS Cloud,
    //     Epicor Kinetic, Acumatica, SYSPRO, Infor LN, JobBOSS², Global Shop,
    //     Made2Manage. See research_project_job_hierarchy_patterns.md.
    //
    // What this migration creates:
    //   1. Programs                — portfolio bucket (empty in v1, reserved
    //                                for v2 EVM / DCAA portfolio rollup).
    //   2. CustomerProjects        — customer-facing project entity (distinct
    //                                from CipProjects which is internal capex).
    //   3. ProjectMembers          — M:N for joint-venture / pass-through.
    //   4. ProjectPhases           — flat-but-tree-capable WBS.
    //   5. ProductionOrders.CustomerProjectId    — nullable FK
    //   6. ProductionOrders.ProjectPhaseId       — nullable FK
    //   7. ProductionOrders.ProjectPostingMode   — nullable int enum:
    //                                              0=FinishedItem (default),
    //                                              1=Consumed
    //   8. Partial indexes on the two new ProductionOrder FKs (WHERE col
    //      IS NOT NULL) so job-shop-mode (no-project) lookups stay cheap.
    //   9. CHECK constraint: when CustomerProjectId IS NOT NULL then
    //      ProjectPostingMode IS NOT NULL — the service layer enforces this
    //      on every link operation; the DB constraint backstops it.
    //
    // What this migration does NOT do:
    //   - Add any UI. PR #4 (the ProductionOrder UI shell) lights up the
    //     first user-visible surface.
    //   - Add any service layer. PR #2 (ICustomerProjectService) and
    //     PR #3 (IProductionOrderService) wire those in.
    //   - Add ChainOfCustody node/edge types for ProductionOrder. PR #3
    //     lands the ChainNode + chain emit when the ProductionOrder
    //     service is built.
    //   - Add Subcontract chain edges (SENT_OUT_FOR / RETURNED_FROM).
    //     PR #6 lands those (and the V2 webhook chain enrichment).
    //   - Add DB-level RLS. Tenant scoping is enforced at the service
    //     layer via CompanyId — matches the CipProject / ProductionOrder
    //     pattern (only ChainNodes / ChainEdges / Embeddings use DB-level
    //     RLS today; the domain tables do not).
    //
    // Idempotent: every CREATE TABLE / INDEX / ALTER uses IF NOT EXISTS,
    // so the migration is safe to re-apply against partial state.
    //
    // Reference: research_project_job_hierarchy_patterns.md §4 (schema),
    //   §5 (anti-patterns), §6 (seven-modes contract — formalized in
    //   ADR-026 shipping in PR #8). project_abs_customer_profile.md
    //   for the customer-driven re-sequencing.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260522_AddCustomerProjectFoundation")]
    public partial class AddCustomerProjectFoundation : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ============================================================
            // 1) Programs — portfolio bucket above CustomerProject.
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Programs"" (
                    ""Id""          serial        PRIMARY KEY,
                    ""CompanyId""   integer       NULL,
                    ""Code""        varchar(64)   NOT NULL,
                    ""Name""        varchar(200)  NOT NULL,
                    ""Description"" varchar(2000) NULL,
                    ""Status""      integer       NOT NULL DEFAULT 0,
                    ""CreatedAt""   timestamptz   NOT NULL DEFAULT now(),
                    ""CreatedBy""   varchar(100)  NULL,
                    ""ModifiedAt""  timestamptz   NULL,
                    ""ModifiedBy""  varchar(100)  NULL,
                    CONSTRAINT fk_programs_company
                        FOREIGN KEY (""CompanyId"") REFERENCES ""Companies"" (""Id"")
                        ON DELETE SET NULL
                );
            ");

            // UNIQUE (CompanyId, Code) — NULL-safe via COALESCE for
            // org-wide programs that pre-date a tenant assignment.
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_programs_company_code
                ON ""Programs"" (COALESCE(""CompanyId"", 0), ""Code"");
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_programs_status
                ON ""Programs"" (""Status"");
            ");

            // ============================================================
            // 2) CustomerProjects — the customer-facing project.
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""CustomerProjects"" (
                    ""Id""                    serial        PRIMARY KEY,
                    ""CompanyId""             integer       NULL,
                    ""ProgramId""             integer       NULL,
                    ""PrimaryCustomerId""     integer       NULL,
                    ""Code""                  varchar(64)   NOT NULL,
                    ""Name""                  varchar(200)  NOT NULL,
                    ""Description""           varchar(2000) NULL,
                    ""Status""                integer       NOT NULL DEFAULT 1,
                    ""Mode""                  integer       NOT NULL DEFAULT 0,
                    ""CostingMode""           integer       NOT NULL DEFAULT 0,
                    ""RevenueMode""           integer       NOT NULL DEFAULT 0,
                    ""ContractValue""         numeric(18,4) NULL,
                    ""Currency""              varchar(3)    NOT NULL DEFAULT 'CAD',
                    ""TargetStartDate""       date          NULL,
                    ""TargetEndDate""         date          NULL,
                    ""ClosedAt""              timestamptz   NULL,
                    ""ProjectManagerName""    varchar(100)  NULL,
                    ""ProjectManagerId""      integer       NULL,
                    ""CreatedAt""             timestamptz   NOT NULL DEFAULT now(),
                    ""CreatedBy""             varchar(100)  NULL,
                    ""ModifiedAt""            timestamptz   NULL,
                    ""ModifiedBy""            varchar(100)  NULL,
                    CONSTRAINT fk_customerprojects_company
                        FOREIGN KEY (""CompanyId"") REFERENCES ""Companies"" (""Id"")
                        ON DELETE SET NULL,
                    CONSTRAINT fk_customerprojects_program
                        FOREIGN KEY (""ProgramId"") REFERENCES ""Programs"" (""Id"")
                        ON DELETE SET NULL,
                    CONSTRAINT fk_customerprojects_primarycustomer
                        FOREIGN KEY (""PrimaryCustomerId"") REFERENCES ""Customers"" (""Id"")
                        ON DELETE SET NULL,
                    CONSTRAINT fk_customerprojects_projectmanager
                        FOREIGN KEY (""ProjectManagerId"") REFERENCES ""ProjectManagers"" (""Id"")
                        ON DELETE SET NULL
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_customerprojects_company_code
                ON ""CustomerProjects"" (COALESCE(""CompanyId"", 0), ""Code"");
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_customerprojects_status
                ON ""CustomerProjects"" (""Status"");
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_customerprojects_mode
                ON ""CustomerProjects"" (""Mode"");
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_customerprojects_primarycustomer
                ON ""CustomerProjects"" (""PrimaryCustomerId"")
                WHERE ""PrimaryCustomerId"" IS NOT NULL;
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_customerprojects_program
                ON ""CustomerProjects"" (""ProgramId"")
                WHERE ""ProgramId"" IS NOT NULL;
            ");

            // ============================================================
            // 3) ProjectMembers — M:N junction (joint-venture / pass-through).
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProjectMembers"" (
                    ""Id""                serial        PRIMARY KEY,
                    ""CustomerProjectId"" integer       NOT NULL,
                    ""CustomerId""        integer       NOT NULL,
                    ""Role""              integer       NOT NULL DEFAULT 0,
                    ""SharePct""          numeric(7,4)  NULL,
                    ""CreatedAt""         timestamptz   NOT NULL DEFAULT now(),
                    CONSTRAINT fk_projectmembers_project
                        FOREIGN KEY (""CustomerProjectId"") REFERENCES ""CustomerProjects"" (""Id"")
                        ON DELETE CASCADE,
                    CONSTRAINT fk_projectmembers_customer
                        FOREIGN KEY (""CustomerId"") REFERENCES ""Customers"" (""Id"")
                        ON DELETE RESTRICT
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_projectmembers_project_customer_role
                ON ""ProjectMembers"" (""CustomerProjectId"", ""CustomerId"", ""Role"");
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_projectmembers_customer
                ON ""ProjectMembers"" (""CustomerId"");
            ");

            // ============================================================
            // 4) ProjectPhases — flat-but-tree-capable WBS.
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProjectPhases"" (
                    ""Id""                serial        PRIMARY KEY,
                    ""CustomerProjectId"" integer       NOT NULL,
                    ""ParentPhaseId""     integer       NULL,
                    ""Code""              varchar(64)   NOT NULL,
                    ""Name""              varchar(200)  NOT NULL,
                    ""Description""       varchar(2000) NULL,
                    ""SortOrder""         integer       NOT NULL DEFAULT 0,
                    ""CreatedAt""         timestamptz   NOT NULL DEFAULT now(),
                    ""CreatedBy""         varchar(100)  NULL,
                    CONSTRAINT fk_projectphases_project
                        FOREIGN KEY (""CustomerProjectId"") REFERENCES ""CustomerProjects"" (""Id"")
                        ON DELETE CASCADE,
                    CONSTRAINT fk_projectphases_parent
                        FOREIGN KEY (""ParentPhaseId"") REFERENCES ""ProjectPhases"" (""Id"")
                        ON DELETE SET NULL
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_projectphases_project_code
                ON ""ProjectPhases"" (""CustomerProjectId"", ""Code"");
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_projectphases_parent
                ON ""ProjectPhases"" (""ParentPhaseId"")
                WHERE ""ParentPhaseId"" IS NOT NULL;
            ");

            // ============================================================
            // 5-7) ProductionOrders extension — three nullable columns.
            //      All nullable so pure-job-shop ProductionOrder rows remain
            //      unaffected. Service layer (PR #2 ICustomerProjectService)
            //      will refuse to link a job without setting a posting mode.
            // ============================================================
            mb.Sql(@"
                ALTER TABLE ""ProductionOrders""
                ADD COLUMN IF NOT EXISTS ""CustomerProjectId""  integer NULL,
                ADD COLUMN IF NOT EXISTS ""ProjectPhaseId""     integer NULL,
                ADD COLUMN IF NOT EXISTS ""ProjectPostingMode"" integer NULL;
            ");

            // FK additions — guarded by IF NOT EXISTS via DO block (Postgres
            // does not support IF NOT EXISTS on ADD CONSTRAINT directly).
            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'fk_productionorders_customerproject'
                    ) THEN
                        ALTER TABLE ""ProductionOrders""
                        ADD CONSTRAINT fk_productionorders_customerproject
                        FOREIGN KEY (""CustomerProjectId"") REFERENCES ""CustomerProjects"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'fk_productionorders_projectphase'
                    ) THEN
                        ALTER TABLE ""ProductionOrders""
                        ADD CONSTRAINT fk_productionorders_projectphase
                        FOREIGN KEY (""ProjectPhaseId"") REFERENCES ""ProjectPhases"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            // ============================================================
            // 8) Partial indexes — job-shop-mode (NULL) rows are 99%+
            //    of production-order traffic. Partial indexes keep those
            //    lookups completely uncluttered.
            // ============================================================
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_productionorders_customerproject
                ON ""ProductionOrders"" (""CustomerProjectId"")
                WHERE ""CustomerProjectId"" IS NOT NULL;
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_productionorders_projectphase
                ON ""ProductionOrders"" (""ProjectPhaseId"")
                WHERE ""ProjectPhaseId"" IS NOT NULL;
            ");

            // ============================================================
            // 9) CHECK constraint — when a job is linked to a project,
            //    the posting mode MUST be set. The service layer enforces
            //    this on every link; this DB constraint backstops it so
            //    direct SQL never produces an incoherent row.
            // ============================================================
            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'ck_productionorders_project_postingmode'
                    ) THEN
                        ALTER TABLE ""ProductionOrders""
                        ADD CONSTRAINT ck_productionorders_project_postingmode
                        CHECK (
                            ""CustomerProjectId"" IS NULL
                            OR ""ProjectPostingMode"" IS NOT NULL
                        );
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // Reverse the migration. Defensive — production has zero rows
            // at the time this ships, so dropping the columns is loss-free.
            // The DROP TABLE order respects FK dependencies.
            mb.Sql(@"
                ALTER TABLE ""ProductionOrders""
                DROP CONSTRAINT IF EXISTS ck_productionorders_project_postingmode;
            ");
            mb.Sql(@"
                ALTER TABLE ""ProductionOrders""
                DROP CONSTRAINT IF EXISTS fk_productionorders_customerproject;
            ");
            mb.Sql(@"
                ALTER TABLE ""ProductionOrders""
                DROP CONSTRAINT IF EXISTS fk_productionorders_projectphase;
            ");
            mb.Sql(@"DROP INDEX IF EXISTS ix_productionorders_customerproject;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_productionorders_projectphase;");
            mb.Sql(@"
                ALTER TABLE ""ProductionOrders""
                DROP COLUMN IF EXISTS ""CustomerProjectId"",
                DROP COLUMN IF EXISTS ""ProjectPhaseId"",
                DROP COLUMN IF EXISTS ""ProjectPostingMode"";
            ");
            mb.Sql(@"DROP TABLE IF EXISTS ""ProjectPhases"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""ProjectMembers"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""CustomerProjects"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""Programs"" CASCADE;");
        }
    }
}
