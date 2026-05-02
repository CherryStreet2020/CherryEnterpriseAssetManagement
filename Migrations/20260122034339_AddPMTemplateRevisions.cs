using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddPMTemplateRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentReleasedRevisionId",
                table: "PMTemplates",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PMTemplateRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PMTemplateId = table.Column<int>(type: "integer", nullable: false),
                    RevisionCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupersedesRevisionId = table.Column<int>(type: "integer", nullable: true),
                    ChangeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    CalendarInterval = table.Column<int>(type: "integer", nullable: false),
                    CalendarIntervalValue = table.Column<int>(type: "integer", nullable: false),
                    MeterType = table.Column<int>(type: "integer", nullable: true),
                    MeterInterval = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    EstimatedLaborCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedPartsCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedTotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RequiresShutdown = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresLOTO = table.Column<bool>(type: "boolean", nullable: false),
                    SkillLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Craft = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Procedure = table.Column<string>(type: "text", nullable: true),
                    SafetyInstructions = table.Column<string>(type: "text", nullable: true),
                    ToolsRequired = table.Column<string>(type: "text", nullable: true),
                    ReferenceDocuments = table.Column<string>(type: "text", nullable: true),
                    AssetCategoryId = table.Column<int>(type: "integer", nullable: true),
                    ManufacturerId = table.Column<int>(type: "integer", nullable: true),
                    ModelPattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsOEMRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    OEMReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsRegulatoryRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RegulatoryReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObsoletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PMTemplateRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PMTemplateRevisions_PMTemplateRevisions_SupersedesRevisionId",
                        column: x => x.SupersedesRevisionId,
                        principalTable: "PMTemplateRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PMTemplateRevisions_PMTemplates_PMTemplateId",
                        column: x => x.PMTemplateId,
                        principalTable: "PMTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PMTemplateRevisionOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PMTemplateRevisionId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    Craft = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PMTemplateRevisionOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PMTemplateRevisionOperations_PMTemplateRevisions_PMTemplateRevisionId",
                        column: x => x.PMTemplateRevisionId,
                        principalTable: "PMTemplateRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplates_CurrentReleasedRevisionId",
                table: "PMTemplates",
                column: "CurrentReleasedRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplateRevisionOperations_PMTemplateRevisionId_Sequence",
                table: "PMTemplateRevisionOperations",
                columns: new[] { "PMTemplateRevisionId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplateRevisions_PMTemplateId_RevisionCode",
                table: "PMTemplateRevisions",
                columns: new[] { "PMTemplateId", "RevisionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplateRevisions_SupersedesRevisionId",
                table: "PMTemplateRevisions",
                column: "SupersedesRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PMTemplates_PMTemplateRevisions_CurrentReleasedRevisionId",
                table: "PMTemplates",
                column: "CurrentReleasedRevisionId",
                principalTable: "PMTemplateRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PMTemplates_PMTemplateRevisions_CurrentReleasedRevisionId",
                table: "PMTemplates");

            migrationBuilder.DropTable(
                name: "PMTemplateRevisionOperations");

            migrationBuilder.DropTable(
                name: "PMTemplateRevisions");

            migrationBuilder.DropIndex(
                name: "IX_PMTemplates_CurrentReleasedRevisionId",
                table: "PMTemplates");

            migrationBuilder.DropColumn(
                name: "CurrentReleasedRevisionId",
                table: "PMTemplates");
        }
    }
}
