using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddPoLineDemandLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BomLineId",
                table: "PurchaseOrderLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDirectToJob",
                table: "PurchaseOrderLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubcontract",
                table: "PurchaseOrderLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OperationSequence",
                table: "PurchaseOrderLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionOrderId",
                table: "PurchaseOrderLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "PurchaseOrderLines",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLineDemandLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    PurchaseOrderLineId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderReleaseId = table.Column<int>(type: "integer", nullable: true),
                    ProductionSupplyDemandId = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    BomLineId = table.Column<int>(type: "integer", nullable: true),
                    OperationSequence = table.Column<int>(type: "integer", nullable: true),
                    AllocatedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPriceAtLink = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PromiseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NeedByDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstReceiptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FullyReceivedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLineDemandLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineDemandLinks_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineDemandLinks_Locations_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineDemandLinks_ProductionMaterialStructures_B~",
                        column: x => x.BomLineId,
                        principalTable: "ProductionMaterialStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineDemandLinks_ProductionOrders_ProductionOrd~",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineDemandLinks_ProductionSupplyDemands_Produc~",
                        column: x => x.ProductionSupplyDemandId,
                        principalTable: "ProductionSupplyDemands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineDemandLinks_PurchaseOrderLines_PurchaseOrd~",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLineDemandLinks_PurchaseOrderReleases_Purchase~",
                        column: x => x.PurchaseOrderReleaseId,
                        principalTable: "PurchaseOrderReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_BomLineId",
                table: "PurchaseOrderLines",
                column: "BomLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ProductionOrderId",
                table: "PurchaseOrderLines",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_BomLineId",
                table: "PurchaseOrderLineDemandLinks",
                column: "BomLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_CompanyId",
                table: "PurchaseOrderLineDemandLinks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_ProductionOrderId",
                table: "PurchaseOrderLineDemandLinks",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_ProductionSupplyDemandId",
                table: "PurchaseOrderLineDemandLinks",
                column: "ProductionSupplyDemandId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_PurchaseOrderLineId",
                table: "PurchaseOrderLineDemandLinks",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_PurchaseOrderLineId_Production~",
                table: "PurchaseOrderLineDemandLinks",
                columns: new[] { "PurchaseOrderLineId", "ProductionSupplyDemandId", "PurchaseOrderReleaseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_PurchaseOrderReleaseId",
                table: "PurchaseOrderLineDemandLinks",
                column: "PurchaseOrderReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_SiteId",
                table: "PurchaseOrderLineDemandLinks",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLineDemandLinks_Status",
                table: "PurchaseOrderLineDemandLinks",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_ProductionMaterialStructures_BomLineId",
                table: "PurchaseOrderLines",
                column: "BomLineId",
                principalTable: "ProductionMaterialStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_ProductionOrders_ProductionOrderId",
                table: "PurchaseOrderLines",
                column: "ProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_ProductionMaterialStructures_BomLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_ProductionOrders_ProductionOrderId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLineDemandLinks");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_BomLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_ProductionOrderId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "BomLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "IsDirectToJob",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "IsSubcontract",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "OperationSequence",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "ProductionOrderId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PurchaseOrderLines");
        }
    }
}
