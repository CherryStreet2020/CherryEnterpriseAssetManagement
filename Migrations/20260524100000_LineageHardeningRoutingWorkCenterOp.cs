// Sprint 13.5 PR #5c.1 — Lineage hardening for PR #5c entities.
//
// Fixes the multi-tenant + multi-site discipline gaps Dean's audit caught:
//
//   1. WorkCenter.LocationId becomes NOT NULL (every WC physically lives at a site).
//   2. WorkCenters UNIQUE retightens from (CompanyId, Code) to (CompanyId, LocationId, Code)
//      so "CNC-1" at Site A and "CNC-1" at Site B in one company can coexist.
//   3. Routing gets LocationId NULLABLE + Routing.IsSiteWideTemplate bool. When LocationId
//      is null AND IsSiteWideTemplate=true, the routing is a company-wide engineering
//      template any site can inherit (SAP's "BOM with no plant" pattern). When LocationId
//      is set, the routing is site-scoped and a production order's LocationId must match.
//   4. Routings UNIQUE retightens to (CompanyId, COALESCE(LocationId,0), Code, RevisionNumber).
//   5. RoutingOperation gets LocationIdSnapshot int NOT NULL (copied from Routing.LocationId
//      at create time so post-create master-edit doesn't silently re-tenant the row).
//   6. ProductionOperation gets LocationIdSnapshot int NOT NULL (copied from
//      ProductionOrder.LocationId at ReleaseFromRoutingAsync time).
//
//   7. DELETE the 8 hardcoded ABS WorkCenters seeded by PR #5c migration
//      (20260524_AddRoutingWorkCenterProductionOperation.cs). Tenant data does NOT live
//      in migrations. Backfill path: tenant-aware seeder service (PR #5c.4).
//
// Backfill before NOT NULL:
//   - WorkCenter.LocationId backfill: the 8 seeded rows are DELETED in step 7, so there
//     are no orphan rows to backfill in dev. If anyone manually inserted WCs without a
//     LocationId, the ALTER COLUMN SET NOT NULL will fail (intentional — those rows
//     need cleanup first).
//   - RoutingOperation.LocationIdSnapshot: only safe because no real Routings exist yet
//     in any environment (the table was just shipped in PR #5c).
//   - ProductionOperation.LocationIdSnapshot: same — no real rows yet.
//
// Cross-refs:
//   - memory: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock 2026-05-23)
//   - memory: reference_bic_entity_checklist.md (6-point gate)
//   - Migrations/20260524_AddRoutingWorkCenterProductionOperation.cs (the PR being hardened)

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524100000_LineageHardeningRoutingWorkCenterOp")]
    public partial class LineageHardeningRoutingWorkCenterOp : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ============================================================
            // 1) DELETE the 8 hardcoded ABS demo WorkCenters from PR #5c.
            //    Tenant data does NOT belong in migrations. Re-seeded later
            //    via a tenant-aware seeder service or dev-only SQL file.
            // ============================================================
            mb.Sql(@"
                DELETE FROM ""WorkCenters""
                WHERE ""CompanyId"" = 1
                  AND ""Code"" IN ('CNC-1', 'CNC-2', 'LATHE-1', 'MILL-MAN', 'DEBURR-1', 'WELD-1', 'QC-1', 'FINAL-1');
            ");

            // ============================================================
            // 2) WorkCenter.LocationId NOT NULL + retighten UNIQUE
            // ============================================================
            // Drop the existing (CompanyId, Code) UNIQUE to make room for the new one.
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_WorkCenters_Company_Code"";");

            // Make LocationId NOT NULL. Will fail if any row has NULL LocationId
            // (intentional — clean those rows first).
            mb.Sql(@"ALTER TABLE ""WorkCenters"" ALTER COLUMN ""LocationId"" SET NOT NULL;");

            // Add the new tenant+site-prefixed UNIQUE.
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WorkCenters_Company_Location_Code""
                ON ""WorkCenters""(""CompanyId"", ""LocationId"", ""Code"");
            ");

            // ============================================================
            // 3) Routing — add LocationId (NULL) + IsSiteWideTemplate + retighten UNIQUE
            // ============================================================
            mb.Sql(@"
                ALTER TABLE ""Routings""
                    ADD COLUMN IF NOT EXISTS ""LocationId""           integer NULL,
                    ADD COLUMN IF NOT EXISTS ""IsSiteWideTemplate""   boolean NOT NULL DEFAULT FALSE;
            ");

            // CHECK: if a routing has a LocationId set, it cannot also be a site-wide template.
            mb.Sql(@"
                ALTER TABLE ""Routings""
                    ADD CONSTRAINT ""CK_Routings_SiteScopeOrTemplate""
                    CHECK (
                        (""LocationId"" IS NULL AND ""IsSiteWideTemplate"" = TRUE)
                     OR (""LocationId"" IS NOT NULL AND ""IsSiteWideTemplate"" = FALSE)
                     OR (""LocationId"" IS NULL AND ""IsSiteWideTemplate"" = FALSE)
                    );
            ");

            // Retighten the UNIQUE to include LocationId (treat NULL as 0 sentinel).
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_Routings_Company_Code_Rev"";");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Routings_Company_Location_Code_Rev""
                ON ""Routings""(""CompanyId"", COALESCE(""LocationId"", 0), ""Code"", ""RevisionNumber"");
            ");

            // ============================================================
            // 4) RoutingOperation.LocationIdSnapshot NOT NULL
            //    Safe because no real RoutingOperations exist yet in any env.
            // ============================================================
            mb.Sql(@"
                ALTER TABLE ""RoutingOperations""
                    ADD COLUMN IF NOT EXISTS ""LocationIdSnapshot"" integer NOT NULL DEFAULT 0;
            ");
            mb.Sql(@"
                ALTER TABLE ""RoutingOperations""
                    ADD CONSTRAINT ""CK_RoutingOps_LocationIdSnapshot""
                    CHECK (""LocationIdSnapshot"" >= 0);
            ");

            // ============================================================
            // 5) ProductionOperation.LocationIdSnapshot NOT NULL
            //    Same — no real rows exist yet.
            // ============================================================
            mb.Sql(@"
                ALTER TABLE ""ProductionOperations""
                    ADD COLUMN IF NOT EXISTS ""LocationIdSnapshot"" integer NOT NULL DEFAULT 0;
            ");
            mb.Sql(@"
                ALTER TABLE ""ProductionOperations""
                    ADD CONSTRAINT ""CK_ProdOps_LocationIdSnapshot""
                    CHECK (""LocationIdSnapshot"" >= 0);
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProdOps_Location_Status""
                ON ""ProductionOperations""(""LocationIdSnapshot"", ""Status"");
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_ProdOps_Location_Status"";");
            mb.Sql(@"ALTER TABLE ""ProductionOperations"" DROP CONSTRAINT IF EXISTS ""CK_ProdOps_LocationIdSnapshot"";");
            mb.Sql(@"ALTER TABLE ""ProductionOperations"" DROP COLUMN IF EXISTS ""LocationIdSnapshot"";");

            mb.Sql(@"ALTER TABLE ""RoutingOperations"" DROP CONSTRAINT IF EXISTS ""CK_RoutingOps_LocationIdSnapshot"";");
            mb.Sql(@"ALTER TABLE ""RoutingOperations"" DROP COLUMN IF EXISTS ""LocationIdSnapshot"";");

            mb.Sql(@"DROP INDEX IF EXISTS ""IX_Routings_Company_Location_Code_Rev"";");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Routings_Company_Code_Rev""
                ON ""Routings""(""CompanyId"", ""Code"", ""RevisionNumber"");
            ");
            mb.Sql(@"ALTER TABLE ""Routings"" DROP CONSTRAINT IF EXISTS ""CK_Routings_SiteScopeOrTemplate"";");
            mb.Sql(@"
                ALTER TABLE ""Routings""
                    DROP COLUMN IF EXISTS ""IsSiteWideTemplate"",
                    DROP COLUMN IF EXISTS ""LocationId"";
            ");

            mb.Sql(@"DROP INDEX IF EXISTS ""IX_WorkCenters_Company_Location_Code"";");
            mb.Sql(@"ALTER TABLE ""WorkCenters"" ALTER COLUMN ""LocationId"" DROP NOT NULL;");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WorkCenters_Company_Code""
                ON ""WorkCenters""(""CompanyId"", ""Code"");
            ");
            // Note: deleted seed rows are not re-inserted on Down. By design — they shouldn't have existed.
        }
    }
}
