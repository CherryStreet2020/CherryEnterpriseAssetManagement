using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 13.5 PR #1.5 — CustomerProject field expansion + ProjectAmendments
    //   + Companies governance flag + CHECK constraints + status-regression
    //   trigger + cockpit at-risk partial index.
    //
    // ADDITIVE-ONLY. Zero breakage. Safe to apply against production.
    // Existing CustomerProjects rows (currently zero) need no backfill —
    // every new column is nullable except ExportControl which has a safe
    // default of 0 (None). Companies.ProjectExportControlRequired defaults
    // false so the existing tenant policy stays "no friction on commercial
    // project creation".
    //
    // SQL source-of-truth: .ship/drafts/sprint-13.5-pr1.5-fields.sql
    //
    // What this migration creates (mirrors the SQL file, in order):
    //   1. 14 new nullable columns on CustomerProjects (RiskScore /
    //      RiskTone / AiSummaryText / AiSummaryModel / AiSummaryGeneratedAt /
    //      AiRefreshLockedUntil / EstimatedTotalCost / PercentComplete /
    //      ProjectedEndDate / LastEvmRollupAt / CustomerPoNumber /
    //      ContractType / QualityProgram), plus 1 NOT NULL DEFAULT 0
    //      (ExportControl).
    //   2. New ProjectAmendments table — append-only change-order log.
    //      CustomerProjects.ContractValue stays the immutable baseline;
    //      EffectiveContractValue is a service-layer SUM of approved
    //      amendments. Per research §5.3 (Acumatica / Oracle / SAP / AIA
    //      G701 universal pattern).
    //   3. Companies.ProjectExportControlRequired bool (Dean's "1c" call,
    //      2026-05-23): allows aero/def tenants to force explicit
    //      ExportControl on project create.
    //   4. Defensive CHECK constraints on every enum + range column —
    //      backstops the service layer against bad raw SQL writes.
    //   5. Postgres trigger fn_block_amendment_status_regression — blocks
    //      illegal Status transitions on ProjectAmendments (e.g. Approved
    //      → Draft) so the append-only event log discipline survives
    //      regardless of service-layer correctness.
    //   6. Two cockpit-sort partial indexes: ix_customerprojects_riskscore
    //      (any risk-sorted cockpit) and ix_customerprojects_atrisk_queue
    //      (the focused Amber/Red queue on Program Cockpit landing).
    //
    // What this migration does NOT do:
    //   - Add any UI. PR #4 (Customer Project Cockpit) lights up the
    //     first user-visible surface for these fields.
    //   - Add any service layer. PR #2 ICustomerProjectService implements
    //     RiskScore computation, EVM rollup, amendment workflow.
    //   - Add Quotations FK constraint (Quotations entity doesn't exist;
    //     SourceQuotationId is a nullable integer for now).
    //   - Add ProjectEvmSnapshot time-series table (deferred to Sprint 14
    //     per research §3.5).
    //   - Add FAI workflow tables (next-up: PR #1.75 ships immediately
    //     after this PR per Dean's "3c" call, 2026-05-23).
    //   - Add cross-cutting RecommendationOutcomes table (deferred to
    //     Sprint 14 per strategic-absorption Item 4).
    //
    // Idempotent: every CREATE / ALTER / CONSTRAINT addition uses
    // IF NOT EXISTS or a DO $$ existence-check block, so the migration is
    // safe to re-apply against partial state.
    //
    // Cross-refs:
    //   - .ship/drafts/sprint-13.5-pr1.5-fields.sql       — source SQL
    //   - docs/research/customerproject-field-set.md      — design memo
    //   - Migrations/20260522_AddCustomerProjectFoundation.cs — PR #1 baseline
    //   - Migrations/20260522_AddChainOfCustodyGraph.cs   — style template
    [DbContext(typeof(AppDbContext))]
    [Migration("20260523_AddCustomerProjectFieldExpansion")]
    public partial class AddCustomerProjectFieldExpansion : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ====================================================================
            // 1) AI INFUSION COLUMNS ON CustomerProjects (research §2.2)
            // ====================================================================
            mb.Sql(@"
                ALTER TABLE ""CustomerProjects""
                    ADD COLUMN IF NOT EXISTS ""RiskScore""             smallint     NULL,
                    ADD COLUMN IF NOT EXISTS ""RiskTone""              smallint     NULL,
                    ADD COLUMN IF NOT EXISTS ""AiSummaryText""         text         NULL,
                    ADD COLUMN IF NOT EXISTS ""AiSummaryModel""        varchar(64)  NULL,
                    ADD COLUMN IF NOT EXISTS ""AiSummaryGeneratedAt""  timestamptz  NULL,
                    ADD COLUMN IF NOT EXISTS ""AiRefreshLockedUntil""  timestamptz  NULL;
            ");

            // ====================================================================
            // 2) EVM / POC COLUMNS ON CustomerProjects (research §3.2)
            // ====================================================================
            mb.Sql(@"
                ALTER TABLE ""CustomerProjects""
                    ADD COLUMN IF NOT EXISTS ""EstimatedTotalCost"" numeric(18,4) NULL,
                    ADD COLUMN IF NOT EXISTS ""PercentComplete""    numeric(5,2)  NULL,
                    ADD COLUMN IF NOT EXISTS ""ProjectedEndDate""   date          NULL,
                    ADD COLUMN IF NOT EXISTS ""LastEvmRollupAt""    timestamptz   NULL;
            ");

            // ====================================================================
            // 3) AERO / DEF QUALITY COLUMNS ON CustomerProjects (research §4.2)
            // ====================================================================
            mb.Sql(@"
                ALTER TABLE ""CustomerProjects""
                    ADD COLUMN IF NOT EXISTS ""CustomerPoNumber"" varchar(100) NULL,
                    ADD COLUMN IF NOT EXISTS ""ContractType""     smallint     NULL,
                    ADD COLUMN IF NOT EXISTS ""QualityProgram""   smallint     NULL,
                    ADD COLUMN IF NOT EXISTS ""ExportControl""    smallint     NOT NULL DEFAULT 0;
            ");

            // ====================================================================
            // 4) ProjectAmendments — append-only change-order log (research §5.2)
            // ====================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProjectAmendments"" (
                    ""Id""                   bigserial     PRIMARY KEY,
                    ""CustomerProjectId""    integer       NOT NULL,
                    ""AmendmentNumber""      integer       NOT NULL,
                    ""EffectiveDate""        date          NOT NULL,
                    ""ChangeType""           smallint      NOT NULL,
                    ""Reason""               varchar(2000) NULL,
                    ""ScopeNarrative""       text          NULL,
                    ""ValueDelta""           numeric(18,4) NOT NULL DEFAULT 0,
                    ""TargetStartDateDelta"" integer       NULL,
                    ""TargetEndDateDelta""   integer       NULL,
                    ""SourceQuotationId""    integer       NULL,
                    ""CustomerReference""    varchar(100)  NULL,
                    ""Status""               smallint      NOT NULL DEFAULT 0,
                    ""ApprovedById""         integer       NULL,
                    ""ApprovedByName""       varchar(100)  NULL,
                    ""ApprovedAt""           timestamptz   NULL,
                    ""CustomerSignatureAt""  timestamptz   NULL,
                    ""Notes""                text          NULL,
                    ""CreatedAt""            timestamptz   NOT NULL DEFAULT now(),
                    ""CreatedBy""            varchar(100)  NULL,
                    ""ModifiedAt""           timestamptz   NULL,
                    ""ModifiedBy""           varchar(100)  NULL,
                    CONSTRAINT fk_projectamendments_customerproject
                        FOREIGN KEY (""CustomerProjectId"")
                        REFERENCES ""CustomerProjects"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT fk_projectamendments_approvedby
                        FOREIGN KEY (""ApprovedById"")
                        REFERENCES ""Users"" (""Id"") ON DELETE SET NULL
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_projectamendments_project_number
                    ON ""ProjectAmendments"" (""CustomerProjectId"", ""AmendmentNumber"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_projectamendments_project_status_date
                    ON ""ProjectAmendments"" (""CustomerProjectId"", ""Status"", ""EffectiveDate"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_projectamendments_status
                    ON ""ProjectAmendments"" (""Status"")
                    WHERE ""Status"" IN (0, 1);
            ");

            // ====================================================================
            // 5) Cockpit-sort partial indexes on CustomerProjects.RiskScore
            // ====================================================================
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_customerprojects_riskscore
                    ON ""CustomerProjects"" (""RiskScore"" DESC NULLS LAST)
                    WHERE ""RiskScore"" IS NOT NULL;
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_customerprojects_atrisk_queue
                    ON ""CustomerProjects"" (""RiskScore"" DESC NULLS LAST)
                    WHERE ""RiskTone"" IN (1, 2);
            ");

            // ====================================================================
            // 6) Companies.ProjectExportControlRequired — tenant governance flag
            //    Dean's choice 1c (2026-05-23): commercial tenants default false
            //    for friction-free creation; aero/def tenants set true to force
            //    explicit ExportControl on every project. Service-layer enforces
            //    in ICustomerProjectService.CreateAsync (PR #2).
            // ====================================================================
            mb.Sql(@"
                ALTER TABLE ""Companies""
                    ADD COLUMN IF NOT EXISTS ""ProjectExportControlRequired"" boolean NOT NULL DEFAULT false;
            ");

            // ====================================================================
            // 7) Defensive CHECK constraints — DB backstop for service-layer writes
            // ====================================================================
            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customerprojects_riskscore_range') THEN
                        ALTER TABLE ""CustomerProjects""
                            ADD CONSTRAINT ck_customerprojects_riskscore_range
                            CHECK (""RiskScore"" IS NULL OR (""RiskScore"" >= 0 AND ""RiskScore"" <= 100));
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customerprojects_risktone_range') THEN
                        ALTER TABLE ""CustomerProjects""
                            ADD CONSTRAINT ck_customerprojects_risktone_range
                            CHECK (""RiskTone"" IS NULL OR ""RiskTone"" IN (0, 1, 2));
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customerprojects_percentcomplete_range') THEN
                        ALTER TABLE ""CustomerProjects""
                            ADD CONSTRAINT ck_customerprojects_percentcomplete_range
                            CHECK (""PercentComplete"" IS NULL
                                   OR (""PercentComplete"" >= 0 AND ""PercentComplete"" <= 100));
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customerprojects_estimatedtotalcost_nonneg') THEN
                        ALTER TABLE ""CustomerProjects""
                            ADD CONSTRAINT ck_customerprojects_estimatedtotalcost_nonneg
                            CHECK (""EstimatedTotalCost"" IS NULL OR ""EstimatedTotalCost"" >= 0);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customerprojects_contracttype_range') THEN
                        ALTER TABLE ""CustomerProjects""
                            ADD CONSTRAINT ck_customerprojects_contracttype_range
                            CHECK (""ContractType"" IS NULL OR ""ContractType"" BETWEEN 0 AND 5);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customerprojects_qualityprogram_range') THEN
                        ALTER TABLE ""CustomerProjects""
                            ADD CONSTRAINT ck_customerprojects_qualityprogram_range
                            CHECK (""QualityProgram"" IS NULL OR ""QualityProgram"" BETWEEN 0 AND 5);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_customerprojects_exportcontrol_range') THEN
                        ALTER TABLE ""CustomerProjects""
                            ADD CONSTRAINT ck_customerprojects_exportcontrol_range
                            CHECK (""ExportControl"" BETWEEN 0 AND 3);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_projectamendments_changetype_range') THEN
                        ALTER TABLE ""ProjectAmendments""
                            ADD CONSTRAINT ck_projectamendments_changetype_range
                            CHECK (""ChangeType"" BETWEEN 0 AND 3);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_projectamendments_status_range') THEN
                        ALTER TABLE ""ProjectAmendments""
                            ADD CONSTRAINT ck_projectamendments_status_range
                            CHECK (""Status"" BETWEEN 0 AND 5);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                                   WHERE conname = 'ck_projectamendments_amendmentnumber_pos') THEN
                        ALTER TABLE ""ProjectAmendments""
                            ADD CONSTRAINT ck_projectamendments_amendmentnumber_pos
                            CHECK (""AmendmentNumber"" >= 1);
                    END IF;
                END $$;
            ");

            // ====================================================================
            // 8) ProjectAmendments append-only Status regression guard
            //    Trigger blocks illegal Status transitions: once Approved (2)
            //    or Rejected (3), cannot regress to Draft/Submitted. Once
            //    Voided (5), terminal. Audit-trail discipline backstop.
            // ====================================================================
            mb.Sql(@"
                CREATE OR REPLACE FUNCTION fn_block_amendment_status_regression()
                RETURNS trigger AS $$
                BEGIN
                    IF OLD.""Status"" = 2 AND NEW.""Status"" IN (0, 1, 3, 4) THEN
                        RAISE EXCEPTION
                            'ProjectAmendment % cannot regress from Approved to status %. '
                            'Use Status=5 (Voided) to reverse an approved amendment.',
                            OLD.""Id"", NEW.""Status""
                            USING ERRCODE = 'check_violation';
                    END IF;
                    IF OLD.""Status"" = 3 AND NEW.""Status"" IN (0, 1, 2, 4) THEN
                        RAISE EXCEPTION
                            'ProjectAmendment % cannot regress from Rejected to status %. '
                            'Create a new amendment instead.',
                            OLD.""Id"", NEW.""Status""
                            USING ERRCODE = 'check_violation';
                    END IF;
                    IF OLD.""Status"" = 5 AND NEW.""Status"" <> 5 THEN
                        RAISE EXCEPTION
                            'ProjectAmendment % is Voided and cannot be reactivated. '
                            'Create a new amendment instead.',
                            OLD.""Id""
                            USING ERRCODE = 'check_violation';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");
            mb.Sql(@"DROP TRIGGER IF EXISTS trg_block_amendment_status_regression ON ""ProjectAmendments"";");
            mb.Sql(@"
                CREATE TRIGGER trg_block_amendment_status_regression
                BEFORE UPDATE ON ""ProjectAmendments""
                FOR EACH ROW
                EXECUTE FUNCTION fn_block_amendment_status_regression();
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // DROP order matters — trigger before function before table.
            mb.Sql(@"DROP TRIGGER  IF EXISTS trg_block_amendment_status_regression ON ""ProjectAmendments"";");
            mb.Sql(@"DROP FUNCTION IF EXISTS fn_block_amendment_status_regression();");

            mb.Sql(@"
                DO $$
                BEGIN
                    PERFORM 1; -- placeholder so the block parses even if all DROPs no-op
                END $$;
            ");
            mb.Sql(@"ALTER TABLE ""ProjectAmendments"" DROP CONSTRAINT IF EXISTS ck_projectamendments_amendmentnumber_pos;");
            mb.Sql(@"ALTER TABLE ""ProjectAmendments"" DROP CONSTRAINT IF EXISTS ck_projectamendments_status_range;");
            mb.Sql(@"ALTER TABLE ""ProjectAmendments"" DROP CONSTRAINT IF EXISTS ck_projectamendments_changetype_range;");
            mb.Sql(@"ALTER TABLE ""CustomerProjects"" DROP CONSTRAINT IF EXISTS ck_customerprojects_exportcontrol_range;");
            mb.Sql(@"ALTER TABLE ""CustomerProjects"" DROP CONSTRAINT IF EXISTS ck_customerprojects_qualityprogram_range;");
            mb.Sql(@"ALTER TABLE ""CustomerProjects"" DROP CONSTRAINT IF EXISTS ck_customerprojects_contracttype_range;");
            mb.Sql(@"ALTER TABLE ""CustomerProjects"" DROP CONSTRAINT IF EXISTS ck_customerprojects_estimatedtotalcost_nonneg;");
            mb.Sql(@"ALTER TABLE ""CustomerProjects"" DROP CONSTRAINT IF EXISTS ck_customerprojects_percentcomplete_range;");
            mb.Sql(@"ALTER TABLE ""CustomerProjects"" DROP CONSTRAINT IF EXISTS ck_customerprojects_risktone_range;");
            mb.Sql(@"ALTER TABLE ""CustomerProjects"" DROP CONSTRAINT IF EXISTS ck_customerprojects_riskscore_range;");

            mb.Sql(@"ALTER TABLE ""Companies"" DROP COLUMN IF EXISTS ""ProjectExportControlRequired"";");

            mb.Sql(@"DROP INDEX IF EXISTS ix_customerprojects_atrisk_queue;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_customerprojects_riskscore;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_projectamendments_status;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_projectamendments_project_status_date;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_projectamendments_project_number;");
            mb.Sql(@"DROP TABLE IF EXISTS ""ProjectAmendments"" CASCADE;");

            mb.Sql(@"
                ALTER TABLE ""CustomerProjects""
                    DROP COLUMN IF EXISTS ""ExportControl"",
                    DROP COLUMN IF EXISTS ""QualityProgram"",
                    DROP COLUMN IF EXISTS ""ContractType"",
                    DROP COLUMN IF EXISTS ""CustomerPoNumber"",
                    DROP COLUMN IF EXISTS ""LastEvmRollupAt"",
                    DROP COLUMN IF EXISTS ""ProjectedEndDate"",
                    DROP COLUMN IF EXISTS ""PercentComplete"",
                    DROP COLUMN IF EXISTS ""EstimatedTotalCost"",
                    DROP COLUMN IF EXISTS ""AiRefreshLockedUntil"",
                    DROP COLUMN IF EXISTS ""AiSummaryGeneratedAt"",
                    DROP COLUMN IF EXISTS ""AiSummaryModel"",
                    DROP COLUMN IF EXISTS ""AiSummaryText"",
                    DROP COLUMN IF EXISTS ""RiskTone"",
                    DROP COLUMN IF EXISTS ""RiskScore"";
            ");
        }
    }
}
