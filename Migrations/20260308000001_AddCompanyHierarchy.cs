using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    public partial class AddCompanyHierarchy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Users' AND column_name = 'AssignedCompanyId'
                    ) THEN
                        ALTER TABLE ""Users"" ADD COLUMN ""AssignedCompanyId"" integer NULL;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name = 'FK_Users_Companies_AssignedCompanyId'
                    ) THEN
                        ALTER TABLE ""Users""
                            ADD CONSTRAINT ""FK_Users_Companies_AssignedCompanyId""
                            FOREIGN KEY (""AssignedCompanyId"")
                            REFERENCES ""Companies"" (""Id"")
                            ON DELETE SET NULL;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE indexname = 'IX_Users_AssignedCompanyId'
                    ) THEN
                        CREATE INDEX ""IX_Users_AssignedCompanyId""
                            ON ""Users"" (""AssignedCompanyId"");
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" DROP CONSTRAINT IF EXISTS ""FK_Users_Companies_AssignedCompanyId"";
                DROP INDEX IF EXISTS ""IX_Users_AssignedCompanyId"";
                ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""AssignedCompanyId"";
            ");
        }
    }
}
