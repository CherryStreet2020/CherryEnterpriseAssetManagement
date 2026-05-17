using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.6 — Revision tracking on the WorkOrder header.
    //
    // Adds two columns + one index + one self-FK to MaintenanceEvents:
    //
    //   Revision             smallint NOT NULL DEFAULT 0
    //   MasterWorkOrderId    integer NULL FK → MaintenanceEvents(Id) ON DELETE SET NULL
    //
    // Pattern: Revision=0 + MasterWorkOrderId=NULL on the master record;
    // Revision=N + MasterWorkOrderId=master.Id on derived revisions.
    //
    // Backfill: every existing row gets Revision=0 (the column DEFAULT does
    // this automatically; no UPDATE needed). MasterWorkOrderId stays NULL
    // for everyone — no historical re-issues exist.
    //
    // Index supports "find all revisions of WO X" lookup in the re-issue
    // UI (Phase F).
    //
    // The self-FK uses ON DELETE SET NULL so deleting a master orphans
    // its revisions rather than cascading. Each orphan becomes its own
    // master (Revision retains its prior value; MasterWorkOrderId NULLs out).
    // Audit trail is preserved.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddWorkOrderRevisionAndMaster")]
    public partial class AddWorkOrderRevisionAndMaster : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD COLUMN IF NOT EXISTS ""Revision"" smallint NOT NULL DEFAULT 0;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD COLUMN IF NOT EXISTS ""MasterWorkOrderId"" integer NULL;
            ");

            // Self-FK with ON DELETE SET NULL.
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD CONSTRAINT ""FK_MaintenanceEvents_MaintenanceEvents_MasterWorkOrderId""
                FOREIGN KEY (""MasterWorkOrderId"") REFERENCES ""MaintenanceEvents""(""Id"")
                ON DELETE SET NULL;
            ");

            // Composite index for "find all revisions of master X" query.
            // ORDER BY Revision DESC at query time; Postgres can scan this
            // index backwards efficiently — no need for an explicit
            // DESC qualifier in the index definition.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS
                    ""IX_MaintenanceEvents_MasterWorkOrderId_Revision""
                ON ""MaintenanceEvents"" (""MasterWorkOrderId"", ""Revision"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_MaintenanceEvents_MasterWorkOrderId_Revision"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP CONSTRAINT IF EXISTS ""FK_MaintenanceEvents_MaintenanceEvents_MasterWorkOrderId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP COLUMN IF EXISTS ""MasterWorkOrderId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP COLUMN IF EXISTS ""Revision"";
            ");
        }
    }
}
