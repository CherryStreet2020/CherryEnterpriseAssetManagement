using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialSupplyLinkB8Pro7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerOrPlanner",
                table: "ProductionMaterialStructures",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSupplyRefreshUtc",
                table: "ProductionMaterialStructures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LateToNeedDate",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LinkedSupplyLineId",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedSupplyRecordId",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedSupplyRecordNumber",
                table: "ProductionMaterialStructures",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedSupplyRecordType",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaterialSupplyStatus",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaterialSupplyType",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SupplierOrDepartment",
                table: "ProductionMaterialStructures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupplyAvailableDate",
                table: "ProductionMaterialStructures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplyNotes",
                table: "ProductionMaterialStructures",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupplyPromisedDate",
                table: "ProductionMaterialStructures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplyQuantityReceived",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplyQuantityRemaining",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplyQuantityRequired",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplyQuantitySupplied",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupplyRequiredDate",
                table: "ProductionMaterialStructures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplyRisk",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_LinkedSupply",
                table: "ProductionMaterialStructures",
                columns: new[] { "LinkedSupplyRecordType", "LinkedSupplyRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_SupplyRequiredDate",
                table: "ProductionMaterialStructures",
                column: "SupplyRequiredDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_SupplyRisk",
                table: "ProductionMaterialStructures",
                column: "SupplyRisk");

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_SupplyStatus",
                table: "ProductionMaterialStructures",
                column: "MaterialSupplyStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProdMatStruct_LinkedSupply",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropIndex(
                name: "IX_ProdMatStruct_SupplyRequiredDate",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropIndex(
                name: "IX_ProdMatStruct_SupplyRisk",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropIndex(
                name: "IX_ProdMatStruct_SupplyStatus",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "BuyerOrPlanner",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "LastSupplyRefreshUtc",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "LateToNeedDate",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "LinkedSupplyLineId",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "LinkedSupplyRecordId",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "LinkedSupplyRecordNumber",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "LinkedSupplyRecordType",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "MaterialSupplyStatus",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "MaterialSupplyType",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplierOrDepartment",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyAvailableDate",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyNotes",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyPromisedDate",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyQuantityReceived",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyQuantityRemaining",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyQuantityRequired",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyQuantitySupplied",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyRequiredDate",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyRisk",
                table: "ProductionMaterialStructures");
        }
    }
}
