using Microsoft.EntityFrameworkCore.Migrations;

namespace Abs.FixedAssets.Migrations
{
    public partial class HardenAssetTypeUnspecifiedBackfill_CompanyPreferred : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""AssetTypeLookupValueId"" = chosen.""LookupValueId""
                FROM ""Companies"" c
                CROSS JOIN LATERAL (
                    SELECT lv.""Id"" AS ""LookupValueId""
                    FROM ""LookupTypes"" lt
                    JOIN ""LookupValues"" lv ON lv.""LookupTypeId"" = lt.""Id""
                    WHERE lt.""Key"" = 'AssetType'
                      AND lv.""Code"" = 'UNSPECIFIED'
                      AND lt.""TenantId"" = c.""TenantId""
                      AND (lt.""CompanyId"" = a.""CompanyId"" OR lt.""CompanyId"" IS NULL)
                    ORDER BY
                        (lt.""CompanyId"" = a.""CompanyId"") DESC,
                        lt.""CompanyId"" NULLS LAST,
                        lt.""Id""
                    LIMIT 1
                ) chosen
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
