#!/usr/bin/env bash
# ADR-013 Phase E.2 — DB schema validation.
#
# Validates that every table, column, FK, UNIQUE index, and delete
# behavior introduced by PRs #119.12, #119.13a, #119.13b is present
# in the database with correct shape.
#
# Run from inside Replit Shell (uses $DATABASE_URL).
# Exits 0 if all checks pass, 1 if any fail.
#
# Reference: project_pr119_12_shipped.md + project_pr119_13a_shipped.md
# + project_pr119_13b_shipped.md memory files for source-of-truth schemas.

set -u  # error on unset variables; not -e because we want all checks to run

PASS=0
FAIL=0

check() {
  local description="$1"
  local query="$2"
  local expected="$3"

  local actual
  actual=$(psql "$DATABASE_URL" -t -A -c "$query" 2>&1 | tr -d '[:space:]')

  if [ "$actual" = "$expected" ]; then
    echo "  PASS  $description"
    PASS=$((PASS+1))
  else
    echo "  FAIL  $description"
    echo "        expected: $expected"
    echo "        actual:   $actual"
    FAIL=$((FAIL+1))
  fi
}

table_exists() {
  check "table $1 exists" \
    "SELECT COUNT(*) FROM pg_tables WHERE tablename='$1'" \
    "1"
}

column_exists() {
  check "column $1.$2 exists" \
    "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='$1' AND column_name='$2'" \
    "1"
}

column_type() {
  check "column $1.$2 is type $3" \
    "SELECT data_type FROM information_schema.columns WHERE table_name='$1' AND column_name='$2'" \
    "$3"
}

fk_with_action() {
  # $1 = constraint name, $2 = ON DELETE action (CASCADE/SETNULL/RESTRICT/NOACTION)
  check "FK $1 has ON DELETE $2" \
    "SELECT confdeltype FROM pg_constraint WHERE conname='$1'" \
    "$(case $2 in
        CASCADE) echo c ;;
        SETNULL) echo n ;;
        RESTRICT) echo r ;;
        NOACTION) echo a ;;
      esac)"
}

unique_index() {
  check "UNIQUE index $1 exists on $2" \
    "SELECT COUNT(*) FROM pg_indexes WHERE indexname='$1' AND tablename='$2' AND indexdef LIKE 'CREATE UNIQUE%'" \
    "1"
}

index_exists() {
  check "index $1 exists on $2" \
    "SELECT COUNT(*) FROM pg_indexes WHERE indexname='$1' AND tablename='$2'" \
    "1"
}

echo "================================================================"
echo "  Phase E DB schema validation"
echo "  Repo: CherryStreet2020/CherryEnterpriseAssetManagement"
echo "  Tests cover: PR #119.12 + #119.13a + #119.13b"
echo "================================================================"

# ----- PR #119.12: ProductionOrder + JobShopDetail + WorkOrderOperation ext -----
echo ""
echo "[PR #119.12] ProductionOrder + JobShopDetail + Operation outside-op"
table_exists ProductionOrders
table_exists ProductionJobShopDetails
column_exists ProductionOrders OrderNumber
column_exists ProductionOrders Type
column_exists ProductionOrders Status
column_exists ProductionOrders MasterProductionOrderId
column_exists ProductionOrders Revision
column_exists ProductionJobShopDetails ProductionOrderId
column_exists ProductionJobShopDetails NestPlanId
column_exists ProductionJobShopDetails DrawingNumber
column_exists ProductionJobShopDetails HasOutsideOperations
column_exists ProductionJobShopDetails MaterialIssueMethod
column_exists WorkOrderOperations IsExternal
column_exists WorkOrderOperations VendorId
column_exists WorkOrderOperations AutoGeneratePR
column_exists WorkOrderOperations VendorPoLineId
column_exists WorkOrderOperations VendorExpectedReturnDate
unique_index IX_ProductionOrders_OrderNumber ProductionOrders
unique_index IX_ProductionJobShopDetails_ProductionOrderId ProductionJobShopDetails
fk_with_action FK_ProductionOrders_ProductionOrders_MasterProductionOrderId SETNULL
fk_with_action FK_ProductionJobShopDetails_ProductionOrders_ProductionOrderId CASCADE
fk_with_action FK_WorkOrderOperations_Vendors_VendorId SETNULL

# CutListId placeholder MUST be gone (dropped in #119.13a)
check "ProductionJobShopDetails.CutListId column dropped (was placeholder)" \
  "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='ProductionJobShopDetails' AND column_name='CutListId'" \
  "0"

