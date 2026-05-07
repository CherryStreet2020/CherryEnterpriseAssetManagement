using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Adds StatusLookupValueId FK column to WorkRequests, and backfills
    // existing rows by matching the legacy Status enum int to the
    // WorkRequestStatus LookupValue's Code (which PR #6 + #7 aligned).
    //
    // After this migration applies, Pages/Maintenance/WorkRequests/Create.cshtml.cs
    // writes StatusLookupValueId alongside Status when a new request is
    // created — keeping the FK and enum in lockstep, same pattern as
    // every other migrated entity.
    //
    // Idempotent: re-running on a DB that already has the column is a
    // no-op (CREATE COLUMN IF NOT EXISTS, UPDATE WHERE NULL).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260507190000_AddStatusLookupValueIdToWorkRequest")]
    public partial class AddStatusLookupValueIdToWorkRequest : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkRequests""
                ADD COLUMN IF NOT EXISTS ""StatusLookupValueId"" integer NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_WorkRequests_StatusLookupValueId""
                ON ""WorkRequests"" (""StatusLookupValueId"");
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_WorkRequests_LookupValues_StatusLookupValueId'
                    ) THEN
                        ALTER TABLE ""WorkRequests""
                        ADD CONSTRAINT ""FK_WorkRequests_LookupValues_StatusLookupValueId""
                        FOREIGN KEY (""StatusLookupValueId"")
                        REFERENCES ""LookupValues"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            // Backfill existing rows by matching the legacy enum int to
            // the WorkRequestStatus LookupValue's Code.
            migrationBuilder.Sql(@"
                UPDATE ""WorkRequests"" wr
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lv.""LookupTypeId"" = lt.""Id""
                WHERE wr.""StatusLookupValueId"" IS NULL
                  AND lt.""Key"" = 'WorkRequestStatus'
                  AND lv.""Code"" = wr.""Status""::int::text;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkRequests""
                DROP CONSTRAINT IF EXISTS ""FK_WorkRequests_LookupValues_StatusLookupValueId"";
            ");
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_WorkRequests_StatusLookupValueId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkRequests""
                DROP COLUMN IF EXISTS ""StatusLookupValueId"";
            ");
        }
    }
}
