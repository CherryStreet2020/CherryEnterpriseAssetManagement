using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Adds StatusLookupValueId FK column to InventoryLists, and backfills
    // existing rows by matching the legacy Status enum int to the
    // InventoryStatus LookupValue's Code (which PR #6 + #7 aligned).
    //
    // After this migration applies, Pages/Inventory/List.cshtml.cs writes
    // StatusLookupValueId alongside Status on every status transition
    // (OnPostStartAsync, OnPostCompleteAsync), keeping the FK and enum
    // in lockstep — same pattern as Pages/Purchasing/Details.cshtml.cs
    // and Pages/Maintenance/ScheduleBoard.cshtml.cs after PR #10.
    //
    // Idempotent: re-running on a DB that already has the column is a
    // no-op (CREATE COLUMN IF NOT EXISTS, UPDATE WHERE NULL).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260507180000_AddStatusLookupValueIdToInventoryList")]
    public partial class AddStatusLookupValueIdToInventoryList : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the FK column. Nullable so existing rows aren't required
            // to have a value populated atomically; the backfill follows.
            migrationBuilder.Sql(@"
                ALTER TABLE ""InventoryLists""
                ADD COLUMN IF NOT EXISTS ""StatusLookupValueId"" integer NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_InventoryLists_StatusLookupValueId""
                ON ""InventoryLists"" (""StatusLookupValueId"");
            ");

            // FK constraint to LookupValues; ON DELETE SET NULL so a
            // LookupValue removal doesn't cascade-delete inventory rows.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_InventoryLists_LookupValues_StatusLookupValueId'
                    ) THEN
                        ALTER TABLE ""InventoryLists""
                        ADD CONSTRAINT ""FK_InventoryLists_LookupValues_StatusLookupValueId""
                        FOREIGN KEY (""StatusLookupValueId"")
                        REFERENCES ""LookupValues"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            // Backfill: for each InventoryList row where StatusLookupValueId
            // is null, look up the LookupValue whose Code matches the
            // legacy Status enum int (cast to text). Only matches against
            // the InventoryStatus LookupType so we don't accidentally
            // pull a same-coded value from a different type.
            migrationBuilder.Sql(@"
                UPDATE ""InventoryLists"" il
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lv.""LookupTypeId"" = lt.""Id""
                WHERE il.""StatusLookupValueId"" IS NULL
                  AND lt.""Key"" = 'InventoryStatus'
                  AND lv.""Code"" = il.""Status""::int::text;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""InventoryLists""
                DROP CONSTRAINT IF EXISTS ""FK_InventoryLists_LookupValues_StatusLookupValueId"";
            ");
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_InventoryLists_StatusLookupValueId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""InventoryLists""
                DROP COLUMN IF EXISTS ""StatusLookupValueId"";
            ");
        }
    }
}
