using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // S1-2: Replace the CustomField1 = "PMTA:N" string hack with proper FK
    // columns on MaintenanceEvent. The hack conflated PMOccurrence.Id with
    // PMTemplateAsset.Id (different tables, different namespaces); the WO
    // closeout's "advance the PM cycle" logic silently miss-targeted rows
    // or no-oped. See docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md.
    //
    // Schema-only and additive: nullable columns + nullable FKs. No
    // backfill — existing in-flight CustomField1-tagged rows simply don't
    // advance their PM cycle on close (same as today's broken behavior).
    // New WOs generated post-migration carry the FKs and behave correctly.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260508120000_AddPmLinkageFksToMaintenanceEvents")]
    public partial class AddPmLinkageFksToMaintenanceEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD COLUMN IF NOT EXISTS ""PMOccurrenceId"" integer NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                ADD COLUMN IF NOT EXISTS ""PMTemplateAssetId"" integer NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaintenanceEvents_PMOccurrenceId""
                ON ""MaintenanceEvents"" (""PMOccurrenceId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaintenanceEvents_PMTemplateAssetId""
                ON ""MaintenanceEvents"" (""PMTemplateAssetId"");
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_MaintenanceEvents_PMOccurrences_PMOccurrenceId'
                    ) THEN
                        ALTER TABLE ""MaintenanceEvents""
                        ADD CONSTRAINT ""FK_MaintenanceEvents_PMOccurrences_PMOccurrenceId""
                        FOREIGN KEY (""PMOccurrenceId"")
                        REFERENCES ""PMOccurrences"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_MaintenanceEvents_PMTemplateAssets_PMTemplateAssetId'
                    ) THEN
                        ALTER TABLE ""MaintenanceEvents""
                        ADD CONSTRAINT ""FK_MaintenanceEvents_PMTemplateAssets_PMTemplateAssetId""
                        FOREIGN KEY (""PMTemplateAssetId"")
                        REFERENCES ""PMTemplateAssets"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP CONSTRAINT IF EXISTS ""FK_MaintenanceEvents_PMTemplateAssets_PMTemplateAssetId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP CONSTRAINT IF EXISTS ""FK_MaintenanceEvents_PMOccurrences_PMOccurrenceId"";
            ");
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_MaintenanceEvents_PMTemplateAssetId"";
            ");
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_MaintenanceEvents_PMOccurrenceId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP COLUMN IF EXISTS ""PMTemplateAssetId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""MaintenanceEvents""
                DROP COLUMN IF EXISTS ""PMOccurrenceId"";
            ");
        }
    }
}
