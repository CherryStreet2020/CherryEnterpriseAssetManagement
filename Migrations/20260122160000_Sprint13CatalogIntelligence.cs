using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    public partial class Sprint13CatalogIntelligence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalImageUrl",
                table: "Items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogUrl",
                table: "VendorItemParts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalImageUrl",
                table: "VendorItemParts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedMpn",
                table: "VendorItemParts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedSku",
                table: "VendorItemParts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEnrichedUtc",
                table: "VendorItemParts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEnrichStatus",
                table: "VendorItemParts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_ImagePath",
                table: "Items",
                column: "ImagePath");

            migrationBuilder.CreateIndex(
                name: "IX_VendorItemParts_CatalogUrl",
                table: "VendorItemParts",
                column: "CatalogUrl");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_ImagePath",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_VendorItemParts_CatalogUrl",
                table: "VendorItemParts");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ExternalImageUrl",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CatalogUrl",
                table: "VendorItemParts");

            migrationBuilder.DropColumn(
                name: "ExternalImageUrl",
                table: "VendorItemParts");

            migrationBuilder.DropColumn(
                name: "ExtractedMpn",
                table: "VendorItemParts");

            migrationBuilder.DropColumn(
                name: "ExtractedSku",
                table: "VendorItemParts");

            migrationBuilder.DropColumn(
                name: "LastEnrichedUtc",
                table: "VendorItemParts");

            migrationBuilder.DropColumn(
                name: "LastEnrichStatus",
                table: "VendorItemParts");
        }
    }
}
