using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 / PR #119.1 — Unified Work Orders: top-level category +
    // external-system linkage on MaintenanceEvent.
    //
    // Adds three columns (all NULLable on disk, then Category gets backfilled
    // to 0 = Maintenance for every existing row, then made NOT NULL):
    //
    //   Category              smallint NOT NULL DEFAULT 0   (WorkOrderCategory enum)
    //   ExternalWorkOrderId   varchar(64) NULL
    //   ExternalSource        varchar(32) NULL
    //
    // Backfill semantics: every existing MaintenanceEvent row becomes
    // Category=Maintenance (0). Lossless. The .NET class default also matches
    // so new inserts that don't specify Category get the right value.
    //
    // Indexes:
    //   - IX_MaintenanceEvents_Category — drives the Plant Floor + WorkQueue
    //     filter chips ("show only Production WOs", etc.)
    //   - IX_MaintenanceEvents_ExternalSource_ExternalWorkOrderId — composite
    //     unique-where-not-null lookup for ERP idempotency (the inbound
    //     webhook handler shipped in Sprint 6 will use this for dedup).
    //
    // No FK constraints added — ExternalSource is free text per ADR-012
    // open question 4. A lookup-table migration is queued as PR #119.4.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddWorkOrderCategoryAndExternalSource")]
    public partial class AddWorkOrderCategoryAndExternalSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add the three columns. Category is NOT NULL with DEFAULT 0
            //    so the ALTER on a large table is a metadata-only operation
            //    in Postgres (no full table rewrite).
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD COLUMN IF NOT EXISTS ""Category"" smallint NOT NULL DEFAULT 0;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD COLUMN IF NOT EXISTS ""ExternalWorkOrderId"" varchar(64) NULL;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD COLUMN IF NOT EXISTS ""ExternalSource"" varchar(32) NULL;
            ");

            // 2) Index on Category for filter-chip queries on the unified
            //    work queue + Plant Floor. Cheap to add; pays off the moment
            //    any UI says "show me Quality WOs only".
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaintenanceEvents_Category""
                ON ""MaintenanceEvents"" (""Category"");
            ");

            // 3) Composite index for ERP idempotency. Partial index on the
            //    NOT NULL case keeps the index small (only ERP-sourced rows).
            //    Lets the inbound webhook handler do a single index probe to
            //    decide insert-vs-update without scanning the whole table.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaintenanceEvents_ExternalSource_ExternalWorkOrderId""
                ON ""MaintenanceEvents"" (""ExternalSource"", ""ExternalWorkOrderId"")
                WHERE ""ExternalSource"" IS NOT NULL AND ""ExternalWorkOrderId"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_MaintenanceEvents_ExternalSource_ExternalWorkOrderId"";
            ");
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_MaintenanceEvents_Category"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP COLUMN IF EXISTS ""ExternalSource"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP COLUMN IF EXISTS ""ExternalWorkOrderId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP COLUMN IF EXISTS ""Category"";
            ");
        }
    }
}
