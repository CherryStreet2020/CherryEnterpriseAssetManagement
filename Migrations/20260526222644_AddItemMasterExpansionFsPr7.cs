using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddItemMasterExpansionFsPr7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AS9100Critical",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EAR99",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ECCN",
                table: "Items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FrozenStandardCost",
                table: "Items",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FrozenStandardCostEffectiveAtUtc",
                table: "Items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InspectionPlanId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntrastatCode",
                table: "Items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPhantom",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSellable",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ItemFamily",
                table: "Items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KeyCharacteristic",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleStage",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LotSizingRule",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MakeBuyCode",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MrpPlannerCode",
                table: "Items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanningPolicy",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFai",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresKitting",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleB",
                table: "Items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_AS9100Critical",
                table: "Items",
                column: "AS9100Critical");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ECCN",
                table: "Items",
                column: "ECCN");

            migrationBuilder.CreateIndex(
                name: "IX_Items_IntrastatCode",
                table: "Items",
                column: "IntrastatCode");

            migrationBuilder.CreateIndex(
                name: "IX_Items_IsSellable",
                table: "Items",
                column: "IsSellable");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemFamily",
                table: "Items",
                column: "ItemFamily");

            migrationBuilder.CreateIndex(
                name: "IX_Items_LifecycleStage",
                table: "Items",
                column: "LifecycleStage");

            migrationBuilder.CreateIndex(
                name: "IX_Items_LotSizingRule",
                table: "Items",
                column: "LotSizingRule");

            migrationBuilder.CreateIndex(
                name: "IX_Items_MakeBuyCode",
                table: "Items",
                column: "MakeBuyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Items_MrpPlannerCode",
                table: "Items",
                column: "MrpPlannerCode");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PlanningPolicy",
                table: "Items",
                column: "PlanningPolicy");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ScheduleB",
                table: "Items",
                column: "ScheduleB");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_AS9100Critical",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_ECCN",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_IntrastatCode",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_IsSellable",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_ItemFamily",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_LifecycleStage",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_LotSizingRule",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_MakeBuyCode",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_MrpPlannerCode",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_PlanningPolicy",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_ScheduleB",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "AS9100Critical",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "EAR99",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ECCN",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "FrozenStandardCost",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "FrozenStandardCostEffectiveAtUtc",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "InspectionPlanId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IntrastatCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsPhantom",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsSellable",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ItemFamily",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "KeyCharacteristic",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "LifecycleStage",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "LotSizingRule",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MakeBuyCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MrpPlannerCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PlanningPolicy",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RequiresFai",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RequiresKitting",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ScheduleB",
                table: "Items");
        }
    }
}
