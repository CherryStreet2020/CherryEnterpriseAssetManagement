using Microsoft.EntityFrameworkCore.Migrations;

namespace Abs.FixedAssets.Migrations
{
    public partial class FixAssetTypeAndPriorityBackfill : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""AssetTypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'AssetType'
                  AND UPPER(lv.""Code"") = UPPER(a.""AssetType"")
                  AND a.""AssetTypeLookupValueId"" IS NULL
                  AND a.""AssetType"" IS NOT NULL
                  AND a.""AssetType"" <> '';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""AssetTypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'AssetType'
                  AND UPPER(lv.""Name"") = UPPER(a.""AssetType"")
                  AND a.""AssetTypeLookupValueId"" IS NULL
                  AND a.""AssetType"" IS NOT NULL
                  AND a.""AssetType"" <> '';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""AssetPriorityLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'AssetPriority'
                  AND lv.""Code"" = CAST(a.""Priority"" AS TEXT)
                  AND a.""AssetPriorityLookupValueId"" IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Assets"" SET ""AssetTypeLookupValueId"" = NULL;
            ");
            migrationBuilder.Sql(@"
                UPDATE ""Assets"" SET ""AssetPriorityLookupValueId"" = NULL;
            ");
        }
    }
}