# ----- PR #119.13a: ProductionBatch backbone -----
echo ""
echo "[PR #119.13a] Polymorphic ProductionBatch backbone (8 tables)"
table_exists ProductionBatches
table_exists Nests
table_exists ProcessBatches
table_exists ProductionBatchAllocations
table_exists ProductionBatchEquipmentLinks
table_exists ProductionBatchStateEvents
table_exists RecipeRevisions
table_exists MrbDispositions
column_exists ProductionBatches BatchNumber
column_exists ProductionBatches BatchType
column_exists ProductionBatches Status
column_exists ProductionBatches BatchPoolCode
column_exists ProductionBatches PrimaryEquipmentId
column_exists ProductionBatches RecipeRevisionId
column_exists ProductionBatches AllocationMethod
column_exists ProductionBatches QuarantineDispositionId
column_exists Nests ProductionBatchId
column_exists Nests Utilization
column_exists Nests PiecesPlanned
column_exists Nests PiecesCut
column_exists Nests PierceCount
column_exists Nests CutPathLengthMm
column_exists ProcessBatches ProductionBatchId
column_exists ProcessBatches ProcessType
column_exists ProcessBatches SetpointTempC
column_exists ProcessBatches LoadMassKg
column_exists ProcessBatches HeatTreatChartUrl
column_exists ProcessBatches WitnessCouponLotId
column_exists ProductionBatchAllocations ProductionBatchId
column_exists ProductionBatchAllocations WorkOrderOperationId
column_exists ProductionBatchAllocations AllocationBasis
column_exists ProductionBatchAllocations AllocatedCost
column_exists ProductionBatchAllocations Origin
column_exists ProductionBatchEquipmentLinks SequenceNo
column_exists ProductionBatchEquipmentLinks Role
column_exists ProductionBatchStateEvents FromStatus
column_exists ProductionBatchStateEvents ToStatus
column_exists ProductionBatchStateEvents MrbDispositionId
column_exists WorkOrderOperations BatchPoolCode
column_exists WorkOrderOperations ProductionBatchId
column_exists WorkOrderOperations BatchSequenceNo
unique_index IX_ProductionBatches_BatchNumber ProductionBatches
unique_index IX_Nests_ProductionBatchId Nests
unique_index IX_ProcessBatches_ProductionBatchId ProcessBatches
unique_index IX_ProductionBatchAllocations_BatchId_OperationId ProductionBatchAllocations
unique_index IX_RecipeRevisions_Name_Version RecipeRevisions
unique_index IX_MrbDispositions_DispositionNumber MrbDispositions
fk_with_action FK_Nests_ProductionBatches_ProductionBatchId CASCADE
fk_with_action FK_ProcessBatches_ProductionBatches_ProductionBatchId CASCADE
fk_with_action FK_ProductionBatchAllocations_ProductionBatches_ProductionBatchId CASCADE
fk_with_action FK_ProductionBatchEquipmentLinks_ProductionBatches_ProductionBatchId CASCADE
fk_with_action FK_ProductionBatchStateEvents_ProductionBatches_ProductionBatchId CASCADE
fk_with_action FK_ProductionBatches_Assets_PrimaryEquipmentId SETNULL
fk_with_action FK_ProductionBatches_RecipeRevisions_RecipeRevisionId SETNULL
fk_with_action FK_ProductionBatches_MrbDispositions_QuarantineDispositionId SETNULL
fk_with_action FK_WorkOrderOperations_ProductionBatches_ProductionBatchId SETNULL
fk_with_action FK_ProductionJobShopDetails_Nests_NestPlanId SETNULL
fk_with_action FK_ProductionBatchEquipmentLinks_Assets_EquipmentId RESTRICT
fk_with_action FK_RecipeRevisions_RecipeRevisions_MasterRecipeId SETNULL

# ----- PR #119.13b: sheet & material traceability -----
echo ""
echo "[PR #119.13b] Sheet & material traceability (4 tables + Nest ext)"
table_exists MaterialMasters
table_exists StockReceipts
table_exists Remnants
table_exists CutListLines
column_exists MaterialMasters ShopCode
column_exists MaterialMasters AstmDesignation
column_exists MaterialMasters Form
column_exists MaterialMasters IsAnisotropic
column_exists StockReceipts ReceiptNumber
column_exists StockReceipts ItemId
column_exists StockReceipts HeatNumber
column_exists StockReceipts MillCertUrl
column_exists StockReceipts Mill
column_exists StockReceipts SourcePoNumber
column_exists StockReceipts SourcePoLineId
column_exists StockReceipts ReceivedAt
column_exists StockReceipts QuantityReceived
column_exists StockReceipts QuantityRemaining
column_exists StockReceipts Status
column_exists Remnants RemnantNumber
column_exists Remnants ParentReceiptId
column_exists Remnants ParentNestId
column_exists Remnants HeatNumber
column_exists Remnants ConsumedByNestId
column_exists CutListLines ItemId
column_exists CutListLines NestId
column_exists CutListLines SourceProductionOrderId
column_exists CutListLines MaterialMasterId
column_exists CutListLines GrainDirection
column_exists CutListLines CommonLineGroup
column_exists CutListLines Status
column_exists Nests StockReceiptId
unique_index IX_MaterialMasters_ShopCode MaterialMasters
unique_index IX_StockReceipts_ReceiptNumber StockReceipts
unique_index IX_Remnants_RemnantNumber Remnants
index_exists IX_StockReceipts_HeatNumber StockReceipts
index_exists IX_Remnants_HeatNumber Remnants
fk_with_action FK_StockReceipts_Items_ItemId RESTRICT
fk_with_action FK_StockReceipts_MaterialMasters_MaterialMasterId SETNULL
fk_with_action FK_Remnants_StockReceipts_ParentReceiptId RESTRICT
fk_with_action FK_Remnants_Nests_ParentNestId SETNULL
fk_with_action FK_Remnants_Nests_ConsumedByNestId SETNULL
fk_with_action FK_CutListLines_Items_ItemId RESTRICT
fk_with_action FK_CutListLines_Nests_NestId SETNULL
fk_with_action FK_CutListLines_ProductionOrders_SourceProductionOrderId SETNULL
fk_with_action FK_Nests_StockReceipts_StockReceiptId SETNULL

# ----- Summary -----
echo ""
echo "================================================================"
echo "  RESULT  pass=$PASS  fail=$FAIL"
echo "================================================================"

if [ "$FAIL" -gt 0 ]; then
  exit 1
fi
exit 0
