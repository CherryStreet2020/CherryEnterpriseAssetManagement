using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B11_R1_2_WorkCenterHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BottleneckFlag",
                table: "WorkCenters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ConstraintPriority",
                table: "WorkCenters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CrewSizeRequired",
                table: "WorkCenters",
                type: "numeric(9,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DispatchRule",
                table: "WorkCenters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParallelMachineCount",
                table: "WorkCenters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrimaryResourceSelectionRule",
                table: "WorkCenters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SchedulingEnabled",
                table: "WorkCenters",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetupFamilyCode",
                table: "WorkCenters",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetupFamilyRule",
                table: "WorkCenters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SiteId",
                table: "WorkCenters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "WorkCenters",
                type: "xid",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkCenterAlternates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    WorkCenterId = table.Column<int>(type: "integer", nullable: false),
                    AlternateWorkCenterId = table.Column<int>(type: "integer", nullable: false),
                    Preference = table.Column<int>(type: "integer", nullable: false),
                    EfficiencyFactor = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCenterAlternates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkCenterAlternates_WorkCenters_AlternateWorkCenterId",
                        column: x => x.AlternateWorkCenterId,
                        principalTable: "WorkCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkCenterAlternates_WorkCenters_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "WorkCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenters_Bottleneck_Partial",
                table: "WorkCenters",
                column: "BottleneckFlag",
                filter: "\"BottleneckFlag\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenters_Site_Partial",
                table: "WorkCenters",
                column: "SiteId",
                filter: "\"SiteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterAlternates_AlternateWorkCenterId",
                table: "WorkCenterAlternates",
                column: "AlternateWorkCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterAlternates_CompanyId",
                table: "WorkCenterAlternates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenterAlternates_WC_Preference",
                table: "WorkCenterAlternates",
                columns: new[] { "WorkCenterId", "Preference" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkCenterAlternates_WC_Alt",
                table: "WorkCenterAlternates",
                columns: new[] { "WorkCenterId", "AlternateWorkCenterId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Sites_SiteId",
                table: "Departments",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkCenters_Sites_SiteId",
                table: "WorkCenters",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Sites_SiteId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkCenters_Sites_SiteId",
                table: "WorkCenters");

            migrationBuilder.DropTable(
                name: "WorkCenterAlternates");

            migrationBuilder.DropIndex(
                name: "IX_WorkCenters_Bottleneck_Partial",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_WorkCenters_Site_Partial",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "BottleneckFlag",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "ConstraintPriority",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "CrewSizeRequired",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "DispatchRule",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "ParallelMachineCount",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "PrimaryResourceSelectionRule",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "SchedulingEnabled",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "SetupFamilyCode",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "SetupFamilyRule",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "WorkCenters");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "WorkCenters");
        }
    }
}
