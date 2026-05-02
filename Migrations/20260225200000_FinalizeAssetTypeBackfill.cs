using Microsoft.EntityFrameworkCore.Migrations;

namespace Abs.FixedAssets.Migrations
{
    public partial class FinalizeAssetTypeBackfill : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""AssetTypeLookupValueId"" = (
                    SELECT lv.""Id""
                    FROM ""LookupValues"" lv
                    JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                    WHERE lt.""Key"" = 'AssetType'
                      AND lv.""Code"" = 'UNSPECIFIED'
                    LIMIT 1
                )
                WHERE a.""AssetTypeLookupValueId"" IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""AssetTypeLookupValueId"" = NULL
                WHERE a.""AssetTypeLookupValueId"" = (
                    SELECT lv.""Id""
                    FROM ""LookupValues"" lv
                    JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                    WHERE lt.""Key"" = 'AssetType'
                      AND lv.""Code"" = 'UNSPECIFIED'
                    LIMIT 1
                )
                AND (a.""AssetType"" IS NULL OR TRIM(a.""AssetType"") = '');
            ");
        }
    }
}
