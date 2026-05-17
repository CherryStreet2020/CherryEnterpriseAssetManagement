using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.5.1 — Re-cast WorkOrders config tables.
    //
    // The AppDbContext.CapitalizeStringProperties convention uppercased
    // every string column on insert until PR #119.5.1 added the
    // Abs.FixedAssets.Models.WorkOrders namespace to the exemption list.
    // So all rows seeded by PR #119.2 through PR #119.5 have their
    // string values stored as UPPERCASE (StatusKey, GuardServiceName,
    // DisplayLabel, DisplayColor, Stage, FieldName, etc.).
    //
    // The fix is two-step:
    //   1. PR #119.5.1 namespace exemption (already in this branch)
    //   2. TRUNCATE the affected tables in this migration so the
    //      idempotent seeders re-populate with proper casing on the next
    //      Web Server startup.
    //
    // WorkOrderApproval is empty in the current deploy (no legacy
    // ApprovedById rows to backfill), so we leave it alone. If any
    // tenant had recorded approvals between the namespace bug and this
    // fix, they'd need a per-row recast — out of scope.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_RecastWorkOrdersConfigTables")]
    public partial class RecastWorkOrdersConfigTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use TRUNCATE so identity sequences reset (cleaner than DELETE).
            // CASCADE not needed — these tables have no inbound FKs from
            // application data; the only FK is WorkOrderApproval → MaintenanceEvents
            // and we're not touching WorkOrderApproval.
            migrationBuilder.Sql(@"TRUNCATE TABLE ""WorkOrderFieldVisibility"" RESTART IDENTITY;");
            migrationBuilder.Sql(@"TRUNCATE TABLE ""WorkOrderStatusTransition"" RESTART IDENTITY;");
            migrationBuilder.Sql(@"TRUNCATE TABLE ""WorkOrderStatusLabel"" RESTART IDENTITY;");
            migrationBuilder.Sql(@"DELETE FROM ""WorkOrderStatusProfile"";");
            migrationBuilder.Sql(@"TRUNCATE TABLE ""NumberSequence"" RESTART IDENTITY;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op. Down would mean putting the corrupted UPPERCASE data
            // back, which is exactly what we just fixed. Leave the tables
            // empty; running the seeders again will re-populate.
        }
    }
}
