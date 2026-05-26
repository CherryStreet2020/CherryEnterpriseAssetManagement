using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionOrderCostsAndParentFk_Sprint128Pr1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualCost",
                table: "ProductionOrders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCost",
                table: "ProductionOrders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialCost",
                table: "ProductionOrders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverheadCost",
                table: "ProductionOrders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentProductionOrderId",
                table: "ProductionOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubcontractCost",
                table: "ProductionOrders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_ParentProductionOrderId",
                table: "ProductionOrders",
                column: "ParentProductionOrderId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_productionorders_no_self_parent",
                table: "ProductionOrders",
                sql: "\"ParentProductionOrderId\" IS NULL OR \"ParentProductionOrderId\" <> \"Id\"");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_ProductionOrders_ParentProductionOrderId",
                table: "ProductionOrders",
                column: "ParentProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_ProductionOrders_ParentProductionOrderId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_ParentProductionOrderId",
                table: "ProductionOrders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_productionorders_no_self_parent",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ActualCost",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "LaborCost",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "MaterialCost",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "OverheadCost",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ParentProductionOrderId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "SubcontractCost",
                table: "ProductionOrders");
        }
    }
}
