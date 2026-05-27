using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionMaterialTransactionB8Pro3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionMaterialTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TransactionDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    BomLineId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    OperationSequence = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    QuantityBeforeTransaction = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    QuantityAfterTransaction = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    FromWarehouse = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FromBin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToWarehouse = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToBin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HeatNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VendorLot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualUnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ExtendedCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CostBucket = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReasonCode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReasonDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupervisorOverride = table.Column<bool>(type: "boolean", nullable: false),
                    SupervisorOverrideBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsReversal = table.Column<bool>(type: "boolean", nullable: false),
                    OriginalTransactionId = table.Column<int>(type: "integer", nullable: true),
                    TransferProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    TransferBomLineId = table.Column<int>(type: "integer", nullable: true),
                    TransferPairId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TransferReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransferApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TransferApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OriginalItemId = table.Column<int>(type: "integer", nullable: true),
                    SubstitutionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubstitutionAuthReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SubstitutionCustomerApproved = table.Column<bool>(type: "boolean", nullable: false),
                    IsBackflushed = table.Column<bool>(type: "boolean", nullable: false),
                    BackflushTriggerOperationId = table.Column<int>(type: "integer", nullable: true),
                    PerformedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionMaterialTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialTransactions_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialTransactions_Items_OriginalItemId",
                        column: x => x.OriginalItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialTransactions_ProductionMaterialStructures~",
                        column: x => x.BomLineId,
                        principalTable: "ProductionMaterialStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialTransactions_ProductionMaterialStructure~1",
                        column: x => x.TransferBomLineId,
                        principalTable: "ProductionMaterialStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialTransactions_ProductionMaterialTransactio~",
                        column: x => x.OriginalTransactionId,
                        principalTable: "ProductionMaterialTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialTransactions_ProductionOrders_ProductionO~",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialTransactions_ProductionOrders_TransferPro~",
                        column: x => x.TransferProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatTxn_BomLine",
                table: "ProductionMaterialTransactions",
                column: "BomLineId");

            migrationBuilder.CreateIndex(
                name: "IX_MatTxn_Date",
                table: "ProductionMaterialTransactions",
                column: "TransactionDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MatTxn_Item",
                table: "ProductionMaterialTransactions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MatTxn_PRO",
                table: "ProductionMaterialTransactions",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MatTxn_TransferPair",
                table: "ProductionMaterialTransactions",
                column: "TransferPairId");

            migrationBuilder.CreateIndex(
                name: "IX_MatTxn_Type",
                table: "ProductionMaterialTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialTransactions_CompanyId",
                table: "ProductionMaterialTransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialTransactions_OriginalItemId",
                table: "ProductionMaterialTransactions",
                column: "OriginalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialTransactions_OriginalTransactionId",
                table: "ProductionMaterialTransactions",
                column: "OriginalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialTransactions_TenantId",
                table: "ProductionMaterialTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialTransactions_TransferBomLineId",
                table: "ProductionMaterialTransactions",
                column: "TransferBomLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialTransactions_TransferProductionOrderId",
                table: "ProductionMaterialTransactions",
                column: "TransferProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "UX_MatTxn_Company_Number",
                table: "ProductionMaterialTransactions",
                columns: new[] { "CompanyId", "TransactionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionMaterialTransactions");
        }
    }
}
