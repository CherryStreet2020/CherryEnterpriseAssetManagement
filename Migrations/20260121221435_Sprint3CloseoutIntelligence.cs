using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3CloseoutIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "MaintenanceEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedBy",
                table: "MaintenanceEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessonsLearned",
                table: "MaintenanceEvents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionSummary",
                table: "MaintenanceEvents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LessonsLearned",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    AssetCategoryId = table.Column<int>(type: "integer", nullable: true),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceWorkOrderId = table.Column<int>(type: "integer", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonsLearned", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonsLearned_AssetCategories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "AssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LessonsLearned_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonsLearned_MaintenanceEvents_SourceWorkOrderId",
                        column: x => x.SourceWorkOrderId,
                        principalTable: "MaintenanceEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LessonsLearned_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonsLearned_AssetCategoryId",
                table: "LessonsLearned",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonsLearned_CompanyId",
                table: "LessonsLearned",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonsLearned_FailureCode",
                table: "LessonsLearned",
                column: "FailureCode");

            migrationBuilder.CreateIndex(
                name: "IX_LessonsLearned_SiteId",
                table: "LessonsLearned",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonsLearned_SourceWorkOrderId",
                table: "LessonsLearned",
                column: "SourceWorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonsLearned");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "LessonsLearned",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "ResolutionSummary",
                table: "MaintenanceEvents");
        }
    }
}
