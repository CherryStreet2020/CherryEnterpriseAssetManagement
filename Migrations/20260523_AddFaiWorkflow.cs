using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 13.5 PR #1.75 — AS9102 First Article Inspection workflow.
    //
    // ADDITIVE-ONLY. Zero breakage. Safe to apply against production.
    //
    // SQL source-of-truth (with comments): research_fai_workflow_schema.md
    //
    // What this migration creates:
    //   1. FaiReports               — AS9102 Form 1 header + lifecycle
    //   2. FaiCharacteristics       — AS9102 Form 3 per-balloon dim row
    //   3. FaiProductAccountability — AS9102 Form 2 mat/proc/test row
    //   4. Attachments +3 nullable FK cols (FaiReportId / FaiCharacteristicId /
    //      FaiProductAccountabilityId) — reuse pattern per research §5
    //   5. CHECK constraints on every enum + range column
    //   6. Partial indexes on hot cockpit paths
    //   7. Status-regression trigger on FaiReports (mirrors PR #1.5
    //      ProjectAmendments append-only discipline)
    //
    // What this migration does NOT do:
    //   - Add ChainOfCustody NodeType / EdgeType strings (string constants
    //     in C#, added at service-layer in PR #2)
    //   - Add UI (PR #4+)
    //   - Add IFaiService (PR #2)
    //   - Auto-compute denormalized counts (service-layer concern)
    //
    // Idempotent: every CREATE / ALTER uses IF NOT EXISTS or DO $$ guards.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260523_AddFaiWorkflow")]
    public partial class AddFaiWorkflow : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 1) FaiReports
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""FaiReports"" (
                    ""Id""                       bigserial     PRIMARY KEY,
                    ""CompanyId""                integer       NOT NULL,
                    ""TenantId""                 integer       NULL,
                    ""FaiNumber""                varchar(50)   NOT NULL,
                    ""Revision""                 smallint      NOT NULL DEFAULT 1,
                    ""Type""                     smallint      NOT NULL DEFAULT 0,
                    ""PartType""                 smallint      NOT NULL DEFAULT 0,
                    ""Reason""                   smallint      NOT NULL DEFAULT 0,
                    ""ReasonText""               text          NULL,
                    ""ItemId""                   integer       NOT NULL,
                    ""ItemRevisionId""           integer       NULL,
                    ""CustomerProjectId""        integer       NULL,
                    ""CustomerId""               integer       NULL,
                    ""ProductionOrderId""        integer       NULL,
                    ""StockReceiptId""           integer       NULL,
                    ""PurchaseOrderId""          integer       NULL,
                    ""BaselineFaiReportId""      bigint        NULL,
                    ""PartNumberSnapshot""       varchar(100)  NOT NULL,
                    ""PartNameSnapshot""         varchar(200)  NULL,
                    ""DrawingNumberSnapshot""    varchar(100)  NULL,
                    ""DrawingRevSnapshot""       varchar(20)   NULL,
                    ""SerialNumberSnapshot""     varchar(100)  NULL,
                    ""LotNumberSnapshot""        varchar(100)  NULL,
                    ""HeatNumberSnapshot""       varchar(100)  NULL,
                    ""CustomerPoSnapshot""       varchar(100)  NULL,
                    ""OrganizationName""         varchar(200)  NULL,
                    ""SupplierName""             varchar(200)  NULL,
                    ""ManufacturingProcessRef""  varchar(200)  NULL,
                    ""Status""                   smallint      NOT NULL DEFAULT 0,
                    ""FirstArticleProducedAt""   date          NULL,
                    ""InspectionStartedAt""      timestamptz   NULL,
                    ""InspectionCompletedAt""    timestamptz   NULL,
                    ""SubmittedAt""              timestamptz   NULL,
                    ""SubmittedById""            integer       NULL,
                    ""ApprovedAt""               timestamptz   NULL,
                    ""ApprovedById""             integer       NULL,
                    ""ApprovedByName""           varchar(100)  NULL,
                    ""ExpiresAt""                date          NULL,
                    ""CharacteristicCount""      integer       NOT NULL DEFAULT 0,
                    ""NonConformCount""          integer       NOT NULL DEFAULT 0,
                    ""WaivedCount""              integer       NOT NULL DEFAULT 0,
                    ""AiSummaryText""            text          NULL,
                    ""AiSummaryModel""           varchar(64)   NULL,
                    ""AiSummaryGeneratedAt""     timestamptz   NULL,
                    ""AiRefreshLockedUntil""     timestamptz   NULL,
                    ""AiRiskScore""              smallint      NULL,
                    ""AiRiskTone""               smallint      NULL,
                    ""CreatedAt""                timestamptz   NOT NULL DEFAULT now(),
                    ""CreatedBy""                varchar(100)  NULL,
                    ""ModifiedAt""               timestamptz   NULL,
                    ""ModifiedBy""               varchar(100)  NULL,
                    CONSTRAINT fk_faireports_company           FOREIGN KEY (""CompanyId"")           REFERENCES ""Companies""        (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT fk_faireports_tenant            FOREIGN KEY (""TenantId"")            REFERENCES ""Tenants""          (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faireports_item              FOREIGN KEY (""ItemId"")              REFERENCES ""Items""            (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT fk_faireports_customerproject   FOREIGN KEY (""CustomerProjectId"")   REFERENCES ""CustomerProjects"" (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faireports_customer          FOREIGN KEY (""CustomerId"")          REFERENCES ""Customers""        (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faireports_productionorder   FOREIGN KEY (""ProductionOrderId"")   REFERENCES ""ProductionOrders"" (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faireports_stockreceipt      FOREIGN KEY (""StockReceiptId"")      REFERENCES ""StockReceipts""    (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faireports_purchaseorder     FOREIGN KEY (""PurchaseOrderId"")     REFERENCES ""PurchaseOrders""   (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faireports_baseline          FOREIGN KEY (""BaselineFaiReportId"") REFERENCES ""FaiReports""       (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faireports_submittedby       FOREIGN KEY (""SubmittedById"")       REFERENCES ""Users""            (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faireports_approvedby        FOREIGN KEY (""ApprovedById"")        REFERENCES ""Users""            (""Id"") ON DELETE SET NULL
                );
            ");

            mb.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ix_faireports_company_fainumber ON ""FaiReports"" (""CompanyId"", ""FaiNumber"");");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faireports_status            ON ""FaiReports"" (""Status"");");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faireports_item              ON ""FaiReports"" (""ItemId"");");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faireports_customerproject   ON ""FaiReports"" (""CustomerProjectId"") WHERE ""CustomerProjectId"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faireports_productionorder   ON ""FaiReports"" (""ProductionOrderId"") WHERE ""ProductionOrderId"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faireports_baseline          ON ""FaiReports"" (""BaselineFaiReportId"") WHERE ""BaselineFaiReportId"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faireports_open_queue        ON ""FaiReports"" (""Status"", ""InspectionStartedAt"") WHERE ""Status"" IN (0, 1, 2);");

            // ================================================================
            // 2) FaiCharacteristics
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""FaiCharacteristics"" (
                    ""Id""                          bigserial     PRIMARY KEY,
                    ""FaiReportId""                 bigint        NOT NULL,
                    ""BalloonNumber""               varchar(20)   NOT NULL,
                    ""DrawingZone""                 varchar(20)   NULL,
                    ""DrawingPageNumber""           smallint      NULL,
                    ""CharacteristicDescription""   varchar(500)  NOT NULL,
                    ""MeasurementType""             smallint      NOT NULL DEFAULT 0,
                    ""NominalValue""                numeric(18,6) NULL,
                    ""UpperToleranceValue""         numeric(18,6) NULL,
                    ""LowerToleranceValue""         numeric(18,6) NULL,
                    ""UnitOfMeasure""               varchar(20)   NULL,
                    ""RequirementText""             varchar(500)  NULL,
                    ""ToleranceText""               varchar(200)  NULL,
                    ""ActualResult""                numeric(18,6) NULL,
                    ""ActualText""                  varchar(500)  NULL,
                    ""Conformance""                 smallint      NOT NULL DEFAULT 0,
                    ""InspectorId""                 integer       NULL,
                    ""InspectorName""               varchar(100)  NULL,
                    ""InspectionDate""              timestamptz   NULL,
                    ""InstrumentUsed""              varchar(200)  NULL,
                    ""NonConformanceNotes""         text          NULL,
                    ""MrbDispositionId""            integer       NULL,
                    ""CreatedAt""                   timestamptz   NOT NULL DEFAULT now(),
                    ""CreatedBy""                   varchar(100)  NULL,
                    ""ModifiedAt""                  timestamptz   NULL,
                    ""ModifiedBy""                  varchar(100)  NULL,
                    CONSTRAINT fk_faicharacteristics_faireport     FOREIGN KEY (""FaiReportId"")      REFERENCES ""FaiReports""      (""Id"") ON DELETE CASCADE,
                    CONSTRAINT fk_faicharacteristics_inspector     FOREIGN KEY (""InspectorId"")      REFERENCES ""Users""           (""Id"") ON DELETE SET NULL,
                    CONSTRAINT fk_faicharacteristics_mrb           FOREIGN KEY (""MrbDispositionId"") REFERENCES ""MrbDispositions"" (""Id"") ON DELETE SET NULL
                );
            ");

            mb.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ix_faicharacteristics_report_balloon ON ""FaiCharacteristics"" (""FaiReportId"", ""BalloonNumber"");");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faicharacteristics_conformance    ON ""FaiCharacteristics"" (""Conformance"");");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faicharacteristics_mrb            ON ""FaiCharacteristics"" (""MrbDispositionId"") WHERE ""MrbDispositionId"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faicharacteristics_nonconform     ON ""FaiCharacteristics"" (""FaiReportId"", ""Conformance"") WHERE ""Conformance"" = 1;");

            // ================================================================
            // 3) FaiProductAccountability (Form 2 — one wide table)
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""FaiProductAccountability"" (
                    ""Id""                   bigserial     PRIMARY KEY,
                    ""FaiReportId""          bigint        NOT NULL,
                    ""EntryType""            smallint      NOT NULL DEFAULT 0,
                    ""Description""          varchar(500)  NOT NULL,
                    ""SpecReference""        varchar(100)  NULL,
                    ""CertificateNumber""    varchar(100)  NULL,
                    ""HeatNumber""           varchar(100)  NULL,
                    ""LotNumber""            varchar(100)  NULL,
                    ""SupplierName""         varchar(200)  NULL,
                    ""VendorId""             integer       NULL,
                    ""TestResult""           varchar(500)  NULL,
                    ""Conformance""          smallint      NULL,
                    ""MaterialMasterId""     integer       NULL,
                    ""StockReceiptId""       integer       NULL,
                    ""Notes""                text          NULL,
                    ""CreatedAt""            timestamptz   NOT NULL DEFAULT now(),
                    ""CreatedBy""            varchar(100)  NULL,
                    ""ModifiedAt""           timestamptz   NULL,
                    ""ModifiedBy""           varchar(100)  NULL,
                    CONSTRAINT fk_faiproductaccountability_faireport FOREIGN KEY (""FaiReportId"") REFERENCES ""FaiReports"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT fk_faiproductaccountability_vendor    FOREIGN KEY (""VendorId"")    REFERENCES ""Vendors""    (""Id"") ON DELETE SET NULL
                );
            ");

            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faiproductaccountability_report_type ON ""FaiProductAccountability"" (""FaiReportId"", ""EntryType"");");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_faiproductaccountability_heatnumber  ON ""FaiProductAccountability"" (""HeatNumber"") WHERE ""HeatNumber"" IS NOT NULL;");

            // ================================================================
            // 4) Attachments — 3 new nullable FK cols (reuse, no FaiAttachment)
            // ================================================================
            mb.Sql(@"
                ALTER TABLE ""Attachments""
                    ADD COLUMN IF NOT EXISTS ""FaiReportId""                bigint NULL,
                    ADD COLUMN IF NOT EXISTS ""FaiCharacteristicId""        bigint NULL,
                    ADD COLUMN IF NOT EXISTS ""FaiProductAccountabilityId"" bigint NULL;
            ");

            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_attachments_faireport') THEN
                        ALTER TABLE ""Attachments""
                            ADD CONSTRAINT fk_attachments_faireport
                            FOREIGN KEY (""FaiReportId"") REFERENCES ""FaiReports"" (""Id"") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_attachments_faicharacteristic') THEN
                        ALTER TABLE ""Attachments""
                            ADD CONSTRAINT fk_attachments_faicharacteristic
                            FOREIGN KEY (""FaiCharacteristicId"") REFERENCES ""FaiCharacteristics"" (""Id"") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_attachments_faiproductaccountability') THEN
                        ALTER TABLE ""Attachments""
                            ADD CONSTRAINT fk_attachments_faiproductaccountability
                            FOREIGN KEY (""FaiProductAccountabilityId"") REFERENCES ""FaiProductAccountability"" (""Id"") ON DELETE CASCADE;
                    END IF;
                END $$;
            ");

            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_attachments_faireport             ON ""Attachments"" (""FaiReportId"")                WHERE ""FaiReportId"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_attachments_faicharacteristic     ON ""Attachments"" (""FaiCharacteristicId"")        WHERE ""FaiCharacteristicId"" IS NOT NULL;");
            mb.Sql(@"CREATE INDEX IF NOT EXISTS ix_attachments_faiproductaccountability ON ""Attachments"" (""FaiProductAccountabilityId"") WHERE ""FaiProductAccountabilityId"" IS NOT NULL;");

            // ================================================================
            // 5) CHECK constraints (DB backstop)
            // ================================================================
            mb.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faireports_type_range') THEN
                        ALTER TABLE ""FaiReports"" ADD CONSTRAINT ck_faireports_type_range CHECK (""Type"" BETWEEN 0 AND 2);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faireports_parttype_range') THEN
                        ALTER TABLE ""FaiReports"" ADD CONSTRAINT ck_faireports_parttype_range CHECK (""PartType"" BETWEEN 0 AND 1);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faireports_reason_range') THEN
                        ALTER TABLE ""FaiReports"" ADD CONSTRAINT ck_faireports_reason_range CHECK (""Reason"" BETWEEN 0 AND 7);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faireports_status_range') THEN
                        ALTER TABLE ""FaiReports"" ADD CONSTRAINT ck_faireports_status_range CHECK (""Status"" BETWEEN 0 AND 6);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faireports_airiskscore_range') THEN
                        ALTER TABLE ""FaiReports"" ADD CONSTRAINT ck_faireports_airiskscore_range CHECK (""AiRiskScore"" IS NULL OR (""AiRiskScore"" >= 0 AND ""AiRiskScore"" <= 100));
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faireports_airisktone_range') THEN
                        ALTER TABLE ""FaiReports"" ADD CONSTRAINT ck_faireports_airisktone_range CHECK (""AiRiskTone"" IS NULL OR ""AiRiskTone"" IN (0, 1, 2));
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faireports_counts_nonneg') THEN
                        ALTER TABLE ""FaiReports"" ADD CONSTRAINT ck_faireports_counts_nonneg
                            CHECK (""CharacteristicCount"" >= 0 AND ""NonConformCount"" >= 0 AND ""WaivedCount"" >= 0);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faicharacteristics_measurementtype_range') THEN
                        ALTER TABLE ""FaiCharacteristics"" ADD CONSTRAINT ck_faicharacteristics_measurementtype_range CHECK (""MeasurementType"" BETWEEN 0 AND 5);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faicharacteristics_conformance_range') THEN
                        ALTER TABLE ""FaiCharacteristics"" ADD CONSTRAINT ck_faicharacteristics_conformance_range CHECK (""Conformance"" BETWEEN 0 AND 4);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faiproductaccountability_entrytype_range') THEN
                        ALTER TABLE ""FaiProductAccountability"" ADD CONSTRAINT ck_faiproductaccountability_entrytype_range CHECK (""EntryType"" BETWEEN 0 AND 3);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_faiproductaccountability_conformance_range') THEN
                        ALTER TABLE ""FaiProductAccountability"" ADD CONSTRAINT ck_faiproductaccountability_conformance_range CHECK (""Conformance"" IS NULL OR ""Conformance"" BETWEEN 0 AND 4);
                    END IF;
                END $$;
            ");

            // ================================================================
            // 6) Status-regression trigger on FaiReports
            //    Append-only discipline. Once Approved/Conditional/Rejected,
            //    only Voided is a legal transition. Voided is terminal.
            // ================================================================
            mb.Sql(@"
                CREATE OR REPLACE FUNCTION fn_block_fai_status_regression()
                RETURNS trigger AS $$
                BEGIN
                    -- Approved (3), Conditional (4), Rejected (5) can only go to Voided (6).
                    IF OLD.""Status"" IN (3, 4, 5) AND NEW.""Status"" NOT IN (OLD.""Status"", 6) THEN
                        RAISE EXCEPTION
                            'FaiReport % cannot regress from terminal status % to %. Use Status=6 (Voided).',
                            OLD.""Id"", OLD.""Status"", NEW.""Status""
                            USING ERRCODE = 'check_violation';
                    END IF;
                    -- Voided (6) is terminal.
                    IF OLD.""Status"" = 6 AND NEW.""Status"" <> 6 THEN
                        RAISE EXCEPTION
                            'FaiReport % is Voided and cannot be reactivated. Create a new FAI.',
                            OLD.""Id""
                            USING ERRCODE = 'check_violation';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");
            mb.Sql(@"DROP TRIGGER IF EXISTS trg_block_fai_status_regression ON ""FaiReports"";");
            mb.Sql(@"
                CREATE TRIGGER trg_block_fai_status_regression
                BEFORE UPDATE ON ""FaiReports""
                FOR EACH ROW
                EXECUTE FUNCTION fn_block_fai_status_regression();
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"DROP TRIGGER  IF EXISTS trg_block_fai_status_regression ON ""FaiReports"";");
            mb.Sql(@"DROP FUNCTION IF EXISTS fn_block_fai_status_regression();");

            mb.Sql(@"ALTER TABLE ""FaiProductAccountability"" DROP CONSTRAINT IF EXISTS ck_faiproductaccountability_conformance_range;");
            mb.Sql(@"ALTER TABLE ""FaiProductAccountability"" DROP CONSTRAINT IF EXISTS ck_faiproductaccountability_entrytype_range;");
            mb.Sql(@"ALTER TABLE ""FaiCharacteristics""       DROP CONSTRAINT IF EXISTS ck_faicharacteristics_conformance_range;");
            mb.Sql(@"ALTER TABLE ""FaiCharacteristics""       DROP CONSTRAINT IF EXISTS ck_faicharacteristics_measurementtype_range;");
            mb.Sql(@"ALTER TABLE ""FaiReports""               DROP CONSTRAINT IF EXISTS ck_faireports_counts_nonneg;");
            mb.Sql(@"ALTER TABLE ""FaiReports""               DROP CONSTRAINT IF EXISTS ck_faireports_airisktone_range;");
            mb.Sql(@"ALTER TABLE ""FaiReports""               DROP CONSTRAINT IF EXISTS ck_faireports_airiskscore_range;");
            mb.Sql(@"ALTER TABLE ""FaiReports""               DROP CONSTRAINT IF EXISTS ck_faireports_status_range;");
            mb.Sql(@"ALTER TABLE ""FaiReports""               DROP CONSTRAINT IF EXISTS ck_faireports_reason_range;");
            mb.Sql(@"ALTER TABLE ""FaiReports""               DROP CONSTRAINT IF EXISTS ck_faireports_parttype_range;");
            mb.Sql(@"ALTER TABLE ""FaiReports""               DROP CONSTRAINT IF EXISTS ck_faireports_type_range;");

            mb.Sql(@"ALTER TABLE ""Attachments"" DROP CONSTRAINT IF EXISTS fk_attachments_faiproductaccountability;");
            mb.Sql(@"ALTER TABLE ""Attachments"" DROP CONSTRAINT IF EXISTS fk_attachments_faicharacteristic;");
            mb.Sql(@"ALTER TABLE ""Attachments"" DROP CONSTRAINT IF EXISTS fk_attachments_faireport;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_attachments_faiproductaccountability;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_attachments_faicharacteristic;");
            mb.Sql(@"DROP INDEX IF EXISTS ix_attachments_faireport;");
            mb.Sql(@"
                ALTER TABLE ""Attachments""
                    DROP COLUMN IF EXISTS ""FaiProductAccountabilityId"",
                    DROP COLUMN IF EXISTS ""FaiCharacteristicId"",
                    DROP COLUMN IF EXISTS ""FaiReportId"";
            ");

            mb.Sql(@"DROP TABLE IF EXISTS ""FaiProductAccountability"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""FaiCharacteristics""       CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""FaiReports""               CASCADE;");
        }
    }
}
