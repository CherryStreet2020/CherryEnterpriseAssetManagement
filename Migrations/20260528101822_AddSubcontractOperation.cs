using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcontractOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubcontractOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    OperationSequence = table.Column<int>(type: "integer", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OperationDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SupplierSiteCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VendorResource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ServiceItemId = table.Column<int>(type: "integer", nullable: true),
                    ServiceDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ServiceQuantityRule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ServiceUom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    FixedLeadTimeDays = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    VariableLeadTimeDaysPerUnit = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ShipWipRequired = table.Column<bool>(type: "boolean", nullable: false),
                    GenerateSubcontractPo = table.Column<bool>(type: "boolean", nullable: false),
                    GenerateShipment = table.Column<bool>(type: "boolean", nullable: false),
                    PriorOperationSequence = table.Column<int>(type: "integer", nullable: true),
                    ReturnOperationSequence = table.Column<int>(type: "integer", nullable: true),
                    VendorWipWarehouseId = table.Column<int>(type: "integer", nullable: true),
                    VendorWipLocation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ShipFromLocationId = table.Column<int>(type: "integer", nullable: true),
                    ReturnToLocationId = table.Column<int>(type: "integer", nullable: true),
                    InspectionOnReturn = table.Column<bool>(type: "boolean", nullable: false),
                    CertRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SupplierInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PackagingInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ShippingMethod = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FreightResponsibility = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CostMethod = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PoCreationStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ShipmentStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReceiptStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OperationCompletionRule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReworkRule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ScrapRule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ServicePurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    QuantityToShip = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityShipped = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityReceivedBack = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityAccepted = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityRejected = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityScrappedAtVendor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RequiredShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiredBackDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualBackDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_Items_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_Locations_ReturnToLocationId",
                        column: x => x.ReturnToLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_Locations_ShipFromLocationId",
                        column: x => x.ShipFromLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_PurchaseOrderLines_ServicePurchaseOrd~",
                        column: x => x.ServicePurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_Vendors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractOperations_WarehouseMasters_VendorWipWarehouseId",
                        column: x => x.VendorWipWarehouseId,
                        principalTable: "WarehouseMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractDemands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    SubcontractOperationId = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    OperationSequence = table.Column<int>(type: "integer", nullable: false),
                    ServicePurchaseDemandId = table.Column<int>(type: "integer", nullable: true),
                    ServiceQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ServiceUnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WipMovementDemandId = table.Column<int>(type: "integer", nullable: true),
                    WipItemId = table.Column<int>(type: "integer", nullable: true),
                    WipQuantityToSend = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WipQuantityReturned = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    FromOperationSequence = table.Column<int>(type: "integer", nullable: true),
                    ToOperationSequence = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RequiredBackDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ServiceCommittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WipAtVendorUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BothSatisfiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractDemands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractDemands_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractDemands_Items_WipItemId",
                        column: x => x.WipItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractDemands_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractDemands_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractDemands_ProductionSupplyDemands_ServicePurchaseD~",
                        column: x => x.ServicePurchaseDemandId,
                        principalTable: "ProductionSupplyDemands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractDemands_ProductionSupplyDemands_WipMovementDeman~",
                        column: x => x.WipMovementDemandId,
                        principalTable: "ProductionSupplyDemands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractDemands_SubcontractOperations_SubcontractOperati~",
                        column: x => x.SubcontractOperationId,
                        principalTable: "SubcontractOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractDemands_CompanyId",
                table: "SubcontractDemands",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractDemands_ProductionOrderId",
                table: "SubcontractDemands",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractDemands_ServicePurchaseDemandId",
                table: "SubcontractDemands",
                column: "ServicePurchaseDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractDemands_SiteId",
                table: "SubcontractDemands",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractDemands_Status",
                table: "SubcontractDemands",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractDemands_SubcontractOperationId",
                table: "SubcontractDemands",
                column: "SubcontractOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractDemands_WipItemId",
                table: "SubcontractDemands",
                column: "WipItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractDemands_WipMovementDemandId",
                table: "SubcontractDemands",
                column: "WipMovementDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_CompanyId_ProductionOrderId_Operation~",
                table: "SubcontractOperations",
                columns: new[] { "CompanyId", "ProductionOrderId", "OperationSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_ProductionOrderId",
                table: "SubcontractOperations",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_ReturnToLocationId",
                table: "SubcontractOperations",
                column: "ReturnToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_ServiceItemId",
                table: "SubcontractOperations",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_ServicePurchaseOrderLineId",
                table: "SubcontractOperations",
                column: "ServicePurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_ShipFromLocationId",
                table: "SubcontractOperations",
                column: "ShipFromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_SiteId",
                table: "SubcontractOperations",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_Status",
                table: "SubcontractOperations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_SupplierId",
                table: "SubcontractOperations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOperations_VendorWipWarehouseId",
                table: "SubcontractOperations",
                column: "VendorWipWarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubcontractDemands");

            migrationBuilder.DropTable(
                name: "SubcontractOperations");
        }
    }
}
