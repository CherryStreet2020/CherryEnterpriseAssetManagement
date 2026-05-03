using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <summary>
    /// Adds CompanyId to CcaClassBalances so each Canadian subsidiary keeps
    /// its own UCC roll-forward. Replaces the old (CcaClassId, FiscalYear)
    /// unique index with (CompanyId, CcaClassId, FiscalYear).
    ///
    /// Idempotent: safe to re-run against a DB that already has the new
    /// schema (dev environments where the column was added by hand).
    /// </summary>
    public partial class AddCompanyIdToCcaClassBalance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the column with a temporary default of 0 so existing rows
            //    survive the NOT NULL constraint. We backfill from
            //    CcaTransactions → Assets.CompanyId immediately after.
            migrationBuilder.Sql(@"
                ALTER TABLE ""CcaClassBalances""
                ADD COLUMN IF NOT EXISTS ""CompanyId"" integer NOT NULL DEFAULT 0;
            ");

            // 2. Best-effort backfill: derive CompanyId from any CcaTransaction
            //    in the same (CcaClassId, FiscalYear) whose Asset has a CompanyId.
            migrationBuilder.Sql(@"
                UPDATE ""CcaClassBalances"" AS b
                SET ""CompanyId"" = sub.company_id
                FROM (
                    SELECT ct.""CcaClassId"" AS cca_class_id,
                           ct.""FiscalYear"" AS fiscal_year,
                           MIN(a.""CompanyId"") AS company_id
                    FROM ""CcaTransactions"" ct
                    JOIN ""Assets"" a ON a.""Id"" = ct.""AssetId""
                    WHERE a.""CompanyId"" IS NOT NULL
                    GROUP BY ct.""CcaClassId"", ct.""FiscalYear""
                ) AS sub
                WHERE b.""CompanyId"" = 0
                  AND b.""CcaClassId"" = sub.cca_class_id
                  AND b.""FiscalYear"" = sub.fiscal_year;
            ");

            // 2b. Any CcaClassBalances row that still has CompanyId = 0 is
            //     orphaned (no underlying CcaTransaction with a company-bound
            //     Asset). Such rows can't be safely attributed to any
            //     subsidiary, so delete them rather than block the migration.
            migrationBuilder.Sql(@"DELETE FROM ""CcaClassBalances"" WHERE ""CompanyId"" = 0;");

            // 3. Drop the old (CcaClassId, FiscalYear) unique index.
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_CcaClassBalances_CcaClassId_FiscalYear"";
            ");

            // 4. Create the new (CompanyId, CcaClassId, FiscalYear) unique index.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CcaClassBalances_CompanyId_CcaClassId_FiscalYear""
                ON ""CcaClassBalances"" (""CompanyId"", ""CcaClassId"", ""FiscalYear"");
            ");

            // 5. Add the Companies FK if not present.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints
                        WHERE table_name = 'CcaClassBalances'
                          AND constraint_name = 'FK_CcaClassBalances_Companies_CompanyId'
                    ) THEN
                        ALTER TABLE ""CcaClassBalances""
                        ADD CONSTRAINT ""FK_CcaClassBalances_Companies_CompanyId""
                        FOREIGN KEY (""CompanyId"") REFERENCES ""Companies"" (""Id"")
                        ON DELETE RESTRICT;
                    END IF;
                END$$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""CcaClassBalances"" DROP CONSTRAINT IF EXISTS ""FK_CcaClassBalances_Companies_CompanyId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CcaClassBalances_CompanyId_CcaClassId_FiscalYear"";");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CcaClassBalances_CcaClassId_FiscalYear"" ON ""CcaClassBalances"" (""CcaClassId"", ""FiscalYear"");");
            migrationBuilder.Sql(@"ALTER TABLE ""CcaClassBalances"" DROP COLUMN IF EXISTS ""CompanyId"";");
        }
    }
}
