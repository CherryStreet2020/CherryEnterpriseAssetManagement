using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-015 / Migration PR #2 — Backfill + NOT NULL on StockReceipts.ProfileId.
    //
    // Migration PR #1 (20260518_AddReceiptProfileCatalog) shipped the
    // additive schema with `StockReceipts.ProfileId int? NULL`. This
    // migration finishes the transition by:
    //   1. Backfilling any existing rows that lack a ProfileId — assigning
    //      them the STEEL profile (the only profile the PR #219 form
    //      populates real fields for). Production currently has ZERO rows
    //      so this is a no-op, but the SQL is here for safety in case any
    //      env has data.
    //   2. Adding NOT NULL constraint on ProfileId so every future receipt
    //      MUST carry a profile. The service layer (StockReceiptService)
    //      was updated in the same PR to set ProfileId on every Create.
    //
    // What this migration does NOT do:
    //   - Touch the legacy sheet-metal columns (HeatNumber, MillCertUrl,
    //     Mill, Length/Width/Thickness, UsableLength/Width). They keep
    //     working in parallel with Attributes JSON for one sprint of
    //     dual-write safety net.
    //   - Drop those columns (deferred to Migration PR #3 alongside the
    //     UiFormSpec-driven Razor page rewrite).
    //   - Backfill historical rows' Attributes JSONB. Production has 0
    //     rows; if a sandbox env has rows, run the manual backfill in
    //     §B below before applying this migration.
    //
    // Manual backfill SQL (for any env that has pre-migration rows):
    //
    //   §A — Set ProfileId for everyone:
    //     UPDATE "StockReceipts" sr
    //     SET "ProfileId" = (SELECT "Id" FROM "ReceiptProfiles" WHERE "Code" = 'STEEL')
    //     WHERE "ProfileId" IS NULL;
    //
    //   §B — Build Attributes JSON from legacy columns:
    //     UPDATE "StockReceipts"
    //     SET "Attributes" = jsonb_strip_nulls(jsonb_build_object(
    //         'heatNumber',     "HeatNumber",
    //         'mill',           "Mill",
    //         'millCertUrl',    "MillCertUrl",
    //         'lengthMm',       "LengthMm",
    //         'widthMm',        "WidthMm",
    //         'thicknessMm',    "ThicknessMm",
    //         'usableLengthMm', "UsableLengthMm",
    //         'usableWidthMm',  "UsableWidthMm"))
    //     WHERE "Attributes" IS NULL;
    //
    // The migration runs §A defensively (it's a no-op against 0 rows and
    // idempotent against partial backfills) so prod, sandbox, and CI all
    // converge to the same NOT NULL state.
    //
    // Reference: ADR-015 §D9 step 7 + StockReceiptService dual-write
    // (BuildSteelAttributesJson{,FromUpdate}).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260518_AddProfileIdNotNullOnStockReceipts")]
    public partial class AddProfileIdNotNullOnStockReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // §A — Defensive backfill of ProfileId for any null rows.
            // No-op against 0 rows. Always safe to re-run.
            migrationBuilder.Sql(@"
                UPDATE ""StockReceipts"" sr
                SET ""ProfileId"" = (
                    SELECT ""Id"" FROM ""ReceiptProfiles""
                    WHERE ""Code"" = 'STEEL'
                    LIMIT 1
                )
                WHERE sr.""ProfileId"" IS NULL;
            ");

            // §B — Defensive backfill of Attributes for any null rows
            // that have at least one legacy steel-specific column populated.
            // No-op against 0 rows. Always safe to re-run because we only
            // touch rows where Attributes IS NULL.
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
                      ""HeatNumber""   IS NOT NULL OR
                      ""Mill""         IS NOT NULL OR
                      ""MillCertUrl""  IS NOT NULL OR
                      ""LengthMm""     IS NOT NULL OR
                      ""WidthMm""      IS NOT NULL OR
                      ""ThicknessMm""  IS NOT NULL
                  );
            ");

            // §C — Hard requirement: every receipt has a profile.
            // After §A this is guaranteed; the ALTER will fail loudly if
            // somehow not (e.g., the STEEL profile is missing).
            migrationBuilder.Sql(@"
                ALTER TABLE ""StockReceipts""
                ALTER COLUMN ""ProfileId"" SET NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Roll back: drop the NOT NULL constraint. We do NOT undo the
            // backfill — Attributes JSONB is purely additive and dropping
            // ProfileId values would be data loss.
            migrationBuilder.Sql(@"
                ALTER TABLE ""StockReceipts""
                ALTER COLUMN ""ProfileId"" DROP NOT NULL;
            ");
        }
    }
}
