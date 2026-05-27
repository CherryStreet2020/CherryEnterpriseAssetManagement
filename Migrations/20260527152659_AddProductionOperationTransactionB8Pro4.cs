using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionOperationTransactionB8Pro4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionOperationTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TransactionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<int>(type: "integer", nullable: false),
                    OperationSequence = table.Column<int>(type: "integer", nullable: false),
                    StatusBefore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StatusAfter = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    GoodQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ScrapQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReworkQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RejectQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SetupMinutes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RunMinutes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MachineMinutes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LaborMinutes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WorkCenterId = table.Column<int>(type: "integer", nullable: true),
                    AssetId = table.Column<int>(type: "integer", nullable: true),
                    OperatorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CrewSize = table.Column<int>(type: "integer", nullable: true),
                    PreviousWorkCenterId = table.Column<int>(type: "integer", nullable: true),
                    PreviousAssetId = table.Column<int>(type: "integer", nullable: true),
                    LaborCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MachineCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    BurdenCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsFinalOperation = table.Column<bool>(type: "boolean", nullable: false),
                    BackflushMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedLotSerials = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DestinationLocation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SkipReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NewOperationId = table.Column<int>(type: "integer", nullable: true),
                    ReworkInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NewOperationSequence = table.Column<int>(type: "integer", nullable: true),
                    ScrapReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CauseCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsReversal = table.Column<bool>(type: "boolean", nullable: false),
                    OriginalTransactionId = table.Column<int>(type: "integer", nullable: true),
                    InspectionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    QualityHold = table.Column<bool>(type: "boolean", nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOperationTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOperationTransactions_ProductionOperationTransact~",
                        column: x => x.OriginalTransactionId,
                        principalTable: "ProductionOperationTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionOperationTransactions_ProductionOperations_Operat~",
                        column: x => x.OperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOperationTransactions_ProductionOrders_Production~",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpTxn_Date",
                table: "ProductionOperationTransactions",
                column: "TransactionDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OpTxn_Op",
                table: "ProductionOperationTransactions",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpTxn_PRO",
                table: "ProductionOperationTransactions",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OpTxn_Type",
                table: "ProductionOperationTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOperationTransactions_CompanyId",
                table: "ProductionOperationTransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOperationTransactions_OriginalTransactionId",
                table: "ProductionOperationTransactions",
                column: "OriginalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOperationTransactions_TenantId",
                table: "ProductionOperationTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_OpTxn_Company_Number",
                table: "ProductionOperationTransactions",
                columns: new[] { "CompanyId", "TransactionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionOperationTransactions");
        }
    }
}
