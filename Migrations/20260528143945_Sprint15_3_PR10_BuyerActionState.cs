using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Sprint15_3_PR10_BuyerActionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerActionNotes",
                table: "ProductionSupplyDemands",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BuyerActionState",
                table: "ProductionSupplyDemands",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BuyerActionStateUpdatedBy",
                table: "ProductionSupplyDemands",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BuyerActionStateUpdatedUtc",
                table: "ProductionSupplyDemands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_BuyerActionState",
                table: "ProductionSupplyDemands",
                column: "BuyerActionState");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSupplyDemands_CompanyId_BuyerUserId_BuyerActionSt~",
                table: "ProductionSupplyDemands",
                columns: new[] { "CompanyId", "BuyerUserId", "BuyerActionState" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionSupplyDemands_BuyerActionState",
                table: "ProductionSupplyDemands");

            migrationBuilder.DropIndex(
                name: "IX_ProductionSupplyDemands_CompanyId_BuyerUserId_BuyerActionSt~",
                table: "ProductionSupplyDemands");

            migrationBuilder.DropColumn(
                name: "BuyerActionNotes",
                table: "ProductionSupplyDemands");

            migrationBuilder.DropColumn(
                name: "BuyerActionState",
                table: "ProductionSupplyDemands");

            migrationBuilder.DropColumn(
                name: "BuyerActionStateUpdatedBy",
                table: "ProductionSupplyDemands");

            migrationBuilder.DropColumn(
                name: "BuyerActionStateUpdatedUtc",
                table: "ProductionSupplyDemands");
        }
    }
}
