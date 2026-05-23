// Sprint 13.5 PR #5c.2 — Tenant Scoping Hardening (P0 cross-tenant security).
//
// AUTHORITY:
//   - memory: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock 2026-05-23)
//   - memory: reference_bic_entity_checklist.md (6-point gate)
//   - audit:  outputs/PR-5c2-audit.md
//
// WHAT THIS LANDS:
//   1. Direct CompanyId column on 6 entities that were scoping through a parent only:
//      - ProductionOrder    (CompanyId NOT NULL, backfill from Location → CustomerProject fallback)
//      - ProductionBatch    (CompanyId + LocationId NOT NULL, backfill from PrimaryEquipment/Allocation)
//      - MaterialMaster     (CompanyId + LocationId NULLABLE — cross-tenant reference data pattern)
//      - MaterialStructure  (CompanyId NOT NULL, LocationId NULL + IsSiteWideTemplate, mirrors Routing)
//      - WorkOrder          (CompanyId NOT NULL, backfill from Asset.CompanyId — defensive denorm)
//      - ProductionOperation (CompanyIdSnapshot NOT NULL — sibling to LocationIdSnapshot from PR #5c.1)
//
//   2. Fix 4 cross-tenant UNIQUE leaks (P0 — every tenant could collide on the others' codes):
//      - ProductionOrders.OrderNumber        global → (CompanyId, OrderNumber)
//      - ProductionBatches.BatchNumber       global → (CompanyId, LocationId, BatchNumber)
//      - MaterialMasters.ShopCode            global → 2 partial: system (CompanyId NULL) + tenant (CompanyId set)
//      - MaterialStructures.StructureNumber  global → 2 partial: site-scoped + company-wide template
//
//   3. Replit gotcha (PR #5c.1.1 hotfix lesson): NO COALESCE-in-index ever. Use 2 partial
//      indexes (WHERE col IS NULL / WHERE col IS NOT NULL) instead.
//
// BACKFILL ORDERING (per BIC checklist):
//   For each new NOT NULL column:
//     1) ADD COLUMN ... NOT NULL DEFAULT 0   (lets ALTER succeed without backfill)
//     2) UPDATE ... FROM parent JOIN         (backfill real values)
//     3) ALTER COLUMN ... DROP DEFAULT       (so future inserts must provide a value)
//     4) ADD CONSTRAINT CK_x_NonNeg          (deferred, allows 0s during grace period)
//
// GRACE PERIOD: deferred CHECK col >= 0 (not > 0). PR #5c.4 seeder will fill the orphan 0s
// from tenant-aware seed data and a follow-up migration will tighten to > 0.

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524120000_TenantScopingHardeningPr5c2")]
    public partial class TenantScopingHardeningPr5c2 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ============================================================
            // 1) ProductionOrder — direct CompanyId + tenant-prefixed OrderNumber UNIQUE
            // ============================================================

            mb.Sql(@"
                ALTER TABLE ""ProductionOrders""
                    ADD COLUMN IF NOT EXISTS ""CompanyId"" integer NOT NULL DEFAULT 0;
            ");

            // Backfill primary path: via Location.
            mb.Sql(@"
                UPDATE ""ProductionOrders"" po
                SET ""CompanyId"" = COALESCE(l.""CompanyId"", 0)
                FROM ""Locations"" l
                WHERE po.""LocationId"" = l.""Id""
                  AND po.""CompanyId"" = 0;
            ");

            // Backfill fallback: via CustomerProject (for orders without a Location).
            mb.Sql(@"
                UPDATE ""ProductionOrders"" po
                SET ""CompanyId"" = COALESCE(cp.""CompanyId"", po.""CompanyId"")
                FROM ""CustomerProjects"" cp
                WHERE po.""CustomerProjectId"" = cp.""Id""
                  AND po.""CompanyId"" = 0;
            ");

            mb.Sql(@"ALTER TABLE ""ProductionOrders"" ALTER COLUMN ""CompanyId"" DROP DEFAULT;");

            mb.Sql(@"
                ALTER TABLE ""ProductionOrders""
                    ADD CONSTRAINT ""CK_ProductionOrders_CompanyIdNonNeg""
                    CHECK (""CompanyId"" >= 0);
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionOrders_CompanyId""
                ON ""ProductionOrders""(""CompanyId"");
            ");

            // Replace global UNIQUE with tenant-prefixed composite.
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_OrderNumber"";");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductionOrders_Company_OrderNumber""
                ON ""ProductionOrders""(""CompanyId"", ""OrderNumber"");
            ");

            // ============================================================
            // 2) ProductionBatch — direct CompanyId + LocationId + composite BatchNumber UNIQUE
            // ============================================================

            mb.Sql(@"
                ALTER TABLE ""ProductionBatches""
                    ADD COLUMN IF NOT EXISTS ""CompanyId""  integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS ""LocationId"" integer NOT NULL DEFAULT 0;
            ");

            // Primary backfill: via PrimaryEquipment (Asset).
            mb.Sql(@"
                UPDATE ""ProductionBatches"" pb
                SET ""CompanyId""  = COALESCE(a.""CompanyId"",  0),
                    ""LocationId"" = COALESCE(a.""LocationId"", 0)
                FROM ""Assets"" a
                WHERE pb.""PrimaryEquipmentId"" = a.""Id""
                  AND pb.""CompanyId"" = 0;
            ");

            // Fallback backfill: via ProductionBatchAllocation → ProductionOrder.
            mb.Sql(@"
                UPDATE ""ProductionBatches"" pb
                SET ""CompanyId""  = COALESCE(l.""CompanyId"",  pb.""CompanyId""),
                    ""LocationId"" = COALESCE(po.""LocationId"", pb.""LocationId"")
                FROM ""ProductionBatchAllocations"" pba
                JOIN ""ProductionOrders"" po ON pba.""ProductionOrderId"" = po.""Id""
                LEFT JOIN ""Locations"" l    ON po.""LocationId""        = l.""Id""
                WHERE pba.""ProductionBatchId"" = pb.""Id""
                  AND pb.""CompanyId"" = 0;
            ");

            mb.Sql(@"ALTER TABLE ""ProductionBatches"" ALTER COLUMN ""CompanyId""  DROP DEFAULT;");
            mb.Sql(@"ALTER TABLE ""ProductionBatches"" ALTER COLUMN ""LocationId"" DROP DEFAULT;");

            mb.Sql(@"
                ALTER TABLE ""ProductionBatches""
                    ADD CONSTRAINT ""CK_ProductionBatches_CompanyIdNonNeg""  CHECK (""CompanyId""  >= 0),
                    ADD CONSTRAINT ""CK_ProductionBatches_LocationIdNonNeg"" CHECK (""LocationId"" >= 0);
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionBatches_Company_Location""
                ON ""ProductionBatches""(""CompanyId"", ""LocationId"");
            ");

            mb.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatches_BatchNumber"";");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductionBatches_Company_Location_BatchNumber""
                ON ""ProductionBatches""(""CompanyId"", ""LocationId"", ""BatchNumber"");
            ");

            // ============================================================
            // 3) MaterialMaster — nullable CompanyId/LocationId (cross-tenant reference pattern)
            //    No backfill — existing rows stay NULL = system reference.
            // ============================================================

            mb.Sql(@"
                ALTER TABLE ""MaterialMasters""
                    ADD COLUMN IF NOT EXISTS ""CompanyId""  integer NULL,
                    ADD COLUMN IF NOT EXISTS ""LocationId"" integer NULL;
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialMasters_Company""
                ON ""MaterialMasters""(""CompanyId"")
                WHERE ""CompanyId"" IS NOT NULL;
            ");

            mb.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialMasters_ShopCode"";");

            // Two partial UNIQUEs (NO COALESCE-in-index — Replit gotcha).
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialMasters_System_ShopCode""
                ON ""MaterialMasters""(""ShopCode"")
                WHERE ""CompanyId"" IS NULL;
            ");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialMasters_Company_ShopCode""
                ON ""MaterialMasters""(""CompanyId"", ""ShopCode"")
                WHERE ""CompanyId"" IS NOT NULL;
            ");

            // ============================================================
            // 4) MaterialStructure — CompanyId NOT NULL + LocationId NULL + IsSiteWideTemplate
            //    (mirrors PR #5c.1 Routing pattern for company-wide engineering templates)
            // ============================================================

            mb.Sql(@"
                ALTER TABLE ""MaterialStructures""
                    ADD COLUMN IF NOT EXISTS ""CompanyId""           integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS ""LocationId""          integer NULL,
                    ADD COLUMN IF NOT EXISTS ""IsSiteWideTemplate""  boolean NOT NULL DEFAULT FALSE;
            ");

            // No parent FK to backfill from. Grace period: rows stay at 0 until PR #5c.4 seeder fills.

            mb.Sql(@"ALTER TABLE ""MaterialStructures"" ALTER COLUMN ""CompanyId"" DROP DEFAULT;");

            mb.Sql(@"
                ALTER TABLE ""MaterialStructures""
                    ADD CONSTRAINT ""CK_MaterialStructures_CompanyIdNonNeg""
                    CHECK (""CompanyId"" >= 0);
            ");

            // Same site-or-template CHECK as Routings (PR #5c.1).
            mb.Sql(@"
                ALTER TABLE ""MaterialStructures""
                    ADD CONSTRAINT ""CK_MaterialStructures_SiteScopeOrTemplate""
                    CHECK (
                        (""LocationId"" IS NULL     AND ""IsSiteWideTemplate"" = TRUE)
                     OR (""LocationId"" IS NOT NULL AND ""IsSiteWideTemplate"" = FALSE)
                     OR (""LocationId"" IS NULL     AND ""IsSiteWideTemplate"" = FALSE)
                    );
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_MaterialStructures_Company_Location""
                ON ""MaterialStructures""(""CompanyId"", ""LocationId"");
            ");

            mb.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructures_StructureNumber"";");

            // Site-scoped UNIQUE (matches Routings_Site_Code_Rev pattern from PR #5c.1.1).
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialStructures_Site_StructureNumber_Rev""
                ON ""MaterialStructures""(""CompanyId"", ""LocationId"", ""StructureNumber"", ""Revision"")
                WHERE ""LocationId"" IS NOT NULL;
            ");

            // Company-wide template UNIQUE.
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialStructures_Template_StructureNumber_Rev""
                ON ""MaterialStructures""(""CompanyId"", ""StructureNumber"", ""Revision"")
                WHERE ""LocationId"" IS NULL;
            ");

            // ============================================================
            // 5) WorkOrder — direct CompanyId (defensive denormalization from Asset.CompanyId)
            // ============================================================

            mb.Sql(@"
                ALTER TABLE ""WorkOrders""
                    ADD COLUMN IF NOT EXISTS ""CompanyId"" integer NOT NULL DEFAULT 0;
            ");

            mb.Sql(@"
                UPDATE ""WorkOrders"" wo
                SET ""CompanyId"" = COALESCE(a.""CompanyId"", 0)
                FROM ""Assets"" a
                WHERE wo.""AssetId"" = a.""Id""
                  AND wo.""CompanyId"" = 0;
            ");

            mb.Sql(@"ALTER TABLE ""WorkOrders"" ALTER COLUMN ""CompanyId"" DROP DEFAULT;");

            mb.Sql(@"
                ALTER TABLE ""WorkOrders""
                    ADD CONSTRAINT ""CK_WorkOrders_CompanyIdNonNeg""
                    CHECK (""CompanyId"" >= 0);
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_WorkOrders_CompanyId""
                ON ""WorkOrders""(""CompanyId"");
            ");

            // ============================================================
            // 6) ProductionOperation — CompanyIdSnapshot (sibling to LocationIdSnapshot from PR #5c.1)
            //    Also unblocks the entity-file sync (LocationIdSnapshot was DB-only, no C# property —
            //    that gets added in the entity-model change of this PR so EF writes both columns).
            // ============================================================

            mb.Sql(@"
                ALTER TABLE ""ProductionOperations""
                    ADD COLUMN IF NOT EXISTS ""CompanyIdSnapshot"" integer NOT NULL DEFAULT 0;
            ");

            mb.Sql(@"
                ALTER TABLE ""ProductionOperations""
                    ADD CONSTRAINT ""CK_ProductionOperations_CompanyIdSnapshotNonNeg""
                    CHECK (""CompanyIdSnapshot"" >= 0);
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ProductionOperations_Company_Status""
                ON ""ProductionOperations""(""CompanyIdSnapshot"", ""Status"");
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // 6) ProductionOperation
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOperations_Company_Status"";");
            mb.Sql(@"ALTER TABLE ""ProductionOperations"" DROP CONSTRAINT IF EXISTS ""CK_ProductionOperations_CompanyIdSnapshotNonNeg"";");
            mb.Sql(@"ALTER TABLE ""ProductionOperations"" DROP COLUMN IF EXISTS ""CompanyIdSnapshot"";");

            // 5) WorkOrder
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrders_CompanyId"";");
            mb.Sql(@"ALTER TABLE ""WorkOrders"" DROP CONSTRAINT IF EXISTS ""CK_WorkOrders_CompanyIdNonNeg"";");
            mb.Sql(@"ALTER TABLE ""WorkOrders"" DROP COLUMN IF EXISTS ""CompanyId"";");

            // 4) MaterialStructure
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructures_Template_StructureNumber_Rev"";");
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructures_Site_StructureNumber_Rev"";");
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialStructures_Company_Location"";");
            mb.Sql(@"ALTER TABLE ""MaterialStructures"" DROP CONSTRAINT IF EXISTS ""CK_MaterialStructures_SiteScopeOrTemplate"";");
            mb.Sql(@"ALTER TABLE ""MaterialStructures"" DROP CONSTRAINT IF EXISTS ""CK_MaterialStructures_CompanyIdNonNeg"";");
            mb.Sql(@"
                ALTER TABLE ""MaterialStructures""
                    DROP COLUMN IF EXISTS ""IsSiteWideTemplate"",
                    DROP COLUMN IF EXISTS ""LocationId"",
                    DROP COLUMN IF EXISTS ""CompanyId"";
            ");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialStructures_StructureNumber""
                ON ""MaterialStructures""(""StructureNumber"");
            ");

            // 3) MaterialMaster
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialMasters_Company_ShopCode"";");
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialMasters_System_ShopCode"";");
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_MaterialMasters_Company"";");
            mb.Sql(@"
                ALTER TABLE ""MaterialMasters""
                    DROP COLUMN IF EXISTS ""LocationId"",
                    DROP COLUMN IF EXISTS ""CompanyId"";
            ");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MaterialMasters_ShopCode""
                ON ""MaterialMasters""(""ShopCode"");
            ");

            // 2) ProductionBatch
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatches_Company_Location_BatchNumber"";");
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionBatches_Company_Location"";");
            mb.Sql(@"ALTER TABLE ""ProductionBatches"" DROP CONSTRAINT IF EXISTS ""CK_ProductionBatches_LocationIdNonNeg"";");
            mb.Sql(@"ALTER TABLE ""ProductionBatches"" DROP CONSTRAINT IF EXISTS ""CK_ProductionBatches_CompanyIdNonNeg"";");
            mb.Sql(@"
                ALTER TABLE ""ProductionBatches""
                    DROP COLUMN IF EXISTS ""LocationId"",
                    DROP COLUMN IF EXISTS ""CompanyId"";
            ");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductionBatches_BatchNumber""
                ON ""ProductionBatches""(""BatchNumber"");
            ");

            // 1) ProductionOrder
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_Company_OrderNumber"";");
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_ProductionOrders_CompanyId"";");
            mb.Sql(@"ALTER TABLE ""ProductionOrders"" DROP CONSTRAINT IF EXISTS ""CK_ProductionOrders_CompanyIdNonNeg"";");
            mb.Sql(@"ALTER TABLE ""ProductionOrders"" DROP COLUMN IF EXISTS ""CompanyId"";");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductionOrders_OrderNumber""
                ON ""ProductionOrders""(""OrderNumber"");
            ");
        }
    }
}
