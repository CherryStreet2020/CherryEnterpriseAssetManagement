using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionSupplyDemand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionSupplyDemands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    DemandNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    BomLineId = table.Column<int>(type: "integer", nullable: true),
                    OperationSequence = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    WbsElement = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    SalesOrderId = table.Column<int>(type: "integer", nullable: true),
                    SalesOrderNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    ParentDemandId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    PartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Revision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RequiredQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReservedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SuppliedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiredOperationStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiredOperationCompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NeedByDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OnDockDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SupplyPolicy = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    SourcingRuleId = table.Column<int>(type: "integer", nullable: true),
                    BuyerUserId = table.Column<int>(type: "integer", nullable: true),
                    PlannerUserId = table.Column<int>(type: "integer", nullable: true),
                    VendorId = table.Column<int>(type: "integer", nullable: true),
                    VendorSiteCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WorkCenterId = table.Column<int>(type: "integer", nullable: true),
                    VendorResource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WarehouseId = table.Column<int>(type: "integer", nullable: true),
                    BinLocationId = table.Column<int>(type: "integer", nullable: true),
                    VendorWipLocation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InspectionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CertRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DrawingSpecRevision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CustomerOwned = table.Column<bool>(type: "boolean", nullable: false),
                    Consigned = table.Column<bool>(type: "boolean", nullable: false),
                    ItarOrExportControlled = table.Column<bool>(type: "boolean", nullable: false),
                    SourceStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SupplyStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ShortageStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CostStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AlertStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LinkedPurchaseOrderId = table.Column<int>(type: "integer", nullable: true),
                    LinkedPurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    LinkedChildProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    LinkedInventoryReservation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LinkedTransferOrder = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LinkedSubcontractShipment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LinkedGoodsReceiptId = table.Column<int>(type: "integer", nullable: true),
                    LinkedVendorInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastRefreshedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionSupplyDemands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_CipProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "CipProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_GoodsReceipts_LinkedGoodsReceiptId",
                        column: x => x.LinkedGoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_Locations_BinLocationId",
                        column: x => x.BinLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_ProductionMaterialStructures_BomLin~",
                        column: x => x.BomLineId,
                        principalTable: "ProductionMaterialStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_ProductionOrders_LinkedChildProduct~",
                        column: x => x.LinkedChildProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_ProductionSupplyDemands_ParentDeman~",
                        column: x => x.ParentDemandId,
                        principalTable: "ProductionSupplyDemands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_PurchaseOrderLines_LinkedPurchaseOr~",
                        column: x => x.LinkedPurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_PurchaseOrders_LinkedPurchaseOrderId",
                        column: x => x.LinkedPurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_Users_BuyerUserId",
                        column: x => x.BuyerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_Users_PlannerUserId",
                        column: x => x.PlannerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_VendorInvoices_LinkedVendorInvoiceId",
                        column: x => x.LinkedVendorInvoiceId,
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_WarehouseMasters_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "WarehouseMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyDemands_WorkCenters_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "WorkCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductionSupplyAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ProductionSupplyDemandId = table.Column<int>(type: "integer", nullable: false),
                    SupplyType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SupplyRecordId = table.Column<int>(type: "integer", nullable: false),
                    SupplyRecordLineId = table.Column<int>(type: "integer", nullable: true),
                    PurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    ChildProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    AllocatedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PromiseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FullyConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionSupplyAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyAllocations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyAllocations_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyAllocations_ProductionOrders_ChildProductio~",
                        column: x => x.ChildProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyAllocations_ProductionSupplyDemands_Product~",
                        column: x => x.ProductionSupplyDemandId,
                        principalTable: "ProductionSupplyDemands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionSupplyAllocations_PurchaseOrderLines_PurchaseOrde~",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyAllocations_ChildProductionOrderId",
                table: "ProductionSupplyAllocations",
                column: "ChildProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyAllocations_CompanyId",
                table: "ProductionSupplyAllocations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyAllocations_ProductionSupplyDemandId",
                table: "ProductionSupplyAllocations",
                column: "ProductionSupplyDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyAllocations_PurchaseOrderLineId",
                table: "ProductionSupplyAllocations",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyAllocations_SiteId",
                table: "ProductionSupplyAllocations",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyAllocations_Status",
                table: "ProductionSupplyAllocations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyAllocations_SupplyType_SupplyRecordId_Suppl~",
                table: "ProductionSupplyAllocations",
                columns: new[] { "SupplyType", "SupplyRecordId", "SupplyRecordLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_AlertStatus",
                table: "ProductionSupplyDemands",
                column: "AlertStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_BinLocationId",
                table: "ProductionSupplyDemands",
                column: "BinLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_BomLineId",
                table: "ProductionSupplyDemands",
                column: "BomLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_BuyerUserId",
                table: "ProductionSupplyDemands",
                column: "BuyerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_CompanyId_DemandNumber",
                table: "ProductionSupplyDemands",
                columns: new[] { "CompanyId", "DemandNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_CustomerId",
                table: "ProductionSupplyDemands",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_ItemId",
                table: "ProductionSupplyDemands",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_LinkedChildProductionOrderId",
                table: "ProductionSupplyDemands",
                column: "LinkedChildProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_LinkedGoodsReceiptId",
                table: "ProductionSupplyDemands",
                column: "LinkedGoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_LinkedPurchaseOrderId",
                table: "ProductionSupplyDemands",
                column: "LinkedPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_LinkedPurchaseOrderLineId",
                table: "ProductionSupplyDemands",
                column: "LinkedPurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_LinkedVendorInvoiceId",
                table: "ProductionSupplyDemands",
                column: "LinkedVendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_ParentDemandId",
                table: "ProductionSupplyDemands",
                column: "ParentDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_PlannerUserId",
                table: "ProductionSupplyDemands",
                column: "PlannerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_ProductionOrderId_BomLineId",
                table: "ProductionSupplyDemands",
                columns: new[] { "ProductionOrderId", "BomLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_ProjectId",
                table: "ProductionSupplyDemands",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_ShortageStatus",
                table: "ProductionSupplyDemands",
                column: "ShortageStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_SiteId",
                table: "ProductionSupplyDemands",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_SupplyStatus",
                table: "ProductionSupplyDemands",
                column: "SupplyStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_VendorId",
                table: "ProductionSupplyDemands",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_WarehouseId",
                table: "ProductionSupplyDemands",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_WorkCenterId",
                table: "ProductionSupplyDemands",
                column: "WorkCenterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionSupplyAllocations");

            migrationBuilder.DropTable(
                name: "ProductionSupplyDemands");
        }
    }
}
