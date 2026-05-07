using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Cleans up two pieces of LookupValue / LookupType residue discovered
    // after PR #6 (AlignSeedEnumDrift) ran on Replit.
    //
    // 1) CipProjectStatus uppercase orphans
    //    Five rows with string codes ('ACTIVE', 'CANCELLED', 'CAPITALIZED',
    //    'COMPLETED', 'PLANNED') exist alongside the canonical numeric
    //    codes 0-5. They're historical residue from an older seed
    //    convention; not in the current seed JSON, not in the C# enum,
    //    not referenced by the FK pattern in current code. Safe to drop.
    //
    // 2) InventoryStatus duplicate LookupType + values
    //    PR #6's migration created a new LookupType for 'InventoryStatus'
    //    with TenantId = NULL. LookupDirectSeeder.SeedAsync then ran with
    //    its own tenant-scoped existence check (lt.TenantId == tenantId
    //    AND lt.CompanyId == NULL AND lt.Key == 'InventoryStatus') which
    //    evaluates NULL == 1 as false in SQL three-valued logic — so it
    //    inserted a SECOND LookupType with TenantId = <first tenant>, plus
    //    its own 5 LookupValues. End state: 2 LookupTypes, 10 values.
    //
    //    Fix: keep the tenant-scoped one (the canonical pattern matching
    //    every other LookupType in the system), drop the NULL-tenanted
    //    one and its 5 values.
    //
    // Both cleanups are idempotent: re-running this migration on a clean
    // DB is a no-op.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260507170000_CleanupLookupOrphans")]
    public partial class CleanupLookupOrphans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── (1) CipProjectStatus uppercase orphans ───────────────
            // The DELETE will fail on FK constraint violation if any
            // ConstructionInProgressProjects row currently points to one
            // of these orphans via StatusLookupValueId. Treat that as a
            // signal — if it happens, those projects need their FK
            // re-pointed at the canonical numeric-code rows first. Today
            // we don't expect any such pointers (CIP page writes the FK
            // using the numeric codes), so this should be a clean drop.
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupValues""
                WHERE ""Code"" IN ('ACTIVE', 'CANCELLED', 'CAPITALIZED', 'COMPLETED', 'PLANNED', 'ON_HOLD')
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'CipProjectStatus'
                  );
            ");

            // ── (2) InventoryStatus duplicate cleanup ────────────────
            // Delete the 5 LookupValues under the NULL-tenanted LookupType,
            // then delete the LookupType itself. The tenant-scoped
            // LookupType + its 5 values remain as the canonical set.
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupValues""
                WHERE ""LookupTypeId"" IN (
                    SELECT ""Id"" FROM ""LookupTypes""
                    WHERE ""Key"" = 'InventoryStatus' AND ""TenantId"" IS NULL
                );
            ");

            migrationBuilder.Sql(@"
                DELETE FROM ""LookupTypes""
                WHERE ""Key"" = 'InventoryStatus' AND ""TenantId"" IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The Down operation cannot fully reverse this migration
            // because the orphan rows it removes were drift artifacts
            // from prior seed conventions — there's no canonical truth
            // about what they "should" be. The Down here is a best-
            // effort restore: it re-creates the InventoryStatus duplicate
            // LookupType (NULL-tenanted) with its 5 values so the data
            // shape matches pre-migration state. The CipProjectStatus
            // orphans are NOT restored in Down — they were strictly
            // erroneous and bringing them back has no business purpose.
            migrationBuilder.Sql(@"
                INSERT INTO ""LookupTypes""
                    (""TenantId"", ""CompanyId"", ""Key"", ""Name"", ""IsSystem"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT NULL, NULL, 'InventoryStatus', 'Inventory Status', true, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""LookupTypes""
                    WHERE ""Key"" = 'InventoryStatus' AND ""TenantId"" IS NULL
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
                WHERE lt.""Key"" = 'InventoryStatus' AND lt.""TenantId"" IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = v.code
                  );
            ");
        }
    }
}
