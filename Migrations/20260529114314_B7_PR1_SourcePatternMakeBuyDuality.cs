using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B7_PR1_SourcePatternMakeBuyDuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultSourcePreference",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedMakeInvestment",
                table: "Items",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSourceControlled",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MakeBreakEvenQty",
                table: "Items",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MakeBuyPolicy",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceControlReason",
                table: "Items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourcePattern",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Items_IsSourceControlled",
                table: "Items",
                column: "IsSourceControlled");

            migrationBuilder.CreateIndex(
                name: "IX_Items_MakeBuyPolicy",
                table: "Items",
                column: "MakeBuyPolicy");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SourcePattern",
                table: "Items",
                column: "SourcePattern");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_IsSourceControlled",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_MakeBuyPolicy",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_SourcePattern",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DefaultSourcePreference",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "FixedMakeInvestment",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsSourceControlled",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MakeBreakEvenQty",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MakeBuyPolicy",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SourceControlReason",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SourcePattern",
                table: "Items");
        }
    }
}
