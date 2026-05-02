using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetCompanyAssetNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: this index was previously applied directly via psql on the
            // production database to enforce uniqueness of AssetNumber within a Company.
            // Use raw SQL with IF NOT EXISTS so the migration is safe to apply on
            // databases that already have the index, and on fresh databases.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Assets_CompanyId_AssetNumber_Unique\" " +
                "ON \"Assets\" (\"CompanyId\", \"AssetNumber\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_Assets_CompanyId_AssetNumber_Unique\";");
        }
    }
}
