-- Sprint 13.5 PR #5c.2 — Tenant Scoping Hardening (reference draft)
--
-- This file mirrors Migrations/20260524120000_TenantScopingHardeningPr5c2.cs in
-- raw SQL form for ops reference. The .cs migration is the source of truth.
--
-- AUTHORITY: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock 2026-05-23)
-- GATE:      reference_bic_entity_checklist.md
-- AUDIT:     outputs/PR-5c2-audit.md
--
-- See the EF Core migration for the deployed version with idempotent guards
-- (ADD COLUMN IF NOT EXISTS, CREATE INDEX IF NOT EXISTS, etc.).

-- 1. ProductionOrder ----------------------------------------------------------
ALTER TABLE "ProductionOrders" ADD COLUMN "CompanyId" integer NOT NULL DEFAULT 0;
UPDATE "ProductionOrders" po SET "CompanyId" = l."CompanyId"
  FROM "Locations" l WHERE po."LocationId" = l."Id" AND po."CompanyId" = 0;
UPDATE "ProductionOrders" po SET "CompanyId" = cp."CompanyId"
  FROM "CustomerProjects" cp WHERE po."CustomerProjectId" = cp."Id" AND po."CompanyId" = 0;
ALTER TABLE "ProductionOrders" ALTER COLUMN "CompanyId" DROP DEFAULT;
ALTER TABLE "ProductionOrders" ADD CONSTRAINT "CK_ProductionOrders_CompanyIdNonNeg" CHECK ("CompanyId" >= 0);
CREATE INDEX "IX_ProductionOrders_CompanyId" ON "ProductionOrders"("CompanyId");
DROP INDEX IF EXISTS "IX_ProductionOrders_OrderNumber";
CREATE UNIQUE INDEX "IX_ProductionOrders_Company_OrderNumber" ON "ProductionOrders"("CompanyId", "OrderNumber");

-- 2. ProductionBatch ---------------------------------------------------------
ALTER TABLE "ProductionBatches"
  ADD COLUMN "CompanyId"  integer NOT NULL DEFAULT 0,
  ADD COLUMN "LocationId" integer NOT NULL DEFAULT 0;
UPDATE "ProductionBatches" pb SET "CompanyId" = a."CompanyId", "LocationId" = COALESCE(a."LocationId", 0)
  FROM "Assets" a WHERE pb."PrimaryEquipmentId" = a."Id" AND pb."CompanyId" = 0;
UPDATE "ProductionBatches" pb SET "CompanyId" = l."CompanyId", "LocationId" = COALESCE(po."LocationId", 0)
  FROM "ProductionBatchAllocations" pba
  JOIN "ProductionOrders" po ON pba."ProductionOrderId" = po."Id"
  LEFT JOIN "Locations" l ON po."LocationId" = l."Id"
  WHERE pba."ProductionBatchId" = pb."Id" AND pb."CompanyId" = 0;
ALTER TABLE "ProductionBatches" ALTER COLUMN "CompanyId" DROP DEFAULT;
ALTER TABLE "ProductionBatches" ALTER COLUMN "LocationId" DROP DEFAULT;
ALTER TABLE "ProductionBatches"
  ADD CONSTRAINT "CK_ProductionBatches_CompanyIdNonNeg" CHECK ("CompanyId" >= 0),
  ADD CONSTRAINT "CK_ProductionBatches_LocationIdNonNeg" CHECK ("LocationId" >= 0);
CREATE INDEX "IX_ProductionBatches_Company_Location" ON "ProductionBatches"("CompanyId", "LocationId");
DROP INDEX IF EXISTS "IX_ProductionBatches_BatchNumber";
CREATE UNIQUE INDEX "IX_ProductionBatches_Company_Location_BatchNumber"
  ON "ProductionBatches"("CompanyId", "LocationId", "BatchNumber");

