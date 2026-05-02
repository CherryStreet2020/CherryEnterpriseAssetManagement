using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace Abs.FixedAssets.Migrations
{
    public partial class AddWorkOrderExecutionFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "MaintenanceEvents",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartedBy",
                table: "MaintenanceEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReason",
                table: "MaintenanceEvents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "StartedBy",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "HoldReason",
                table: "MaintenanceEvents");
        }
    }
}
