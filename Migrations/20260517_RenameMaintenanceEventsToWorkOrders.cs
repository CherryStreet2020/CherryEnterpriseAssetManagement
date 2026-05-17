using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.7 — Rename the WorkOrder header table.
    //
    // Drops the "MaintenanceEvents" name in favor of "WorkOrders" — the
    // unified-work-order rename that the v0.2 ADR committed to.
    //
    // Renames in this migration:
    //   - Table:    "MaintenanceEvents"        → "WorkOrders"
    //   - PK:       "PK_MaintenanceEvents"     → "PK_WorkOrders"
    //   - Every FK: "FK_MaintenanceEvents_*"   → "FK_WorkOrders_*"
    //   - Every IX: "IX_MaintenanceEvents_*"   → "IX_WorkOrders_*"
    //
    // The constraint + index renames use dynamic DO blocks so we don't
    // have to enumerate every name by hand — there are 8+ FKs and 8+
    // indexes from migrations going back to 2026-01. Future-proof:
    // any rename pattern we add later gets picked up.
    //
    // Also backfills ApprovalAction.TargetEntityType: existing rows say
    // "MaintenanceEvent"; new rows will say "WorkOrder" (because we
    // already updated the literal in CloseoutService + MaintenanceService).
    // This UPDATE keeps audit history consistent under the new name.
    //
    // Migration history (the 30 prior migration files) still references
    // "MaintenanceEvents" in their raw SQL bodies — those files are
    // immutable historical snapshots of what the schema was at THAT
    // migration's point in time. Reading them with knowledge of this
    // 20260517 rename is the correct mental model.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_RenameMaintenanceEventsToWorkOrders")]
    public partial class RenameMaintenanceEventsToWorkOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Rename the table itself.
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents"" RENAME TO ""WorkOrders"";
            ");

            // 2) Rename the PK constraint (EF Core's default name was
            //    PK_MaintenanceEvents; want PK_WorkOrders to match
            //    convention).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE table_name = 'WorkOrders'
                          AND constraint_name = 'PK_MaintenanceEvents'
                    ) THEN
                        EXECUTE 'ALTER TABLE ""WorkOrders"" RENAME CONSTRAINT ""PK_MaintenanceEvents"" TO ""PK_WorkOrders""';
                    END IF;
                END $$;
            ");

            // 3) Rename every FK constraint that lives on this table.
            //    Matches FK_MaintenanceEvents_* (FKs OWNED by this table)
            //    AND FK_*_MaintenanceEvents_* (FKs from OTHER tables that
            //    point back to this one — those constraints live on the
            //    OTHER table, not WorkOrders, so we have to scan all
            //    tables).
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                    new_name text;
                BEGIN
                    FOR r IN
                        SELECT
                            tc.table_name AS owning_table,
                            tc.constraint_name
                        FROM information_schema.table_constraints tc
                        WHERE tc.constraint_type = 'FOREIGN KEY'
                          AND tc.constraint_name LIKE '%MaintenanceEvents%'
                    LOOP
                        new_name := REPLACE(r.constraint_name, 'MaintenanceEvents', 'WorkOrders');
                        EXECUTE format(
                            'ALTER TABLE %I RENAME CONSTRAINT %I TO %I',
                            r.owning_table, r.constraint_name, new_name
                        );
                    END LOOP;
                END $$;
            ");

            // 4) Rename every index whose name contains MaintenanceEvents.
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
                          AND indexname LIKE '%MaintenanceEvents%'
                    LOOP
                        new_name := REPLACE(r.indexname, 'MaintenanceEvents', 'WorkOrders');
                        EXECUTE format(
                            'ALTER INDEX %I RENAME TO %I',
                            r.indexname, new_name
                        );
                    END LOOP;
                END $$;
            ");

            // 5) Backfill ApprovalAction.TargetEntityType so audit-log
            //    history is consistent under the new name. The literal
            //    in CloseoutService + MaintenanceService was already
            //    sed-replaced to ""WorkOrder""; this aligns historical
            //    rows.
            migrationBuilder.Sql(@"
                UPDATE ""ApprovalActions""
                SET ""TargetEntityType"" = 'WorkOrder'
                WHERE ""TargetEntityType"" = 'MaintenanceEvent';
            ");

            // 6) Backfill AuditEntityTypes lookup row. The seed JSON
            //    file (seed/reference-data/AuditEntityType.json) was
            //    updated; this aligns the existing DB row so the FK
            //    audit-log labels stay correct.
            migrationBuilder.Sql(@"
                UPDATE ""AuditEntityTypes""
                SET ""Code"" = 'WorkOrder', ""Name"" = 'Work Order'
                WHERE ""Code"" = 'MaintenanceEvent';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse order.

            // 6) Reverse the AuditEntityTypes backfill.
            migrationBuilder.Sql(@"
                UPDATE ""AuditEntityTypes""
                SET ""Code"" = 'MaintenanceEvent', ""Name"" = 'Maintenance Event'
                WHERE ""Code"" = 'WorkOrder';
            ");

            // 5) Reverse the ApprovalAction backfill.
            migrationBuilder.Sql(@"
                UPDATE ""ApprovalActions""
                SET ""TargetEntityType"" = 'MaintenanceEvent'
                WHERE ""TargetEntityType"" = 'WorkOrder';
            ");

            // 4) Reverse index renames.
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
                          AND indexname LIKE '%WorkOrders%'
                          AND indexname NOT LIKE '%WorkOrders%WorkOrders%'  -- avoid double-replacing
                    LOOP
                        new_name := REPLACE(r.indexname, 'WorkOrders', 'MaintenanceEvents');
                        IF new_name <> r.indexname THEN
                            EXECUTE format(
                                'ALTER INDEX %I RENAME TO %I',
                                r.indexname, new_name
                            );
                        END IF;
                    END LOOP;
                END $$;
            ");

            // 3) Reverse FK constraint renames.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                    new_name text;
                BEGIN
                    FOR r IN
                        SELECT
                            tc.table_name AS owning_table,
                            tc.constraint_name
                        FROM information_schema.table_constraints tc
                        WHERE tc.constraint_type = 'FOREIGN KEY'
                          AND tc.constraint_name LIKE '%WorkOrders%'
                    LOOP
                        new_name := REPLACE(r.constraint_name, 'WorkOrders', 'MaintenanceEvents');
                        IF new_name <> r.constraint_name THEN
                            EXECUTE format(
                                'ALTER TABLE %I RENAME CONSTRAINT %I TO %I',
                                r.owning_table, r.constraint_name, new_name
                            );
                        END IF;
                    END LOOP;
                END $$;
            ");

            // 2) Reverse PK rename.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE table_name = 'WorkOrders'
                          AND constraint_name = 'PK_WorkOrders'
                    ) THEN
                        EXECUTE 'ALTER TABLE ""WorkOrders"" RENAME CONSTRAINT ""PK_WorkOrders"" TO ""PK_MaintenanceEvents""';
                    END IF;
                END $$;
            ");

            // 1) Reverse the table rename.
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrders"" RENAME TO ""MaintenanceEvents"";
            ");
        }
    }
}
