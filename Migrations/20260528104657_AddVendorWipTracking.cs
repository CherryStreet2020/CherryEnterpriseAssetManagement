using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorWipTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    LocationCode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SupplierSiteCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkedWarehouseId = table.Column<int>(type: "integer", nullable: true),
                    LinkedBinLocationId = table.Column<int>(type: "integer", nullable: true),
                    LocationType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    VendorManaged = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerOwnedMaterialAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    ConsignedMaterialAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    WipAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    InspectionRequiredOnReturn = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultShippingMethod = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DefaultTransitDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultReceivingLocationId = table.Column<int>(type: "integer", nullable: true),
                    DefaultReturnToOperationSequence = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorLocations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorLocations_Locations_DefaultReceivingLocationId",
                        column: x => x.DefaultReceivingLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorLocations_Locations_LinkedBinLocationId",
                        column: x => x.LinkedBinLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorLocations_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorLocations_Vendors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorLocations_WarehouseMasters_LinkedWarehouseId",
                        column: x => x.LinkedWarehouseId,
                        principalTable: "WarehouseMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VendorWipBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    OperationSequence = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    VendorLocationId = table.Column<int>(type: "integer", nullable: true),
                    VendorWipWarehouseId = table.Column<int>(type: "integer", nullable: true),
                    VendorWipLocationDescription = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    PartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Revision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    QuantityShipped = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityAtVendor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityReceivedBack = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityAccepted = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityRejected = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityScrappedAtVendor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityLost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    InventoryStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Ownership = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ValuationStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    QualityStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UnitValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalValueAtVendor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FirstShippedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTransactionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiredReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AgingDaysAtVendor = table.Column<int>(type: "integer", nullable: false),
                    SubcontractOperationId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorWipBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorWipBalances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorWipBalances_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorWipBalances_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorWipBalances_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorWipBalances_SubcontractOperations_SubcontractOperatio~",
                        column: x => x.SubcontractOperationId,
                        principalTable: "SubcontractOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorWipBalances_VendorLocations_VendorLocationId",
                        column: x => x.VendorLocationId,
                        principalTable: "VendorLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorWipBalances_Vendors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorWipBalances_WarehouseMasters_VendorWipWarehouseId",
                        column: x => x.VendorWipWarehouseId,
                        principalTable: "WarehouseMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VendorWipTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    TransactionNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    OperationSequence = table.Column<int>(type: "integer", nullable: false),
                    SubcontractOperationId = table.Column<int>(type: "integer", nullable: true),
                    VendorWipBalanceId = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    VendorLocationId = table.Column<int>(type: "integer", nullable: true),
                    PurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    PartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Revision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExtendedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    FromLocationDescription = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ToLocationDescription = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ShipmentDocument = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    ReceiptDocument = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    TransactionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReverseOfTransactionId = table.Column<int>(type: "integer", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorWipTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_SubcontractOperations_SubcontractOper~",
                        column: x => x.SubcontractOperationId,
                        principalTable: "SubcontractOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_VendorLocations_VendorLocationId",
                        column: x => x.VendorLocationId,
                        principalTable: "VendorLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_VendorWipBalances_VendorWipBalanceId",
                        column: x => x.VendorWipBalanceId,
                        principalTable: "VendorWipBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_VendorWipTransactions_ReverseOfTransa~",
                        column: x => x.ReverseOfTransactionId,
                        principalTable: "VendorWipTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorWipTransactions_Vendors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorLocations_CompanyId_SupplierId_LocationCode",
                table: "VendorLocations",
                columns: new[] { "CompanyId", "SupplierId", "LocationCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorLocations_DefaultReceivingLocationId",
                table: "VendorLocations",
                column: "DefaultReceivingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLocations_LinkedBinLocationId",
                table: "VendorLocations",
                column: "LinkedBinLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLocations_LinkedWarehouseId",
                table: "VendorLocations",
                column: "LinkedWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLocations_SiteId",
                table: "VendorLocations",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLocations_SupplierId",
                table: "VendorLocations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_CompanyId",
                table: "VendorWipBalances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_InventoryStatus",
                table: "VendorWipBalances",
                column: "InventoryStatus");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_ItemId",
                table: "VendorWipBalances",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_ProductionOrderId_OperationSequence_Suppl~",
                table: "VendorWipBalances",
                columns: new[] { "ProductionOrderId", "OperationSequence", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_SiteId",
                table: "VendorWipBalances",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_SubcontractOperationId",
                table: "VendorWipBalances",
                column: "SubcontractOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_SupplierId",
                table: "VendorWipBalances",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_VendorLocationId",
                table: "VendorWipBalances",
                column: "VendorLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipBalances_VendorWipWarehouseId",
                table: "VendorWipBalances",
                column: "VendorWipWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_CompanyId_TransactionNumber",
                table: "VendorWipTransactions",
                columns: new[] { "CompanyId", "TransactionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_ItemId",
                table: "VendorWipTransactions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_ProductionOrderId_OperationSequence",
                table: "VendorWipTransactions",
                columns: new[] { "ProductionOrderId", "OperationSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_PurchaseOrderLineId",
                table: "VendorWipTransactions",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_ReverseOfTransactionId",
                table: "VendorWipTransactions",
                column: "ReverseOfTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_SiteId",
                table: "VendorWipTransactions",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_SubcontractOperationId",
                table: "VendorWipTransactions",
                column: "SubcontractOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_SupplierId",
                table: "VendorWipTransactions",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_TransactionType",
                table: "VendorWipTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_VendorLocationId",
                table: "VendorWipTransactions",
                column: "VendorLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorWipTransactions_VendorWipBalanceId",
                table: "VendorWipTransactions",
                column: "VendorWipBalanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorWipTransactions");

            migrationBuilder.DropTable(
                name: "VendorWipBalances");

            migrationBuilder.DropTable(
                name: "VendorLocations");
        }
    }
}
