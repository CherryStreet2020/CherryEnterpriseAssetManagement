using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // MP #112 — Period Close Orchestration.
    //
    // Stores the auditor-facing snapshot of every pre-flight check value
    // captured at the moment of close, plus the sequenced-step trace.
    // Lives on FiscalPeriod (not AuditLog) so an auditor pulling the
    // period record gets the close packet inline rather than chasing
    // foreign keys.
    //
    // Format is JSON; the orchestration service writes it as a single
    // serialized PreflightSnapshot record. Nullable because periods
    // created before this PR (and currently-open periods) have no
    // snapshot. Re-opening a period preserves the prior snapshot;
    // re-closing overwrites with the new one.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516_AddPreflightSnapshotToFiscalPeriod")]
    public partial class AddPreflightSnapshotToFiscalPeriod : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""FiscalPeriods""
                ADD COLUMN IF NOT EXISTS ""PreflightSnapshotJson"" text NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""FiscalPeriods""
                DROP COLUMN IF EXISTS ""PreflightSnapshotJson"";
            ");
        }
    }
}
