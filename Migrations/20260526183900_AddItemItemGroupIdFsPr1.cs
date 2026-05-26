using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddItemItemGroupIdFsPr1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemGroupId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemGroupId",
                table: "Items",
                column: "ItemGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_ItemGroups_ItemGroupId",
                table: "Items",
                column: "ItemGroupId",
                principalTable: "ItemGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_ItemGroups_ItemGroupId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_ItemGroupId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ItemGroupId",
                table: "Items");
        }
    }
}
