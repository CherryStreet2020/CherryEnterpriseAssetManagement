using Microsoft.EntityFrameworkCore.Migrations;

namespace Abs.FixedAssets.Migrations
{
    public partial class HardenAssetTypeUnspecifiedBackfill_TenantScoped : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""AssetTypeLookupValueId"" = lv.""Id""
                FROM ""Companies"" c
                JOIN ""LookupTypes"" lt ON lt.""TenantId"" = c.""TenantId""
                                       AND lt.""Key"" = 'AssetType'
                                       AND lt.""CompanyId"" IS NOT DISTINCT FROM NULL
                JOIN ""LookupValues"" lv ON lv.""LookupTypeId"" = lt.""Id""
                                         AND lv.""Code"" = 'UNSPECIFIED'
                WHERE a.""CompanyId"" = c.""Id""
                  AND a.""AssetTypeLookupValueId"" IS NULL
                  AND (a.""AssetType"" IS NULL OR btrim(a.""AssetType"") = '');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
