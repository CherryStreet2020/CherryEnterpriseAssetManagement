using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // PR #104 (B-16): Promote MaintenanceEvent.FailureCode from a free-text
    // string to a proper FK against the seeded FailureCode master.
    //
    // Pre-#104, operators typed "BRG-WEAR" or "bearing wear" or
    // "bearing-wear" interchangeably. The Pareto / Weibull aggregation that
    // reliability dashboards need can't bucket free-text consistently —
    // every typo splits a real-world failure mode into its own row.
    //
    // The text column stays for backward compatibility (existing reports,
    // CloseoutService.GenerateCloseoutSummary). New writes set both the FK
    // and the denormalized text label; new reads prefer the FK.
    //
    // Backfill: case-insensitive match against FailureCode.Code, then
    // FailureCode.Name. Where two or more masters match the same string the
    // backfill leaves FailureCodeId NULL (ambiguous — admin review needed).
    //
    // Raw SQL pattern matches PR #67 / #82 / #101.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516_AddFailureCodeIdToMaintenanceEvent")]
    public partial class AddFailureCodeIdToMaintenanceEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD COLUMN IF NOT EXISTS ""FailureCodeId"" integer NULL;
            ");

            // Index for the Pareto / Weibull joins that the reliability
            // dashboards will hit. ON CONFLICT-safe via IF NOT EXISTS.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaintenanceEvents_FailureCodeId""
                ON ""MaintenanceEvents"" (""FailureCodeId"");
            ");

            // Backfill — unambiguous matches only. Two-pass: (1) match on
            // Code, (2) match on Name where Code didn't resolve. Each pass
            // skips any string that maps to multiple FailureCode rows
            // (rare; would indicate duplicate seed data).
            migrationBuilder.Sql(@"
                UPDATE ""MaintenanceEvents"" me
                SET ""FailureCodeId"" = fc.""Id""
                FROM ""FailureCodes"" fc
                WHERE me.""FailureCodeId"" IS NULL
                  AND me.""FailureCode"" IS NOT NULL
                  AND LOWER(TRIM(me.""FailureCode"")) = LOWER(fc.""Code"")
                  AND (
                      SELECT COUNT(*) FROM ""FailureCodes"" fc2
                      WHERE LOWER(fc2.""Code"") = LOWER(fc.""Code"")
                  ) = 1;
            ");
            migrationBuilder.Sql(@"
                UPDATE ""MaintenanceEvents"" me
                SET ""FailureCodeId"" = fc.""Id""
                FROM ""FailureCodes"" fc
                WHERE me.""FailureCodeId"" IS NULL
                  AND me.""FailureCode"" IS NOT NULL
                  AND LOWER(TRIM(me.""FailureCode"")) = LOWER(fc.""Name"")
                  AND (
                      SELECT COUNT(*) FROM ""FailureCodes"" fc2
                      WHERE LOWER(fc2.""Name"") = LOWER(fc.""Name"")
                  ) = 1;
            ");

            // FK constraint. ON DELETE SET NULL because the FailureCode
            // master is reference data; an admin deleting a code shouldn't
            // wipe the historical WO record, just leave the linkage NULL
            // (the denormalized text column still shows what was selected).
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD CONSTRAINT ""FK_MaintenanceEvents_FailureCodes_FailureCodeId""
                FOREIGN KEY (""FailureCodeId"") REFERENCES ""FailureCodes""(""Id"")
                ON DELETE SET NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP CONSTRAINT IF EXISTS ""FK_MaintenanceEvents_FailureCodes_FailureCodeId"";
            ");
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_MaintenanceEvents_FailureCodeId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP COLUMN IF EXISTS ""FailureCodeId"";
            ");
        }
    }
}
