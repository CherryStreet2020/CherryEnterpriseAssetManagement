using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.5 — NumberSequence table (SAP NRIV pattern).
    //
    // One row per (Classification, Year, TenantId) holding the
    // monotonically-incrementing counter that drives WO numbering.
    //
    // Unique index on (Classification, Year, COALESCE(TenantId, 0))
    // prevents duplicate-row creation under race conditions while
    // letting global (NULL tenant) + tenant-specific rows coexist
    // for the same (classification, year).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddNumberSequence")]
    public partial class AddNumberSequence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""NumberSequence"" (
                    ""Id""                SERIAL PRIMARY KEY,
                    ""Classification""    smallint    NOT NULL,
                    ""Year""              integer     NOT NULL,
                    ""Prefix""            varchar(8)  NOT NULL,
                    ""CurrentValue""      integer     NOT NULL DEFAULT 0,
                    ""Padding""           integer     NOT NULL DEFAULT 4,
                    ""YearSeparator""     varchar(2)  NOT NULL DEFAULT '-',
                    ""CounterSeparator""  varchar(2)  NOT NULL DEFAULT '-',
                    ""TenantId""          integer     NULL,
                    ""CreatedAt""         timestamptz NOT NULL DEFAULT now(),
                    ""UpdatedAt""         timestamptz NOT NULL DEFAULT now()
                );
            ");

            // Unique index using the COALESCE trick to let NULL tenant
            // (global default) and a specific tenant id coexist for the
            // same (Classification, Year) bucket.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS
                    ""IX_NumberSequence_Classification_Year_TenantId""
                ON ""NumberSequence""
                    (""Classification"", ""Year"", COALESCE(""TenantId"", 0));
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_NumberSequence_Classification_Year_TenantId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""NumberSequence"";");
        }
    }
}
