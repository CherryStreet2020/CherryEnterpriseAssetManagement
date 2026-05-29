using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B7_PR3_VarianceBaselineMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LockedEstimateCapturedUtc",
                table: "ProductionOrderCostSummaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VarianceBaselineMode",
                table: "ProductionOrderCostSummaries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProCostSum_BaselineMode",
                table: "ProductionOrderCostSummaries",
                column: "VarianceBaselineMode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProCostSum_BaselineMode",
                table: "ProductionOrderCostSummaries");

            migrationBuilder.DropColumn(
                name: "LockedEstimateCapturedUtc",
                table: "ProductionOrderCostSummaries");

            migrationBuilder.DropColumn(
                name: "VarianceBaselineMode",
                table: "ProductionOrderCostSummaries");
        }
    }
}
