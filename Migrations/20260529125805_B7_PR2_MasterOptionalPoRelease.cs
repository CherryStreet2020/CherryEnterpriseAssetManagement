using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B7_PR2_MasterOptionalPoRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AsPlannedDescription",
                table: "ProductionOrders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsPlannedDrawingNumber",
                table: "ProductionOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsPlannedDrawingRev",
                table: "ProductionOrders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsPlannedPartNumber",
                table: "ProductionOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CrystallizedItemId",
                table: "ProductionOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPoFirst",
                table: "ProductionOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CrystallizedItemId_Partial",
                table: "ProductionOrders",
                column: "CrystallizedItemId",
                filter: "\"CrystallizedItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_IsPoFirst_Partial",
                table: "ProductionOrders",
                column: "IsPoFirst",
                filter: "\"IsPoFirst\" = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Items_CrystallizedItemId",
                table: "ProductionOrders",
                column: "CrystallizedItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Items_CrystallizedItemId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_CrystallizedItemId_Partial",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_IsPoFirst_Partial",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "AsPlannedDescription",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "AsPlannedDrawingNumber",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "AsPlannedDrawingRev",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "AsPlannedPartNumber",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "CrystallizedItemId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "IsPoFirst",
                table: "ProductionOrders");
        }
    }
}
