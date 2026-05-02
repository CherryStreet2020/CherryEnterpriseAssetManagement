using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    public partial class AddUserAssignedSiteId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Users' AND column_name = 'AssignedSiteId'
                    ) THEN
                        ALTER TABLE ""Users"" ADD COLUMN ""AssignedSiteId"" integer NULL;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_Users_Sites_AssignedSiteId'
                    ) THEN
                        ALTER TABLE ""Users""
                            ADD CONSTRAINT ""FK_Users_Sites_AssignedSiteId""
                            FOREIGN KEY (""AssignedSiteId"")
                            REFERENCES ""Sites"" (""Id"")
                            ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE indexname = 'IX_Users_AssignedSiteId'
                    ) THEN
                        CREATE INDEX ""IX_Users_AssignedSiteId"" ON ""Users"" (""AssignedSiteId"");
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" DROP CONSTRAINT IF EXISTS ""FK_Users_Sites_AssignedSiteId"";
                DROP INDEX IF EXISTS ""IX_Users_AssignedSiteId"";
                ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""AssignedSiteId"";
            ");
        }
    }
}
