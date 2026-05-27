using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionOrderHeaderExpansionB8Po1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DrawingRevision",
                table: "ProductionOrders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FreezeBom",
                table: "ProductionOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FreezeCost",
                table: "ProductionOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FreezeRouting",
                table: "ProductionOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FreezeSchedule",
                table: "ProductionOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HoldReason",
                table: "ProductionOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReasonNotes",
                table: "ProductionOrders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LotSerialRequirement",
                table: "ProductionOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlannerUserId",
                table: "ProductionOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromiseDate",
                table: "ProductionOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityReleased",
                table: "ProductionOrders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityRework",
                table: "ProductionOrders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SupervisorUserId",
                table: "ProductionOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkInstructionsRevision",
                table: "ProductionOrders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_HoldReason",
                table: "ProductionOrders",
                column: "HoldReason");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_PlannerUserId",
                table: "ProductionOrders",
                column: "PlannerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_PromiseDate",
                table: "ProductionOrders",
                column: "PromiseDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_SupervisorUserId",
                table: "ProductionOrders",
                column: "SupervisorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Users_PlannerUserId",
                table: "ProductionOrders",
                column: "PlannerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Users_SupervisorUserId",
                table: "ProductionOrders",
                column: "SupervisorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Users_PlannerUserId",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Users_SupervisorUserId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_HoldReason",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_PlannerUserId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_PromiseDate",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_SupervisorUserId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "DrawingRevision",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "FreezeBom",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "FreezeCost",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "FreezeRouting",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "FreezeSchedule",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "HoldReason",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "HoldReasonNotes",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "LotSerialRequirement",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "PlannerUserId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "PromiseDate",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "QuantityReleased",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "QuantityRework",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "SupervisorUserId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "WorkInstructionsRevision",
                table: "ProductionOrders");
        }
    }
}
