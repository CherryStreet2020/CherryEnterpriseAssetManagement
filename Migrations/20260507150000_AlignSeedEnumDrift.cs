using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Aligns five drifted seed/enum pairs and inserts the missing
    // InventoryStatus type. Discovered during the FK-bound dropdown
    // migration audit on 2026-05-07.
    //
    // Why: a FK lookup that resolves a LookupValue by its Code (the enum
    // int as a string) was returning the wrong row in 5 of 17 status-like
    // enums because the seed JSON had drifted away from the C# enum
    // definition (the source of truth for the running app).
    //
    // The LookupSeedPipeline at Services/Seeding/Pipelines/LookupSeedPipeline.cs
    // is an UPSERT (FindByNaturalKey on (LookupTypeId, Code), then
    // ShouldUpdate on Name/SortOrder), so on a fresh install the JSON
    // changes alone fix the problem. This migration is the durable belt-
    // and-suspenders fix for environments where the seed pipeline may
    // not run on every startup, AND it's required for the cases below
    // where Code itself has to change (string → int): the seed pipeline
    // would otherwise insert a parallel row with the new int code and
    // leave the old string-coded row as an orphan.
    //
    // Idempotent: safe to re-run.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260507150000_AlignSeedEnumDrift")]
    public partial class AlignSeedEnumDrift : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── AssetStatus ──────────────────────────────────────────
            // Drift: code "1" was named "Inactive" but enum value 1 is
            // FullyDepreciated. Codes 4/5/6 (WrittenOff/Impaired/Held)
            // were missing entirely.
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Fully Depreciated',
                    ""SortOrder"" = 2,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '1'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'AssetStatus');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues""
                    (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", '4', 'Written Off', 5, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                WHERE lt.""Key"" = 'AssetStatus'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = '4'
                  );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues""
                    (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", '5', 'Impaired', 6, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                WHERE lt.""Key"" = 'AssetStatus'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = '5'
                  );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues""
                    (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", '6', 'Held', 7, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                WHERE lt.""Key"" = 'AssetStatus'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = '6'
                  );
            ");

            // ── CipProjectStatus ─────────────────────────────────────
            // Drift: every name except code "2" (On Hold) was assigned
            // to the wrong enum slot. Re-label in place.
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Planned', ""SortOrder"" = 1, ""Metadata"" = NULL,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '0'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Active', ""SortOrder"" = 2,
                    ""Metadata"" = '{""AllowCosts"":true}'::jsonb,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '1'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Completed', ""SortOrder"" = 4, ""Metadata"" = NULL,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '3'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Cancelled', ""SortOrder"" = 5,
                    ""Metadata"" = '{""IsTerminal"":true}'::jsonb,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '4'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Capitalized', ""SortOrder"" = 6,
                    ""Metadata"" = '{""IsTerminal"":true}'::jsonb,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '5'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");

            // ── ItemStatus ───────────────────────────────────────────
            // Drift: code "3" name was "Pending" — enum value 3 is
            // PendingApproval. Code "4" (Discontinued) was missing.
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Pending Approval',
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '3'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'ItemStatus');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues""
                    (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", '4', 'Discontinued', 5, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                WHERE lt.""Key"" = 'ItemStatus'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = '4'
                  );
            ");

            // ── WorkRequestStatus ────────────────────────────────────
            // Drift: codes were strings ("New", "UnderReview", "Approved",
            // "Rejected", "Completed") instead of the enum's int values.
            // Rename codes in place to preserve any (unlikely) FK refs.
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Code"" = '0', ""Name"" = 'New', ""SortOrder"" = 1,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = 'New'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Code"" = '1', ""Name"" = 'In Review', ""SortOrder"" = 2,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = 'UnderReview'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Code"" = '2', ""Name"" = 'Approved', ""SortOrder"" = 3,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = 'Approved'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Code"" = '3', ""Name"" = 'Rejected', ""SortOrder"" = 4,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = 'Rejected'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");

            // 'Completed' was an orphan (not present in C# enum). Remove.
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupValues""
                WHERE ""Code"" = 'Completed'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues""
                    (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", '4', 'Converted to WO', 5, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                WHERE lt.""Key"" = 'WorkRequestStatus'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = '4'
                  );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues""
                    (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", '5', 'Cancelled', 6, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                WHERE lt.""Key"" = 'WorkRequestStatus'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = '5'
                  );
            ");

            // ── VendorStatus ─────────────────────────────────────────
            // Drift: codes were SCREAMING_SNAKE strings ("ACTIVE",
            // "INACTIVE", "PENDING", "SUSPENDED") instead of the enum's
            // int values. Names also drifted (PENDING → enum OnHold,
            // SUSPENDED → enum Blocked).
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Code"" = '0', ""Name"" = 'Active', ""SortOrder"" = 1,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = 'ACTIVE'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'VendorStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Code"" = '1', ""Name"" = 'Inactive', ""SortOrder"" = 2,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = 'INACTIVE'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'VendorStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Code"" = '2', ""Name"" = 'On Hold', ""SortOrder"" = 3,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = 'PENDING'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'VendorStatus');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Code"" = '3', ""Name"" = 'Blocked', ""SortOrder"" = 4,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = 'SUSPENDED'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'VendorStatus');
            ");

            // ── InventoryStatus ──────────────────────────────────────
            // No seed file existed before this PR; the JSON has been
            // added. Insert the LookupType + 5 values for environments
            // where the seed pipeline doesn't run.
            migrationBuilder.Sql(@"
                INSERT INTO ""LookupTypes""
                    (""Key"", ""Name"", ""IsSystem"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT 'InventoryStatus', 'Inventory Status', true, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""LookupTypes"" WHERE ""Key"" = 'InventoryStatus'
                );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues""
                    (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", v.code, v.name, v.sort_order, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                CROSS JOIN (VALUES
                    ('0', 'Draft',       1),
                    ('1', 'In Progress', 2),
                    ('2', 'Completed',   3),
                    ('3', 'Cancelled',   4),
                    ('4', 'Reconciled',  5)
                ) AS v(code, name, sort_order)
                WHERE lt.""Key"" = 'InventoryStatus'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = v.code
                  );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── InventoryStatus ──────────────────────────────────────
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupValues""
                WHERE ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'InventoryStatus');
            ");
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupTypes"" WHERE ""Key"" = 'InventoryStatus';
            ");

            // ── VendorStatus ─────────────────────────────────────────
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Code"" = 'SUSPENDED', ""Name"" = 'Suspended'
                WHERE ""Code"" = '3' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'VendorStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Code"" = 'PENDING', ""Name"" = 'Pending Approval'
                WHERE ""Code"" = '2' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'VendorStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Code"" = 'INACTIVE'
                WHERE ""Code"" = '1' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'VendorStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Code"" = 'ACTIVE'
                WHERE ""Code"" = '0' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'VendorStatus');
            ");

            // ── WorkRequestStatus ────────────────────────────────────
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupValues""
                WHERE ""Code"" IN ('4','5')
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues"" (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", 'Completed', 'Completed', 5, true, (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                WHERE lt.""Key"" = 'WorkRequestStatus'
                  AND NOT EXISTS (SELECT 1 FROM ""LookupValues"" lv WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = 'Completed');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Code"" = 'Rejected'
                WHERE ""Code"" = '3' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Code"" = 'Approved'
                WHERE ""Code"" = '2' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Code"" = 'UnderReview', ""Name"" = 'Under Review'
                WHERE ""Code"" = '1' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Code"" = 'New'
                WHERE ""Code"" = '0' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'WorkRequestStatus');
            ");

            // ── ItemStatus ───────────────────────────────────────────
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupValues""
                WHERE ""Code"" = '4'
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'ItemStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Name"" = 'Pending'
                WHERE ""Code"" = '3' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'ItemStatus');
            ");

            // ── CipProjectStatus ─────────────────────────────────────
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Name"" = 'Capitalized', ""SortOrder"" = 5, ""Metadata"" = '{""IsTerminal"":true}'::jsonb
                WHERE ""Code"" = '5' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Name"" = 'Capitalized', ""SortOrder"" = 5, ""Metadata"" = '{""IsTerminal"":true}'::jsonb
                WHERE ""Code"" = '4' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Name"" = 'Cancelled', ""SortOrder"" = 4, ""Metadata"" = '{""IsTerminal"":true}'::jsonb
                WHERE ""Code"" = '3' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Name"" = 'Completed', ""SortOrder"" = 2, ""Metadata"" = NULL
                WHERE ""Code"" = '1' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Name"" = 'Active', ""SortOrder"" = 1, ""Metadata"" = '{""AllowCosts"":true}'::jsonb
                WHERE ""Code"" = '0' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus');
            ");

            // ── AssetStatus ──────────────────────────────────────────
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupValues""
                WHERE ""Code"" IN ('4','5','6')
                  AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'AssetStatus');
            ");
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues"" SET ""Name"" = 'Inactive', ""SortOrder"" = 2
                WHERE ""Code"" = '1' AND ""LookupTypeId"" IN (SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'AssetStatus');
            ");
        }
    }
}
