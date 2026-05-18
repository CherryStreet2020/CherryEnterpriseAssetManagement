using System;
using System.IO;
using System.Reflection;
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-015 / Migration PR #1 — Industry-agnostic receipt schema (additive).
    //
    // This migration is ADDITIVE ONLY. Zero breakage. The PR #219 admin
    // CRUD continues to work unchanged against the legacy sheet-metal
    // columns (HeatNumber, MillCertUrl, Mill, Length/Width/Thickness,
    // UsableLength/Width). The new ProfileId / Attributes / SerialNumber
    // columns are nullable. The dual-write transition window lasts one
    // sprint; Migration PR #2 will backfill existing Steel rows, and
    // Migration PR #3 will drop the legacy columns and flip the Edit UI
    // to render dynamically from the active profile's UiFormSpec.
    //
    // What this migration creates:
    //   1. ReceiptProfiles table — config-driven schema definition per
    //      industry vertical (12 starter profiles seeded).
    //   2. StockReceipts.ProfileId (FK), .SerialNumber, .Attributes jsonb.
    //   3. Items.DefaultReceiptProfileId (FK), .DefaultReceiptAttributes jsonb.
    //   4. Vendors.DefaultReceiptAttributes jsonb, .SendsAsn, .AsnFormat.
    //   5. Expression indexes on hot per-profile facets:
    //        heatNumber, mill (STEEL)
    //        expirationDate, ndc, gtin (PHARMA)
    //        traceabilityLotCode (FOOD)
    //        metrcTag (CANNABIS)
    //        mslLevel (ELECTRONICS)
    //        udiDi (MEDICAL_DEVICE)
    //   6. GIN index on StockReceipts.Attributes for long-tail containment
    //      queries.
    //   7. Postgres CHECK constraint `jsonb_typeof(Attributes) = 'object'`
    //      backstops against schema drift.
    //   8. Seed: 12 starter ReceiptProfiles via embedded
    //      Migrations/Seeds/ReceiptProfilesSeed.sql.
    //
    // What this migration does NOT do:
    //   - Backfill existing StockReceipt rows (deferred to Migration PR #2).
    //   - Add NOT NULL to ProfileId (deferred to Migration PR #2).
    //   - Drop the 8 legacy sheet-metal columns (deferred to Migration PR #3).
    //   - Change the Razor admin UI (deferred to Migration PR #3 + the
    //     /Receiving/Inbox wizard work).
    //
    // Reference: ADR-015 §"Decisions" D1, D2, D3, D5, D9 + D10 spike result.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260518_AddReceiptProfileCatalog")]
    public partial class AddReceiptProfileCatalog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1) ReceiptProfiles table ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ReceiptProfiles"" (
                    ""Id""                   serial       PRIMARY KEY,
                    ""Code""                 varchar(64)  NOT NULL UNIQUE,
                    ""Name""                 varchar(128) NOT NULL,
                    ""Description""          varchar(500),
                    ""JsonSchema""           jsonb        NOT NULL DEFAULT '{}'::jsonb,
                    ""PromotedFacets""       jsonb        NOT NULL DEFAULT '[]'::jsonb,
                    ""DefaultAttributes""    jsonb        NOT NULL DEFAULT '{}'::jsonb,
                    ""UiFormSpec""           jsonb        NOT NULL DEFAULT '{}'::jsonb,
                    ""RegulatoryProfileIds"" jsonb        NOT NULL DEFAULT '[]'::jsonb,
                    ""IsActive""             boolean      NOT NULL DEFAULT TRUE,
                    ""CreatedAt""            timestamptz  NOT NULL DEFAULT now(),
                    ""CreatedBy""            varchar(100),
                    ""ModifiedAt""           timestamptz,
                    ""ModifiedBy""           varchar(100)
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ReceiptProfiles_IsActive""
                ON ""ReceiptProfiles"" (""IsActive"") WHERE ""IsActive"" = TRUE;
            ");

            // ---------- 2) StockReceipts additive columns ----------
            // All nullable for the dual-write transition window.
            migrationBuilder.Sql(@"
                ALTER TABLE ""StockReceipts""
                ADD COLUMN IF NOT EXISTS ""ProfileId""    integer NULL
                    REFERENCES ""ReceiptProfiles"" (""Id"") ON DELETE RESTRICT,
                ADD COLUMN IF NOT EXISTS ""SerialNumber"" varchar(128) NULL,
                ADD COLUMN IF NOT EXISTS ""Attributes""   jsonb NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_ProfileId""
                ON ""StockReceipts"" (""ProfileId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_SerialNumber""
                ON ""StockReceipts"" (""SerialNumber"") WHERE ""SerialNumber"" IS NOT NULL;
            ");

            // jsonb_typeof CHECK constraint backstops against rogue writes
            // that put a non-object into Attributes (string/number/array).
            // Allow NULL because Attributes is nullable until Migration PR #2.
            migrationBuilder.Sql(@"
                ALTER TABLE ""StockReceipts""
                DROP CONSTRAINT IF EXISTS ""CK_StockReceipts_Attributes_IsObject"";
                ALTER TABLE ""StockReceipts""
                ADD CONSTRAINT ""CK_StockReceipts_Attributes_IsObject""
                CHECK (""Attributes"" IS NULL OR jsonb_typeof(""Attributes"") = 'object');
            ");

            // ---------- 3) Expression indexes on hot per-profile facets ----------
            // Per ADR-015 D3 + the §4.5 perf trick. Equality / range queries
            // against these JSONB keys hit a B-tree, not a JSONB scan.
            //
            // STEEL / AEROSPACE / OIL_GAS hot facets:
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_HeatNumber""
                ON ""StockReceipts"" ((""Attributes"" ->> 'heatNumber'))
                WHERE ""Attributes"" IS NOT NULL;
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_Mill""
                ON ""StockReceipts"" ((""Attributes"" ->> 'mill'))
                WHERE ""Attributes"" IS NOT NULL;
            ");

            // PHARMA hot facets — expirationDate. NOTE: We index the TEXT
            // value, NOT a date-cast. The text::date cast is STABLE (not
            // IMMUTABLE) in PostgreSQL because date parsing depends on the
            // session DateStyle/locale — Postgres rejects STABLE expressions
            // in index DDL. Because the JSON Schema requires ISO-8601
            // (YYYY-MM-DD) date strings, lexicographic ordering on the text
            // matches calendar ordering exactly, so range queries like
            // `WHERE Attributes ->> 'expirationDate' <= '2026-06-17'` still
            // hit the index and return the right rows. If we ever need true
            // date semantics at query time, use to_date(value, 'YYYY-MM-DD')
            // — to_date is IMMUTABLE with an explicit format.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_ExpirationDate""
                ON ""StockReceipts"" ((""Attributes"" ->> 'expirationDate'))
                WHERE ""Attributes"" IS NOT NULL
                  AND (""Attributes"" ->> 'expirationDate') IS NOT NULL;
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_Ndc""
                ON ""StockReceipts"" ((""Attributes"" ->> 'ndc'))
                WHERE ""Attributes"" IS NOT NULL;
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_Gtin""
                ON ""StockReceipts"" ((""Attributes"" ->> 'gtin'))
                WHERE ""Attributes"" IS NOT NULL;
            ");

            // FOOD hot facet:
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_Tlc""
                ON ""StockReceipts"" ((""Attributes"" ->> 'traceabilityLotCode'))
                WHERE ""Attributes"" IS NOT NULL;
            ");

            // CANNABIS hot facet:
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_MetrcTag""
                ON ""StockReceipts"" ((""Attributes"" ->> 'metrcTag'))
                WHERE ""Attributes"" IS NOT NULL;
            ");

            // ELECTRONICS hot facet — mslLevel cast to int for numeric filtering:
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_MslLevel""
                ON ""StockReceipts"" (((""Attributes"" ->> 'mslLevel')::int))
                WHERE ""Attributes"" IS NOT NULL
                  AND (""Attributes"" ->> 'mslLevel') IS NOT NULL;
            ");

            // MEDICAL_DEVICE hot facet:
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attr_UdiDi""
                ON ""StockReceipts"" ((""Attributes"" ->> 'udiDi'))
                WHERE ""Attributes"" IS NOT NULL;
            ");

            // ---------- 4) GIN index on Attributes for containment queries ----------
            // Use default jsonb_ops (not jsonb_path_ops) — supports both
            // key-only queries and containment matches across the long tail.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_StockReceipts_Attributes_Gin""
                ON ""StockReceipts"" USING gin (""Attributes"")
                WHERE ""Attributes"" IS NOT NULL;
            ");

            // ---------- 5) Items additive columns ----------
            migrationBuilder.Sql(@"
                ALTER TABLE ""Items""
                ADD COLUMN IF NOT EXISTS ""DefaultReceiptProfileId""  integer NULL
                    REFERENCES ""ReceiptProfiles"" (""Id"") ON DELETE SET NULL,
                ADD COLUMN IF NOT EXISTS ""DefaultReceiptAttributes"" jsonb   NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Items_DefaultReceiptProfileId""
                ON ""Items"" (""DefaultReceiptProfileId"")
                WHERE ""DefaultReceiptProfileId"" IS NOT NULL;
            ");

            // ---------- 6) Vendors additive columns ----------
            migrationBuilder.Sql(@"
                ALTER TABLE ""Vendors""
                ADD COLUMN IF NOT EXISTS ""DefaultReceiptAttributes"" jsonb        NULL,
                ADD COLUMN IF NOT EXISTS ""SendsAsn""                 boolean      NOT NULL DEFAULT FALSE,
                ADD COLUMN IF NOT EXISTS ""AsnFormat""                varchar(32)  NULL;
            ");

            // ---------- 7) Seed the 12 starter ReceiptProfiles ----------
            // Loaded from the embedded resource Migrations/Seeds/ReceiptProfilesSeed.sql.
            // INSERT statements are idempotent (ON CONFLICT (Code) DO NOTHING),
            // so running the migration twice is safe.
            var seedSql = LoadEmbeddedSeed();
            migrationBuilder.Sql(seedSql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse order for safe rollback.

            // 6) Vendors columns
            migrationBuilder.Sql(@"
                ALTER TABLE ""Vendors""
                DROP COLUMN IF EXISTS ""AsnFormat"",
                DROP COLUMN IF EXISTS ""SendsAsn"",
                DROP COLUMN IF EXISTS ""DefaultReceiptAttributes"";
            ");

            // 5) Items columns
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Items_DefaultReceiptProfileId"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Items""
                DROP COLUMN IF EXISTS ""DefaultReceiptAttributes"",
                DROP COLUMN IF EXISTS ""DefaultReceiptProfileId"";
            ");

            // 4) GIN index
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attributes_Gin"";");

            // 3) Expression indexes
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_UdiDi"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_MslLevel"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_MetrcTag"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_Tlc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_Gtin"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_Ndc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_ExpirationDate"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_Mill"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_Attr_HeatNumber"";");

            // 2) StockReceipts columns + constraint
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_SerialNumber"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_StockReceipts_ProfileId"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""StockReceipts""
                DROP CONSTRAINT IF EXISTS ""CK_StockReceipts_Attributes_IsObject"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""StockReceipts""
                DROP COLUMN IF EXISTS ""Attributes"",
                DROP COLUMN IF EXISTS ""SerialNumber"",
                DROP COLUMN IF EXISTS ""ProfileId"";
            ");

            // 1) ReceiptProfiles table
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ReceiptProfiles_IsActive"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ReceiptProfiles"" CASCADE;");
        }

        // Reads the embedded ReceiptProfilesSeed.sql resource into a string.
        // The .csproj must include <EmbeddedResource Include="Migrations\Seeds\ReceiptProfilesSeed.sql" />.
        private static string LoadEmbeddedSeed()
        {
            var assembly = typeof(AddReceiptProfileCatalog).Assembly;
            // Resource name format: <RootNamespace>.<DotPath>.<Filename>
            // For "Abs.FixedAssets" root namespace + "Migrations\Seeds\ReceiptProfilesSeed.sql":
            //   "Abs.FixedAssets.Migrations.Seeds.ReceiptProfilesSeed.sql"
            const string resourceName = "Abs.FixedAssets.Migrations.Seeds.ReceiptProfilesSeed.sql";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                // Defensive fallback for casing / path drift. Search by suffix.
                var candidate = Array.Find(
                    assembly.GetManifestResourceNames(),
                    n => n.EndsWith("ReceiptProfilesSeed.sql", StringComparison.OrdinalIgnoreCase));
                if (candidate is null)
                {
                    throw new InvalidOperationException(
                        "Migration 20260518_AddReceiptProfileCatalog could not locate the " +
                        "ReceiptProfilesSeed.sql embedded resource. Expected manifest name: " +
                        $"'{resourceName}'. Available resources: " +
                        string.Join(", ", assembly.GetManifestResourceNames()) +
                        ". Check that the .csproj includes " +
                        "<EmbeddedResource Include=\"Migrations\\Seeds\\ReceiptProfilesSeed.sql\" />.");
                }
                using var fallback = assembly.GetManifestResourceStream(candidate)!;
                using var fallbackReader = new StreamReader(fallback);
                return fallbackReader.ReadToEnd();
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
