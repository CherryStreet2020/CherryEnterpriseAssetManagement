using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B11_R2_6_ResourceCalendarsFiniteCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AvailableHoursPerDay",
                table: "ProductionResources",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxBatchSize",
                table: "ProductionResources",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxConcurrentJobs",
                table: "ProductionResources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxJobQuantity",
                table: "ProductionResources",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinBatchSize",
                table: "ProductionResources",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinJobQuantity",
                table: "ProductionResources",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResourceCalendarExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ProductionResourceId = table.Column<int>(type: "integer", nullable: false),
                    ExceptionType = table.Column<int>(type: "integer", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapacityOverridePct = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    SourceWorkOrderId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceCalendarExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceCalendarExceptions_ProductionResources_ProductionRe~",
                        column: x => x.ProductionResourceId,
                        principalTable: "ProductionResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceCalendarExceptions_WorkOrders_SourceWorkOrderId",
                        column: x => x.SourceWorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionResources_Calendar_Partial",
                table: "ProductionResources",
                column: "CalendarId",
                filter: "\"CalendarId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCalendarExceptions_CompanyId",
                table: "ResourceCalendarExceptions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCalendarExceptions_ExceptionType",
                table: "ResourceCalendarExceptions",
                column: "ExceptionType");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCalendarExceptions_Resource_Window",
                table: "ResourceCalendarExceptions",
                columns: new[] { "ProductionResourceId", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCalendarExceptions_WorkOrder_Partial",
                table: "ResourceCalendarExceptions",
                column: "SourceWorkOrderId",
                filter: "\"SourceWorkOrderId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionResources_WorkCalendars_CalendarId",
                table: "ProductionResources",
                column: "CalendarId",
                principalTable: "WorkCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionResources_WorkCalendars_CalendarId",
                table: "ProductionResources");

            migrationBuilder.DropTable(
                name: "ResourceCalendarExceptions");

            migrationBuilder.DropIndex(
                name: "IX_ProductionResources_Calendar_Partial",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "AvailableHoursPerDay",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "MaxBatchSize",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "MaxConcurrentJobs",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "MaxJobQuantity",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "MinBatchSize",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "MinJobQuantity",
                table: "ProductionResources");
        }
    }
}
