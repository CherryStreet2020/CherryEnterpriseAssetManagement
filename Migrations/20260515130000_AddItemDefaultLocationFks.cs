using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // DEF-008: Best-in-class item-location preferences.
    //
    // Adds:
    //   Items.DefaultLocationId           — global default storage location (FK → Locations.Id)
    //   ItemCompanyStockings.DefaultLocationId — per-company override (FK → Locations.Id)
    //
    // The receive UI cascades: per-company → global → null. The receive
    // posting service requires a non-null ReceivingLocationId for stock
    // items to update ItemInventory; this gives the receive form a
    // canonical default to pre-fill from so operators don't have to pick
    // every time (and can't silently submit without a location for stock).
    //
    // Schema-only + additive. Existing items keep NULL; backfill is left
    // to operators via the Item edit UI.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260515130000_AddItemDefaultLocationFks")]
    public partial class AddItemDefaultLocationFks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Items""
                ADD COLUMN IF NOT EXISTS ""DefaultLocationId"" integer NULL;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""ItemCompanyStockings""
                ADD COLUMN IF NOT EXISTS ""DefaultLocationId"" integer NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Items_DefaultLocationId""
                ON ""Items"" (""DefaultLocationId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ItemCompanyStockings_DefaultLocationId""
                ON ""ItemCompanyStockings"" (""DefaultLocationId"");
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Items_Locations_DefaultLocationId') THEN
                        ALTER TABLE ""Items""
                        ADD CONSTRAINT ""FK_Items_Locations_DefaultLocationId""
                        FOREIGN KEY (""DefaultLocationId"")
                        REFERENCES ""Locations"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ItemCompanyStockings_Locations_DefaultLocationId') THEN
                        ALTER TABLE ""ItemCompanyStockings""
                        ADD CONSTRAINT ""FK_ItemCompanyStockings_Locations_DefaultLocationId""
                        FOREIGN KEY (""DefaultLocationId"")
                        REFERENCES ""Locations"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ItemCompanyStockings""
                DROP CONSTRAINT IF EXISTS ""FK_ItemCompanyStockings_Locations_DefaultLocationId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Items""
                DROP CONSTRAINT IF EXISTS ""FK_Items_Locations_DefaultLocationId"";
            ");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ItemCompanyStockings_DefaultLocationId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Items_DefaultLocationId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ItemCompanyStockings"" DROP COLUMN IF EXISTS ""DefaultLocationId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Items"" DROP COLUMN IF EXISTS ""DefaultLocationId"";");
        }
    }
}
