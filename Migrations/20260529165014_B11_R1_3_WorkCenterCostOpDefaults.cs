using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B11_R1_3_WorkCenterCostOpDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostCenterId",
                table: "WorkCenters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultBackflushMaterials",
                table: "WorkCenters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DefaultCompletionBehavior",
                table: "WorkCenters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultScrapPct",
                table: "WorkCenters",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultYieldPct",
                table: "WorkCenters",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedOverheadRatePerHour",
                table: "WorkCenters",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsCountPoint",
                table: "WorkCenters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LaborRateSource",
                table: "WorkCenters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotingRatePerHour",
                table: "WorkCenters",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RunLaborRatePerHour",
                table: "WorkCenters",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RunMachineRatePerHour",
                table: "WorkCenters",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SetupLaborRatePerHour",
                table: "WorkCenters",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SetupMachineRatePerHour",
                table: "WorkCenters",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VariableOverheadRatePerHour",
                table: "WorkCenters",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenters_CostCenter_Partial",
                table: "WorkCenters",
                column: "CostCenterId",
                filter: "\"CostCenterId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkCenters_CostCenters_CostCenterId",
                table: "WorkCenters",
                column: "CostCenterId",
                principalTable: "CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkCenters_CostCenters_CostCenterId",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_WorkCenters_CostCenter_Partial",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "DefaultBackflushMaterials",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "DefaultCompletionBehavior",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "DefaultScrapPct",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "DefaultYieldPct",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "FixedOverheadRatePerHour",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "IsCountPoint",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "LaborRateSource",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "QuotingRatePerHour",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "RunLaborRatePerHour",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "RunMachineRatePerHour",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "SetupLaborRatePerHour",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "SetupMachineRatePerHour",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "VariableOverheadRatePerHour",
                table: "WorkCenters");
        }
    }
}
