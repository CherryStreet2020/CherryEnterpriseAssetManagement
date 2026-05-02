using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Sprint14ProcurementV2Lite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OrderMultiple",
                table: "Items",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastPrice",
                table: "Items",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceEffectiveDate",
                table: "Items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContractFlag",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContractRef",
                table: "Items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockPolicy",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceEffectiveDate",
                table: "VendorItemParts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderMultiple",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "LastPrice",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PriceEffectiveDate",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ContractFlag",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ContractRef",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "StockPolicy",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PriceEffectiveDate",
                table: "VendorItemParts");
        }
    }
}
