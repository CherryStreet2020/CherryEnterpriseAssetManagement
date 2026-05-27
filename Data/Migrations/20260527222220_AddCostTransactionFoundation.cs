using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCostTransactionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SiteIdSnapshot",
                table: "ProductionOperations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CostTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    CostObjectType = table.Column<int>(type: "integer", nullable: false),
                    CostObjectId = table.Column<int>(type: "integer", nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    CostBucket = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SourceTransactionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceTransactionId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    OperationId = table.Column<int>(type: "integer", nullable: true),
                    BomLineId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExtendedCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    CostElement = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    InventoryValuationMethod = table.Column<int>(type: "integer", nullable: true),
                    EffectiveCostDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GlPostingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CapitalizedToInventory = table.Column<bool>(type: "boolean", nullable: false),
                    IncludedInJobMargin = table.Column<bool>(type: "boolean", nullable: false),
                    IncludedInProjectMargin = table.Column<bool>(type: "boolean", nullable: false),
                    RollupAdditiveFlag = table.Column<bool>(type: "boolean", nullable: false),
                    TransferEventId = table.Column<int>(type: "integer", nullable: true),
                    ParentCostObjectId = table.Column<int>(type: "integer", nullable: true),
                    ParentCostObjectType = table.Column<int>(type: "integer", nullable: true),
                    ChildCostObjectId = table.Column<int>(type: "integer", nullable: true),
                    ChildCostObjectType = table.Column<int>(type: "integer", nullable: true),
                    IsReversal = table.Column<bool>(type: "boolean", nullable: false),
                    ReversalOfTransactionId = table.Column<int>(type: "integer", nullable: true),
                    VarianceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HeatNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    JournalEntryId = table.Column<int>(type: "integer", nullable: true),
                    AccountingKeyId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TransferNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceCostObjectType = table.Column<int>(type: "integer", nullable: false),
                    SourceCostObjectId = table.Column<int>(type: "integer", nullable: false),
                    SourceSiteId = table.Column<int>(type: "integer", nullable: true),
                    DestinationCostObjectType = table.Column<int>(type: "integer", nullable: false),
                    DestinationCostObjectId = table.Column<int>(type: "integer", nullable: false),
                    DestinationSiteId = table.Column<int>(type: "integer", nullable: true),
                    TransferQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TransferUnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TransferExtendedCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MaterialCostTransferred = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LaborCostTransferred = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OverheadCostTransferred = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SubcontractCostTransferred = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OtherCostTransferred = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TransferType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsProvisional = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsReversal = table.Column<bool>(type: "boolean", nullable: false),
                    ReversalOfTransferId = table.Column<int>(type: "integer", nullable: true),
                    JournalEntryId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostTransfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrderCostSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    CostStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EstimatedMaterialCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedLaborCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedMachineCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedBurdenCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedOutsideProcessingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedSubcontractCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedFreightLandedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedToolingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedScrapReworkCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimatedTotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualMaterialCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualLaborCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualMachineCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualBurdenCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualOutsideProcessingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualSubcontractCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualFreightLandedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualToolingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualScrapReworkCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualTotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OpenCommittedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ForecastRemainingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EstimateAtCompletion = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostVariance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MarginImpact = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WipBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CompletedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClosedSettledValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ParentCostTransferIn = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChildCostTransferOut = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NonAdditiveChildDetailTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostPerGoodUnit = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    GoodQuantityCompleted = table.Column<decimal>(type: "numeric", nullable: true),
                    ScrapQuantityTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    LastRollupTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RollupStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CostExceptionCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderCostSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostTransactions_CompanyId",
                table: "CostTransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CostTransactions_TenantId",
                table: "CostTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CostTxn_Additive",
                table: "CostTransactions",
                column: "RollupAdditiveFlag");

            migrationBuilder.CreateIndex(
                name: "IX_CostTxn_Bucket",
                table: "CostTransactions",
                column: "CostBucket");

            migrationBuilder.CreateIndex(
                name: "IX_CostTxn_CostObject",
                table: "CostTransactions",
                columns: new[] { "CostObjectType", "CostObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostTxn_Date",
                table: "CostTransactions",
                column: "EffectiveCostDate");

            migrationBuilder.CreateIndex(
                name: "IX_CostTxn_PRO",
                table: "CostTransactions",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CostTxn_Site",
                table: "CostTransactions",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_CostTxn_Type",
                table: "CostTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "UX_CostTxn_Company_Number",
                table: "CostTransactions",
                columns: new[] { "CompanyId", "TransactionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostTransfers_CompanyId",
                table: "CostTransfers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CostTransfers_TenantId",
                table: "CostTransfers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CostXfer_Dest",
                table: "CostTransfers",
                columns: new[] { "DestinationCostObjectType", "DestinationCostObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostXfer_Source",
                table: "CostTransfers",
                columns: new[] { "SourceCostObjectType", "SourceCostObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostXfer_Type",
                table: "CostTransfers",
                column: "TransferType");

            migrationBuilder.CreateIndex(
                name: "UX_CostXfer_Company_Number",
                table: "CostTransfers",
                columns: new[] { "CompanyId", "TransferNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProCostSum_Status",
                table: "ProductionOrderCostSummaries",
                column: "CostStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderCostSummaries_CompanyId",
                table: "ProductionOrderCostSummaries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderCostSummaries_TenantId",
                table: "ProductionOrderCostSummaries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_ProCostSum_Company_PRO",
                table: "ProductionOrderCostSummaries",
                columns: new[] { "CompanyId", "ProductionOrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostTransactions");

            migrationBuilder.DropTable(
                name: "CostTransfers");

            migrationBuilder.DropTable(
                name: "ProductionOrderCostSummaries");

            migrationBuilder.DropColumn(
                name: "SiteIdSnapshot",
                table: "ProductionOperations");
        }
    }
}
