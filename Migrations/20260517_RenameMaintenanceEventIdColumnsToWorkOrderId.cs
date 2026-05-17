using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.7.3 — Rename child-table FK *columns* from
    // MaintenanceEventId → WorkOrderId.
    //
    // Why this exists:
    //   PR #119.7 renamed the parent TABLE (MaintenanceEvents → WorkOrders)
    //   and used a dynamic DO block with LIKE '%MaintenanceEvents%' to
    //   rename every FK constraint and index whose name contained the
    //   table-name segment. Critically, "MaintenanceEvents" (with the
    //   trailing 's') is NOT a substring of "MaintenanceEventId" (which
    //   ends in 'Id'), so those LIKE matches never touched:
    //
    //     - The FK *column name* MaintenanceEventId on three child tables
    //     - Indexes named IX_<table>_MaintenanceEventId
    //
    //   Meanwhile the C# sed-rename DID rewrite every property and
    //   navigation reference to WorkOrderId. EF Core then queried
    //   "WorkOrderId" against a DB that still had a column named
    //   "MaintenanceEventId", crashing the WorkOrder Details page with
    //   `PostgresException: 42703: column w0.WorkOrderId does not exist`.
    //
    // Affected child tables (confirmed via AppDbContextModelSnapshot.cs):
    //   - Attachments.MaintenanceEventId        (int? NULL, ON DELETE SET NULL)
    //   - WorkOrderOperations.MaintenanceEventId (int NOT NULL, ON DELETE CASCADE)
    //   - WorkOrderParts.MaintenanceEventId      (int NOT NULL, ON DELETE CASCADE)
    //
    // Migration steps:
    //   1) ALTER TABLE … RENAME COLUMN on each of the 3 child tables.
    //   2) Sweep FK constraint names ending in 'MaintenanceEventId' and
    //      rename the trailing segment to 'WorkOrderId'.
    //   3) Sweep index names ending in 'MaintenanceEventId' likewise.
    //
    // PR #119.7 guarded the AuditEntityTypes UPDATE in PR #119.7.1 because
    // that table may not exist in some envs. No such guard needed here —
    // every Attachments / WorkOrderOperations / WorkOrderParts table is
    // created by the original 2026-01 multi-location migration that's
    // already run in every env.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_RenameMaintenanceEventIdColumnsToWorkOrderId")]
    public partial class RenameMaintenanceEventIdColumnsToWorkOrderId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Rename the three child-table columns.
            //    Guard each with to_regclass so a partial-state DB can't
            //    block the migration. Postgres auto-rewrites referencing
            //    index column-lists and FK column-lists on rename.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema='public'
                          AND table_name='Attachments'
                          AND column_name='MaintenanceEventId'
                    ) THEN
                        EXECUTE 'ALTER TABLE ""Attachments"" RENAME COLUMN ""MaintenanceEventId"" TO ""WorkOrderId""';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema='public'
                          AND table_name='WorkOrderOperations'
                          AND column_name='MaintenanceEventId'
                    ) THEN
                        EXECUTE 'ALTER TABLE ""WorkOrderOperations"" RENAME COLUMN ""MaintenanceEventId"" TO ""WorkOrderId""';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema='public'
                          AND table_name='WorkOrderParts'
                          AND column_name='MaintenanceEventId'
                    ) THEN
                        EXECUTE 'ALTER TABLE ""WorkOrderParts"" RENAME COLUMN ""MaintenanceEventId"" TO ""WorkOrderId""';
                    END IF;
                END $$;
            ");

            // 2) Rename FK constraint names whose trailing segment is
            //    still 'MaintenanceEventId'. Use REGEXP_REPLACE to replace
            //    only the trailing token (anchored to end-of-name) so we
            //    don't accidentally touch a constraint that legitimately
            //    references the historical name elsewhere.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                    new_name text;
                BEGIN
                    FOR r IN
                        SELECT
                            tc.table_name      AS owning_table,
                            tc.constraint_name
                        FROM information_schema.table_constraints tc
                        WHERE tc.constraint_type = 'FOREIGN KEY'
                          AND tc.constraint_name LIKE '%MaintenanceEventId'
                    LOOP
                        new_name := REGEXP_REPLACE(r.constraint_name, 'MaintenanceEventId$', 'WorkOrderId');
                        EXECUTE format(
                            'ALTER TABLE %I RENAME CONSTRAINT %I TO %I',
                            r.owning_table, r.constraint_name, new_name
                        );
                    END LOOP;
                END $$;
            ");

            // 3) Rename indexes whose name contains 'MaintenanceEventId'.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                    new_name text;
                BEGIN
                    FOR r IN
                        SELECT indexname
                        FROM pg_indexes
                        WHERE schemaname = 'public'
                          AND indexname LIKE '%MaintenanceEventId%'
                    LOOP
                        new_name := REPLACE(r.indexname, 'MaintenanceEventId', 'WorkOrderId');
                        IF new_name <> r.indexname THEN
                            EXECUTE format(
                                'ALTER INDEX %I RENAME TO %I',
                                r.indexname, new_name
                            );
                        END IF;
                    END LOOP;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse order.

            // 3) Reverse index renames. Only rename indexes that we made:
            //    those whose name contains 'WorkOrderId' but came from a
            //    table whose other indexes never legitimately had a
            //    WorkOrderId suffix. Simplest: rename every
            //    IX_*WorkOrderId on the three known tables back.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                    new_name text;
                BEGIN
                    FOR r IN
                        SELECT i.indexname, i.tablename
                        FROM pg_indexes i
                        WHERE i.schemaname = 'public'
                          AND i.indexname LIKE '%WorkOrderId%'
                          AND i.tablename IN ('Attachments', 'WorkOrderOperations', 'WorkOrderParts')
                    LOOP
                        new_name := REPLACE(r.indexname, 'WorkOrderId', 'MaintenanceEventId');
                        IF new_name <> r.indexname THEN
                            EXECUTE format(
                                'ALTER INDEX %I RENAME TO %I',
                                r.indexname, new_name
                            );
                        END IF;
                    END LOOP;
                END $$;
            ");

            // 2) Reverse FK constraint renames.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                    new_name text;
                BEGIN
                    FOR r IN
                        SELECT
                            tc.table_name      AS owning_table,
                            tc.constraint_name
                        FROM information_schema.table_constraints tc
                        WHERE tc.constraint_type = 'FOREIGN KEY'
                          AND tc.constraint_name LIKE '%WorkOrderId'
                          AND tc.table_name IN ('Attachments', 'WorkOrderOperations', 'WorkOrderParts')
                    LOOP
                        new_name := REGEXP_REPLACE(r.constraint_name, 'WorkOrderId$', 'MaintenanceEventId');
                        IF new_name <> r.constraint_name THEN
                            EXECUTE format(
                                'ALTER TABLE %I RENAME CONSTRAINT %I TO %I',
                                r.owning_table, r.constraint_name, new_name
                            );
                        END IF;
                    END LOOP;
                END $$;
            ");

            // 1) Reverse column renames.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema='public'
                          AND table_name='WorkOrderParts'
                          AND column_name='WorkOrderId'
                    ) THEN
                        EXECUTE 'ALTER TABLE ""WorkOrderParts"" RENAME COLUMN ""WorkOrderId"" TO ""MaintenanceEventId""';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema='public'
                          AND table_name='WorkOrderOperations'
                          AND column_name='WorkOrderId'
                    ) THEN
                        EXECUTE 'ALTER TABLE ""WorkOrderOperations"" RENAME COLUMN ""WorkOrderId"" TO ""MaintenanceEventId""';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema='public'
                          AND table_name='Attachments'
                          AND column_name='WorkOrderId'
                    ) THEN
                        EXECUTE 'ALTER TABLE ""Attachments"" RENAME COLUMN ""WorkOrderId"" TO ""MaintenanceEventId""';
                    END IF;
                END $$;
            ");
        }
    }
}
