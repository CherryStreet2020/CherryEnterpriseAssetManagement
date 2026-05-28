using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcontractShipmentReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubcontractShipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ShipmentNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SubcontractOperationId = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    OperationSequence = table.Column<int>(type: "integer", nullable: false),
                    SubcontractDemandId = table.Column<int>(type: "integer", nullable: true),
                    ServicePurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    VendorLocationId = table.Column<int>(type: "integer", nullable: true),
                    ShipFromLocationId = table.Column<int>(type: "integer", nullable: true),
                    VendorWipLocationCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Carrier = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    ShippingMethod = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    FreightCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    FreightCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    RequiredShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CertRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PackingInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_Locations_ShipFromLocationId",
                        column: x => x.ShipFromLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_PurchaseOrderLines_ServicePurchaseOrde~",
                        column: x => x.ServicePurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_SubcontractDemands_SubcontractDemandId",
                        column: x => x.SubcontractDemandId,
                        principalTable: "SubcontractDemands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_SubcontractOperations_SubcontractOpera~",
                        column: x => x.SubcontractOperationId,
                        principalTable: "SubcontractOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_VendorLocations_VendorLocationId",
                        column: x => x.VendorLocationId,
                        principalTable: "VendorLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractShipments_Vendors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    VendorPackingSlip = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    SubcontractOperationId = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    OperationSequence = table.Column<int>(type: "integer", nullable: false),
                    SubcontractShipmentId = table.Column<int>(type: "integer", nullable: true),
                    ServicePurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    VendorLocationId = table.Column<int>(type: "integer", nullable: true),
                    ReceivingLocationId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Carrier = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    CertReceived = table.Column<bool>(type: "boolean", nullable: false),
                    CertReference = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    InspectionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_Locations_ReceivingLocationId",
                        column: x => x.ReceivingLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_PurchaseOrderLines_ServicePurchaseOrder~",
                        column: x => x.ServicePurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_SubcontractOperations_SubcontractOperat~",
                        column: x => x.SubcontractOperationId,
                        principalTable: "SubcontractOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_SubcontractShipments_SubcontractShipmen~",
                        column: x => x.SubcontractShipmentId,
                        principalTable: "SubcontractShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_VendorLocations_VendorLocationId",
                        column: x => x.VendorLocationId,
                        principalTable: "VendorLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractReceipts_Vendors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractShipmentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    SubcontractShipmentId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    PartNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DrawingRevision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    QuantityShipped = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    VendorWipTransactionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractShipmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractShipmentLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractShipmentLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractShipmentLines_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractShipmentLines_SubcontractShipments_SubcontractSh~",
                        column: x => x.SubcontractShipmentId,
                        principalTable: "SubcontractShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubcontractShipmentLines_VendorWipTransactions_VendorWipTra~",
                        column: x => x.VendorWipTransactionId,
                        principalTable: "VendorWipTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractReceiptLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    SubcontractReceiptId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    SubcontractShipmentLineId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    PartNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DrawingRevision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    QuantityReceived = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityAccepted = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityRejected = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityScrappedAtVendor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityShort = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Scenario = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Disposition = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RejectReason = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    NcrReference = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    VendorWipTransactionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractReceiptLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractReceiptLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractReceiptLines_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractReceiptLines_SubcontractReceipts_SubcontractRece~",
                        column: x => x.SubcontractReceiptId,
                        principalTable: "SubcontractReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubcontractReceiptLines_SubcontractShipmentLines_Subcontrac~",
                        column: x => x.SubcontractShipmentLineId,
                        principalTable: "SubcontractShipmentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubcontractReceiptLines_VendorWipTransactions_VendorWipTran~",
                        column: x => x.VendorWipTransactionId,
                        principalTable: "VendorWipTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceiptLines_CompanyId",
                table: "SubcontractReceiptLines",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceiptLines_ItemId",
                table: "SubcontractReceiptLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceiptLines_Scenario",
                table: "SubcontractReceiptLines",
                column: "Scenario");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceiptLines_SiteId",
                table: "SubcontractReceiptLines",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceiptLines_SubcontractReceiptId",
                table: "SubcontractReceiptLines",
                column: "SubcontractReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceiptLines_SubcontractReceiptId_LineNumber",
                table: "SubcontractReceiptLines",
                columns: new[] { "SubcontractReceiptId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceiptLines_SubcontractShipmentLineId",
                table: "SubcontractReceiptLines",
                column: "SubcontractShipmentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceiptLines_VendorWipTransactionId",
                table: "SubcontractReceiptLines",
                column: "VendorWipTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_CompanyId_ReceiptNumber",
                table: "SubcontractReceipts",
                columns: new[] { "CompanyId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_ProductionOrderId_OperationSequence",
                table: "SubcontractReceipts",
                columns: new[] { "ProductionOrderId", "OperationSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_ReceivingLocationId",
                table: "SubcontractReceipts",
                column: "ReceivingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_ServicePurchaseOrderLineId",
                table: "SubcontractReceipts",
                column: "ServicePurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_SiteId",
                table: "SubcontractReceipts",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_Status",
                table: "SubcontractReceipts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_SubcontractOperationId",
                table: "SubcontractReceipts",
                column: "SubcontractOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_SubcontractShipmentId",
                table: "SubcontractReceipts",
                column: "SubcontractShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_SupplierId",
                table: "SubcontractReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractReceipts_VendorLocationId",
                table: "SubcontractReceipts",
                column: "VendorLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipmentLines_CompanyId",
                table: "SubcontractShipmentLines",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipmentLines_ItemId",
                table: "SubcontractShipmentLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipmentLines_SiteId",
                table: "SubcontractShipmentLines",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipmentLines_SubcontractShipmentId",
                table: "SubcontractShipmentLines",
                column: "SubcontractShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipmentLines_SubcontractShipmentId_LineNumber",
                table: "SubcontractShipmentLines",
                columns: new[] { "SubcontractShipmentId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipmentLines_VendorWipTransactionId",
                table: "SubcontractShipmentLines",
                column: "VendorWipTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_CompanyId_ShipmentNumber",
                table: "SubcontractShipments",
                columns: new[] { "CompanyId", "ShipmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_ProductionOrderId_OperationSequence",
                table: "SubcontractShipments",
                columns: new[] { "ProductionOrderId", "OperationSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_ServicePurchaseOrderLineId",
                table: "SubcontractShipments",
                column: "ServicePurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_ShipFromLocationId",
                table: "SubcontractShipments",
                column: "ShipFromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_SiteId",
                table: "SubcontractShipments",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_Status",
                table: "SubcontractShipments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_SubcontractDemandId",
                table: "SubcontractShipments",
                column: "SubcontractDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_SubcontractOperationId",
                table: "SubcontractShipments",
                column: "SubcontractOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_SupplierId",
                table: "SubcontractShipments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractShipments_VendorLocationId",
                table: "SubcontractShipments",
                column: "VendorLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubcontractReceiptLines");

            migrationBuilder.DropTable(
                name: "SubcontractReceipts");

            migrationBuilder.DropTable(
                name: "SubcontractShipmentLines");

            migrationBuilder.DropTable(
                name: "SubcontractShipments");
        }
    }
}
