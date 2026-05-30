using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave3Pr7WbsHardenProjectPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualCost",
                table: "ProjectPhases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualFinish",
                table: "ProjectPhases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualStart",
                table: "ProjectPhases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaselineFinish",
                table: "ProjectPhases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaselineStart",
                table: "ProjectPhases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaselinedAt",
                table: "ProjectPhases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaselinedBy",
                table: "ProjectPhases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommittedCost",
                table: "ProjectPhases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControlAccount",
                table: "ProjectPhases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CustomerVisible",
                table: "ProjectPhases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ForecastCost",
                table: "ProjectPhases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ForecastFinish",
                table: "ProjectPhases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ForecastStart",
                table: "ProjectPhases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaselined",
                table: "ProjectPhases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentComplete",
                table: "ProjectPhases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlannedCost",
                table: "ProjectPhases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleDepartment",
                table: "ProjectPhases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleOwner",
                table: "ProjectPhases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ProjectPhases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WbsLevel",
                table: "ProjectPhases",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "WbsType",
                table: "ProjectPhases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPercent",
                table: "ProjectPhases",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ProjectPhases",
                type: "xid",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectphases_baselined",
                table: "ProjectPhases",
                column: "IsBaselined",
                filter: "\"IsBaselined\" = true");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projectphases_costs_nonneg",
                table: "ProjectPhases",
                sql: "(\"PlannedCost\" IS NULL OR \"PlannedCost\" >= 0) AND (\"ActualCost\" IS NULL OR \"ActualCost\" >= 0) AND (\"CommittedCost\" IS NULL OR \"CommittedCost\" >= 0) AND (\"ForecastCost\" IS NULL OR \"ForecastCost\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projectphases_percentcomplete_range",
                table: "ProjectPhases",
                sql: "\"PercentComplete\" IS NULL OR (\"PercentComplete\" >= 0 AND \"PercentComplete\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projectphases_status_range",
                table: "ProjectPhases",
                sql: "\"Status\" BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projectphases_wbslevel_pos",
                table: "ProjectPhases",
                sql: "\"WbsLevel\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projectphases_wbstype_range",
                table: "ProjectPhases",
                sql: "\"WbsType\" BETWEEN 0 AND 9");

            migrationBuilder.AddCheckConstraint(
                name: "ck_projectphases_weightpercent_range",
                table: "ProjectPhases",
                sql: "\"WeightPercent\" IS NULL OR (\"WeightPercent\" >= 0 AND \"WeightPercent\" <= 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_projectphases_baselined",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projectphases_costs_nonneg",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projectphases_percentcomplete_range",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projectphases_status_range",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projectphases_wbslevel_pos",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projectphases_wbstype_range",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_projectphases_weightpercent_range",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ActualCost",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ActualFinish",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ActualStart",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "BaselineFinish",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "BaselineStart",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "BaselinedAt",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "BaselinedBy",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "CommittedCost",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ControlAccount",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "CustomerVisible",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ForecastCost",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ForecastFinish",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ForecastStart",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "IsBaselined",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "PercentComplete",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "PlannedCost",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ResponsibleDepartment",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "ResponsibleOwner",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "WbsLevel",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "WbsType",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "WeightPercent",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProjectPhases");
        }
    }
}