-- 3. MaterialMaster (cross-tenant reference) ---------------------------------
ALTER TABLE "MaterialMasters" ADD COLUMN "CompanyId" integer NULL, ADD COLUMN "LocationId" integer NULL;
CREATE INDEX "IX_MaterialMasters_Company" ON "MaterialMasters"("CompanyId") WHERE "CompanyId" IS NOT NULL;
DROP INDEX IF EXISTS "IX_MaterialMasters_ShopCode";
CREATE UNIQUE INDEX "IX_MaterialMasters_System_ShopCode"
  ON "MaterialMasters"("ShopCode") WHERE "CompanyId" IS NULL;
CREATE UNIQUE INDEX "IX_MaterialMasters_Company_ShopCode"
  ON "MaterialMasters"("CompanyId", "ShopCode") WHERE "CompanyId" IS NOT NULL;

-- 4. MaterialStructure (mirrors Routing site-or-template pattern) ------------
ALTER TABLE "MaterialStructures"
  ADD COLUMN "CompanyId"          integer NOT NULL DEFAULT 0,
  ADD COLUMN "LocationId"         integer NULL,
  ADD COLUMN "IsSiteWideTemplate" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE "MaterialStructures" ALTER COLUMN "CompanyId" DROP DEFAULT;
ALTER TABLE "MaterialStructures"
  ADD CONSTRAINT "CK_MaterialStructures_CompanyIdNonNeg" CHECK ("CompanyId" >= 0),
  ADD CONSTRAINT "CK_MaterialStructures_SiteScopeOrTemplate" CHECK (
    ("LocationId" IS NULL     AND "IsSiteWideTemplate" = TRUE)
 OR ("LocationId" IS NOT NULL AND "IsSiteWideTemplate" = FALSE)
 OR ("LocationId" IS NULL     AND "IsSiteWideTemplate" = FALSE));
CREATE INDEX "IX_MaterialStructures_Company_Location" ON "MaterialStructures"("CompanyId", "LocationId");
DROP INDEX IF EXISTS "IX_MaterialStructures_StructureNumber";
CREATE UNIQUE INDEX "IX_MaterialStructures_Site_StructureNumber_Rev"
  ON "MaterialStructures"("CompanyId", "LocationId", "StructureNumber", "Revision")
  WHERE "LocationId" IS NOT NULL;
CREATE UNIQUE INDEX "IX_MaterialStructures_Template_StructureNumber_Rev"
  ON "MaterialStructures"("CompanyId", "StructureNumber", "Revision")
  WHERE "LocationId" IS NULL;

-- 5. WorkOrder (defensive denormalization from Asset) ------------------------
ALTER TABLE "WorkOrders" ADD COLUMN "CompanyId" integer NOT NULL DEFAULT 0;
UPDATE "WorkOrders" wo SET "CompanyId" = a."CompanyId"
  FROM "Assets" a WHERE wo."AssetId" = a."Id" AND wo."CompanyId" = 0;
ALTER TABLE "WorkOrders" ALTER COLUMN "CompanyId" DROP DEFAULT;
ALTER TABLE "WorkOrders" ADD CONSTRAINT "CK_WorkOrders_CompanyIdNonNeg" CHECK ("CompanyId" >= 0);
CREATE INDEX "IX_WorkOrders_CompanyId" ON "WorkOrders"("CompanyId");

-- 6. ProductionOperation (CompanyIdSnapshot sibling to LocationIdSnapshot) ---
ALTER TABLE "ProductionOperations" ADD COLUMN "CompanyIdSnapshot" integer NOT NULL DEFAULT 0;
ALTER TABLE "ProductionOperations"
  ADD CONSTRAINT "CK_ProductionOperations_CompanyIdSnapshotNonNeg" CHECK ("CompanyIdSnapshot" >= 0);
CREATE INDEX "IX_ProductionOperations_Company_Status"
  ON "ProductionOperations"("CompanyIdSnapshot", "Status");
