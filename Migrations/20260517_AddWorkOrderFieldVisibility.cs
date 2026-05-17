using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.2 — WorkOrderFieldVisibility config table.
    //
    // SAP-OIAN-pattern field-selection table. One row per
    // (Classification × FieldName) with Visibility, ordering, section
    // grouping, and optional tenant override. Powers the renderer in
    // Phase F without per-classification code branching.
    //
    // Indexes:
    //   - (Classification, FieldName, TenantId) UNIQUE — one rule per
    //     tuple. Tenant rows can override the global (TenantId IS NULL)
    //     row without colliding because the unique constraint includes
    //     TenantId.
    //   - (Classification, TenantId) — drives the bulk read pattern
    //     used by IWorkOrderFieldVisibilityService.GetLayoutAsync.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddWorkOrderFieldVisibility")]
    public partial class AddWorkOrderFieldVisibility : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WorkOrderFieldVisibility"" (
                    ""Id""              SERIAL PRIMARY KEY,
                    ""Classification""  smallint    NOT NULL,
                    ""FieldName""       varchar(80) NOT NULL,
                    ""Visibility""      smallint    NOT NULL DEFAULT 1,
                    ""DisplayLabel""    varchar(80) NULL,
                    ""DisplayOrder""    integer     NOT NULL DEFAULT 100,
                    ""SectionName""     varchar(40) NOT NULL DEFAULT 'Other',
                    ""HelpText""        varchar(500) NULL,
                    ""ValidationHint""  varchar(200) NULL,
                    ""TenantId""        integer     NULL,
                    ""CreatedAt""       timestamptz NOT NULL DEFAULT now(),
                    ""UpdatedAt""       timestamptz NOT NULL DEFAULT now()
                );
            ");

            // UNIQUE — one rule per (Classification, FieldName, Tenant).
            // The COALESCE trick lets a NULL TenantId and a specific
            // TenantId coexist for the same (Classification, FieldName)
            // — Postgres treats NULLs as distinct in UNIQUE indexes,
            // which is the right behavior here (tenant override wins).
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS
                    ""IX_WorkOrderFieldVisibility_Classification_FieldName_TenantId""
                ON ""WorkOrderFieldVisibility""
                    (""Classification"", ""FieldName"", COALESCE(""TenantId"", 0));
            ");

            // Bulk-read index for the service's GetLayoutAsync call.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS
                    ""IX_WorkOrderFieldVisibility_Classification_TenantId""
                ON ""WorkOrderFieldVisibility"" (""Classification"", ""TenantId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_WorkOrderFieldVisibility_Classification_TenantId"";
            ");
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_WorkOrderFieldVisibility_Classification_FieldName_TenantId"";
            ");
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS ""WorkOrderFieldVisibility"";
            ");
        }
    }
}
