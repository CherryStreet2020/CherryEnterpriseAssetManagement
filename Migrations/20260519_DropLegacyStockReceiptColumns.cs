using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-015 / Migration PR #3 — Drop legacy steel-specific columns.
    //
    // After Migration PR #1 (additive schema) and Migration PR #2
    // (ProfileId backfill + NOT NULL), every receipt carries its industry-
    // specific payload in Attributes jsonb. The 8 steel-specific columns
    // are now redundant and the same-PR Razor refactor (UiFormSpec-driven
    // dynamic form) no longer references them.
    //
    // What this migration drops:
    //   HeatNumber, MillCertUrl, Mill,
    //   LengthMm, WidthMm, ThicknessMm,
    //   UsableLengthMm, UsableWidthMm
    //
    // Safety: §A defensively backfills Attributes from any row that still
    // has populated legacy columns but a NULL Attributes payload. Migration
    // PR #2 already ran this against production (0 rows; no-op), but the
    // SQL here makes the column drop safe even if some sandbox env skipped
    // PR #2's backfill.
    //
    // Down(): re-adds the 8 columns (nullable, no defaults) and best-
    // effort hydrates them from Attributes ->> '<key>' jsonb reads. Loss-
    // less for any row whose Attributes was sourced from PR #2's backfill;
    // for net-new PR #3 rows, the JSONB extract reproduces the data.
    //
    // Reference: docs/research/dynamic-razor-form-spec.md §8.15
    [DbContext(typeof(AppDbContext))]
    [Migration("20260519_DropLegacyStockReceiptColumns")]
    public partial class DropLegacyStockReceiptColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // §A — Defensive backfill of Attributes from legacy columns
            // for any row that still has NULL Attributes. No-op against
            // 0 rows. Always safe to re-run.
            migrationBuilder.Sql(@"
                UPDATE ""StockReceipts""
                SET ""Attributes"" = jsonb_strip_nulls(jsonb_build_object(
                    'heatNumber',     ""HeatNumber"",
                    'mill',           ""Mill"",
                    'millCertUrl',    ""MillCertUrl"",
                    'lengthMm',       ""LengthMm"",
                    'widthMm',        ""WidthMm"",
                    'thicknessMm',    ""ThicknessMm"",
                    'usableLengthMm', ""UsableLengthMm"",
                    'usableWidthMm',  ""UsableWidthMm""))
                WHERE ""Attributes"" IS NULL
                  AND (
                      ""HeatNumber""     IS NOT NULL OR
                      ""Mill""           IS NOT NULL OR
                      ""MillCertUrl""    IS NOT NULL OR
                      ""LengthMm""       IS NOT NULL OR
                      ""WidthMm""        IS NOT NULL OR
                      ""ThicknessMm""    IS NOT NULL OR
                      ""UsableLengthMm"" IS NOT NULL OR
                      ""UsableWidthMm""  IS NOT NULL
                  );
            ");

            // §B — Drop the 8 legacy columns. Postgres DROP COLUMN runs
            // inside the migration transaction; if any of the drops fail
            // they all roll back together.
            migrationBuilder.DropColumn(name: "HeatNumber",      table: "StockReceipts");
            migrationBuilder.DropColumn(name: "MillCertUrl",     table: "StockReceipts");
            migrationBuilder.DropColumn(name: "Mill",            table: "StockReceipts");
            migrationBuilder.DropColumn(name: "LengthMm",        table: "StockReceipts");
            migrationBuilder.DropColumn(name: "WidthMm",         table: "StockReceipts");
            migrationBuilder.DropColumn(name: "ThicknessMm",     table: "StockReceipts");
            migrationBuilder.DropColumn(name: "UsableLengthMm",  table: "StockReceipts");
            migrationBuilder.DropColumn(name: "UsableWidthMm",   table: "StockReceipts");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add the 8 columns nullable; do a best-effort backfill
            // from Attributes JSONB. Lossy only for rows that were edited
            // through the dynamic form between Up() and Down() with values
            // that don't map cleanly to the legacy column types.
            migrationBuilder.AddColumn<string>(
                name: "HeatNumber", table: "StockReceipts",
                type: "character varying(64)", maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "MillCertUrl", table: "StockReceipts",
                type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Mill", table: "StockReceipts",
                type: "character varying(128)", maxLength: 128, nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "LengthMm", table: "StockReceipts",
                type: "numeric(10,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "WidthMm", table: "StockReceipts",
                type: "numeric(10,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "ThicknessMm", table: "StockReceipts",
                type: "numeric(10,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "UsableLengthMm", table: "StockReceipts",
                type: "numeric(10,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "UsableWidthMm", table: "StockReceipts",
                type: "numeric(10,2)", nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""StockReceipts"" SET
                    ""HeatNumber""      = (""Attributes"" ->> 'heatNumber'),
                    ""Mill""            = (""Attributes"" ->> 'mill'),
                    ""MillCertUrl""     = (""Attributes"" ->> 'millCertUrl'),
                    ""LengthMm""        = NULLIF(""Attributes"" ->> 'lengthMm', '')::numeric(10,2),
                    ""WidthMm""         = NULLIF(""Attributes"" ->> 'widthMm', '')::numeric(10,2),
                    ""ThicknessMm""     = NULLIF(""Attributes"" ->> 'thicknessMm', '')::numeric(10,2),
                    ""UsableLengthMm""  = NULLIF(""Attributes"" ->> 'usableLengthMm', '')::numeric(10,2),
                    ""UsableWidthMm""   = NULLIF(""Attributes"" ->> 'usableWidthMm', '')::numeric(10,2)
                WHERE ""Attributes"" IS NOT NULL;
            ");
        }
    }
}
