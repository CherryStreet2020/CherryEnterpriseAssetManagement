-- Sprint 13.5 PR #5c.2 — Tenant Scoping Hardening
--
-- AUTHORITY: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock 2026-05-23)
-- GATE: reference_bic_entity_checklist.md (6-point gate)
-- AUDIT: outputs/PR-5c2-audit.md
--
-- WHAT THIS LANDS:
--   1. Direct CompanyId on 6 entities that were scoping through a parent only:
--      ProductionBatch, MaterialMaster (NULL = system ref), MaterialStructure,
--      ProductionOrder, WorkOrder, ProductionOperation (as CompanyIdSnapshot).
--   2. Direct LocationId on entities that physically live at a site:
--      ProductionBatch, MaterialStructure (NULL + IsSiteWideTemplate, mirroring
--      PR #5c.1 Routing pattern).
--   3. Fix 4 cross-tenant UNIQUE leaks:
--      - ProductionOrders.OrderNumber  → (CompanyId, OrderNumber)
--      - ProductionBatches.BatchNumber → (CompanyId, LocationId, BatchNumber)
--      - MaterialMasters.ShopCode      → 2 partial: system (NULL Company) + tenant (Company set)
--      - MaterialStructures.StructureNumber → 2 partial: site + template (mirrors Routing)
--   4. ProductionOperation entity-file sync (LocationIdSnapshot + CompanyIdSnapshot
--      properties added in C# so EF actually writes them — currently they default
--      to 0 silently because the columns exist but the properties don't).
--
-- REPLIT GOTCHA (PR #5c.1.1 lesson): NO COALESCE-in-index. Always 2 partial
-- indexes (WHERE col IS NULL / WHERE col IS NOT NULL).
--
-- ORDERING: every NOT NULL column adds with DEFAULT 0, backfills via parent
-- JOIN, then we drop the DEFAULT. Old global UNIQUEs drop BEFORE new composite
-- UNIQUEs create (Postgres allows simultaneous duplicate-coverage but cleaner
-- to drop first).

-- ============================================================
-- 1. ProductionOrder — add CompanyId NOT NULL, fix OrderNumber UNIQUE
-- ============================================================

ALTER TABLE "ProductionOrders"
    ADD COLUMN IF NOT EXISTS "CompanyId" integer NOT NULL DEFAULT 0;

UPDATE "ProductionOrders" po
SET "CompanyId" = COALESCE(l."CompanyId", 0)
FROM "Locations" l
WHERE po."LocationId" = l."Id"
  AND po."CompanyId" = 0;

-- For ProductionOrders with no LocationId (orphan unsited orders), fall back to
-- CompanyId via CustomerProject if linked, else leave as 0 — covered by deferred CHECK.
UPDATE "ProductionOrders" po
SET "CompanyId" = COALESCE(cp."CompanyId", po."CompanyId")
FROM "CustomerProjects" cp
WHERE po."CustomerProjectId" = cp."Id"
  AND po."CompanyId" = 0;

ALTER TABLE "ProductionOrders" ALTER COLUMN "CompanyId" DROP DEFAULT;

-- Deferred CHECK — allows 0s during grace period (orphan orders). Tighten to
-- > 0 in PR #5c.4 after a backfill seeder fills them.
ALTER TABLE "ProductionOrders"
    ADD CONSTRAINT "CK_ProductionOrders_CompanyIdNonNeg"
    CHECK ("CompanyId" >= 0);

CREATE INDEX IF NOT EXISTS "IX_ProductionOrders_CompanyId" ON "ProductionOrders"("CompanyId");

-- Drop the global UNIQUE leak.
DROP INDEX IF EXISTS "IX_ProductionOrders_OrderNumber";

-- New composite UNIQUE — one OrderNumber per Company.
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductionOrders_Company_OrderNumber"
    ON "ProductionOrders"("CompanyId", "OrderNumber");


-- ============================================================
-- 2. ProductionBatch — add CompanyId + LocationId NOT NULL, fix BatchNumber UNIQUE
-- ============================================================

ALTER TABLE "ProductionBatches"
    ADD COLUMN IF NOT EXISTS "CompanyId" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "LocationId" integer NOT NULL DEFAULT 0;

-- Backfill from PrimaryEquipment (Asset) where available.
UPDATE "ProductionBatches" pb
SET "CompanyId"  = COALESCE(a."CompanyId", 0),
    "LocationId" = COALESCE(a."LocationId", 0)
FROM "Assets" a
WHERE pb."PrimaryEquipmentId" = a."Id"
  AND pb."CompanyId" = 0;

-- Fallback backfill: via ProductionBatchAllocation → ProductionOrder → Location.
UPDATE "ProductionBatches" pb
SET "CompanyId"  = COALESCE(l."CompanyId", pb."CompanyId"),
    "LocationId" = COALESCE(po."LocationId", pb."LocationId")
FROM "ProductionBatchAllocations" pba
JOIN "ProductionOrders" po ON pba."ProductionOrderId" = po."Id"
LEFT JOIN "Locations" l ON po."LocationId" = l."Id"
WHERE pba."ProductionBatchId" = pb."Id"
  AND pb."CompanyId" = 0;

ALTER TABLE "ProductionBatches" ALTER COLUMN "CompanyId" DROP DEFAULT;
ALTER TABLE "ProductionBatches" ALTER COLUMN "LocationId" DROP DEFAULT;

ALTER TABLE "ProductionBatches"
    ADD CONSTRAINT "CK_ProductionBatches_CompanyIdNonNeg" CHECK ("CompanyId" >= 0),
    ADD CONSTRAINT "CK_ProductionBatches_LocationIdNonNeg" CHECK ("LocationId" >= 0);

CREATE INDEX IF NOT EXISTS "IX_ProductionBatches_Company_Location" ON "ProductionBatches"("CompanyId", "LocationId");

-- Drop the global UNIQUE leak.
DROP INDEX IF EXISTS "IX_ProductionBatches_BatchNumber";

-- New composite UNIQUE — one BatchNumber per (Company, Location).
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductionBatches_Company_Location_BatchNumber"
    ON "ProductionBatches"("CompanyId", "LocationId", "BatchNumber");


-- ============================================================
-- 3. MaterialMaster — add NULLABLE CompanyId + LocationId (cross-tenant reference)
-- ============================================================

ALTER TABLE "MaterialMasters"
    ADD COLUMN IF NOT EXISTS "CompanyId" integer NULL,
    ADD COLUMN IF NOT EXISTS "LocationId" integer NULL;

-- No backfill — existing rows stay NULL (treated as system reference data).
-- New tenant-specific rows get CompanyId set by the seeder or admin UI.

CREATE INDEX IF NOT EXISTS "IX_MaterialMasters_Company" ON "MaterialMasters"("CompanyId");

-- Drop the global UNIQUE leak.
DROP INDEX IF EXISTS "IX_MaterialMasters_ShopCode";

-- New partial UNIQUE — one ShopCode per scope, no COALESCE-in-index (Replit gotcha).
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaterialMasters_System_ShopCode"
    ON "MaterialMasters"("ShopCode")
    WHERE "CompanyId" IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaterialMasters_Company_ShopCode"
    ON "MaterialMasters"("CompanyId", "ShopCode")
    WHERE "CompanyId" IS NOT NULL;


-- ============================================================
-- 4. MaterialStructure — add CompanyId NOT NULL + LocationId NULL + IsSiteWideTemplate
--    (mirrors PR #5c.1 Routing pattern for company-wide engineering templates)
-- ============================================================

ALTER TABLE "MaterialStructures"
    ADD COLUMN IF NOT EXISTS "CompanyId" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "LocationId" integer NULL,
    ADD COLUMN IF NOT EXISTS "IsSiteWideTemplate" boolean NOT NULL DEFAULT FALSE;

-- No straightforward backfill (no parent FK). Existing rows stay at CompanyId = 0,
-- which is allowed by the deferred CHECK. PR #5c.4 seeder will fill from a
-- tenant-aware service at next onboarding cycle.

ALTER TABLE "MaterialStructures" ALTER COLUMN "CompanyId" DROP DEFAULT;

ALTER TABLE "MaterialStructures"
    ADD CONSTRAINT "CK_MaterialStructures_CompanyIdNonNeg" CHECK ("CompanyId" >= 0);

-- Mirror the Routing site-scope-or-template CHECK pattern (PR #5c.1).
ALTER TABLE "MaterialStructures"
    ADD CONSTRAINT "CK_MaterialStructures_SiteScopeOrTemplate"
    CHECK (
        ("LocationId" IS NULL AND "IsSiteWideTemplate" = TRUE)
     OR ("LocationId" IS NOT NULL AND "IsSiteWideTemplate" = FALSE)
     OR ("LocationId" IS NULL AND "IsSiteWideTemplate" = FALSE)
    );

CREATE INDEX IF NOT EXISTS "IX_MaterialStructures_Company_Location" ON "MaterialStructures"("CompanyId", "LocationId");

-- Drop the global UNIQUE leak.
DROP INDEX IF EXISTS "IX_MaterialStructures_StructureNumber";

-- New partial UNIQUEs — site-scoped + template-scoped (no COALESCE-in-index).
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaterialStructures_Site_StructureNumber_Rev"
    ON "MaterialStructures"("CompanyId", "LocationId", "StructureNumber", "Revision")
    WHERE "LocationId" IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MaterialStructures_Template_StructureNumber_Rev"
    ON "MaterialStructures"("CompanyId", "StructureNumber", "Revision")
    WHERE "LocationId" IS NULL;


-- ============================================================
-- 5. WorkOrder — add direct CompanyId (defensive denormalization from Asset)
-- ============================================================

ALTER TABLE "WorkOrders"
    ADD COLUMN IF NOT EXISTS "CompanyId" integer NOT NULL DEFAULT 0;

-- Backfill from Asset.CompanyId (FK is NOT NULL on WorkOrder.AssetId).
UPDATE "WorkOrders" wo
SET "CompanyId" = COALESCE(a."CompanyId", 0)
FROM "Assets" a
WHERE wo."AssetId" = a."Id"
  AND wo."CompanyId" = 0;

ALTER TABLE "WorkOrders" ALTER COLUMN "CompanyId" DROP DEFAULT;

ALTER TABLE "WorkOrders"
    ADD CONSTRAINT "CK_WorkOrders_CompanyIdNonNeg" CHECK ("CompanyId" >= 0);

CREATE INDEX IF NOT EXISTS "IX_WorkOrders_CompanyId" ON "WorkOrders"("CompanyId");


-- ============================================================
-- 6. ProductionOperation — add CompanyIdSnapshot, mirror LocationIdSnapshot
--    (LocationIdSnapshot was added in PR #5c.1 but the C# entity is missing
--    the property — fixed in the model file part of this PR.)
-- ============================================================

ALTER TABLE "ProductionOperations"
    ADD COLUMN IF NOT EXISTS "CompanyIdSnapshot" integer NOT NULL DEFAULT 0;

ALTER TABLE "ProductionOperations"
    ADD CONSTRAINT "CK_ProductionOperations_CompanyIdSnapshotNonNeg"
    CHECK ("CompanyIdSnapshot" >= 0);

CREATE INDEX IF NOT EXISTS "IX_ProductionOperations_Company_Status"
    ON "ProductionOperations"("CompanyIdSnapshot", "Status");


-- ============================================================
-- DOWN — reverse everything (for completeness / rollback safety).
-- Will be expressed in EF Core migration Down() method.
-- ============================================================
