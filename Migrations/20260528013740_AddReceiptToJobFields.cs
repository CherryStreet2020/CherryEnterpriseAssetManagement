using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptToJobFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DirectToJobBomLineId",
                table: "GoodsReceiptLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DirectToJobPostedUtc",
                table: "GoodsReceiptLines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DirectToJobProductionOrderId",
                table: "GoodsReceiptLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InspectionRequired",
                table: "GoodsReceiptLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDirectToJob",
                table: "GoodsReceiptLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_DirectToJobBomLineId",
                table: "GoodsReceiptLines",
                column: "DirectToJobBomLineId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_DirectToJobProductionOrderId",
                table: "GoodsReceiptLines",
                column: "DirectToJobProductionOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptLines_ProductionMaterialStructures_DirectToJobB~",
                table: "GoodsReceiptLines",
                column: "DirectToJobBomLineId",
                principalTable: "ProductionMaterialStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptLines_ProductionOrders_DirectToJobProductionOrd~",
                table: "GoodsReceiptLines",
                column: "DirectToJobProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptLines_ProductionMaterialStructures_DirectToJobB~",
                table: "GoodsReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptLines_ProductionOrders_DirectToJobProductionOrd~",
                table: "GoodsReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptLines_DirectToJobBomLineId",
                table: "GoodsReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptLines_DirectToJobProductionOrderId",
                table: "GoodsReceiptLines");

            migrationBuilder.DropColumn(
                name: "DirectToJobBomLineId",
                table: "GoodsReceiptLines");

            migrationBuilder.DropColumn(
                name: "DirectToJobPostedUtc",
                table: "GoodsReceiptLines");

            migrationBuilder.DropColumn(
                name: "DirectToJobProductionOrderId",
                table: "GoodsReceiptLines");

            migrationBuilder.DropColumn(
                name: "InspectionRequired",
                table: "GoodsReceiptLines");

            migrationBuilder.DropColumn(
                name: "IsDirectToJob",
                table: "GoodsReceiptLines");
        }
    }
}
