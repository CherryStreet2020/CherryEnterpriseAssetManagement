using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddImpactAnalysisAndRedlineFs143Pr7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeImpactAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    AnalysisNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EcoId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AffectedProductionOrderCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedDeviationCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedCarCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedDocumentCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedCustomerCount = table.Column<int>(type: "integer", nullable: false),
                    TotalImpactLines = table.Column<int>(type: "integer", nullable: false),
                    ResolvedImpactLines = table.Column<int>(type: "integer", nullable: false),
                    CriticalImpactLines = table.Column<int>(type: "integer", nullable: false),
                    RequiresFaiRetrigger = table.Column<bool>(type: "boolean", nullable: false),
                    FaiTriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FaiTriggeredBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FaiReportsCreated = table.Column<int>(type: "integer", nullable: false),
                    RequiresCustomerNotice = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerNoticeTriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerNoticeTriggeredBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AnalyzedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnalyzedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeImpactAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeImpactAnalyses_EngineeringChangeOrders_EcoId",
                        column: x => x.EcoId,
                        principalTable: "EngineeringChangeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRedlines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    RedlineNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentVersionId = table.Column<int>(type: "integer", nullable: false),
                    EcoId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Severity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AffectedArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MarkupDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SpecificationReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DrawingZone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DrawingView = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AffectsForm = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFit = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFunction = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresFaiRetrigger = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRedlines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRedlines_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRedlines_EngineeringChangeOrders_EcoId",
                        column: x => x.EcoId,
                        principalTable: "EngineeringChangeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentRedlines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChangeImpactLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChangeImpactAnalysisId = table.Column<int>(type: "integer", nullable: false),
                    LineType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Severity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AffectedEntityId = table.Column<int>(type: "integer", nullable: false),
                    AffectedEntityDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AffectedItemId = table.Column<int>(type: "integer", nullable: true),
                    RecommendedAction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ActionTaken = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TriggeredFaiReportId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeImpactLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeImpactLines_ChangeImpactAnalyses_ChangeImpactAnalysis~",
                        column: x => x.ChangeImpactAnalysisId,
                        principalTable: "ChangeImpactAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeImpactLines_Items_AffectedItemId",
                        column: x => x.AffectedItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeImpactAnalyses_CompanyId",
                table: "ChangeImpactAnalyses",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeImpactAnalyses_EcoId",
                table: "ChangeImpactAnalyses",
                column: "EcoId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeImpactAnalyses_TenantId",
                table: "ChangeImpactAnalyses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CIA_Status",
                table: "ChangeImpactAnalyses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_CIA_Company_Eco",
                table: "ChangeImpactAnalyses",
                columns: new[] { "CompanyId", "EcoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CIA_Company_Number",
                table: "ChangeImpactAnalyses",
                columns: new[] { "CompanyId", "AnalysisNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CIL_Analysis",
                table: "ChangeImpactLines",
                column: "ChangeImpactAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_CIL_Item",
                table: "ChangeImpactLines",
                column: "AffectedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CIL_Type",
                table: "ChangeImpactLines",
                column: "LineType");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRedlines_CompanyId",
                table: "DocumentRedlines",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRedlines_ItemId",
                table: "DocumentRedlines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRedlines_TenantId",
                table: "DocumentRedlines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DRL_DocVer",
                table: "DocumentRedlines",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DRL_Eco",
                table: "DocumentRedlines",
                column: "EcoId");

            migrationBuilder.CreateIndex(
                name: "IX_DRL_Status",
                table: "DocumentRedlines",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_DRL_Company_Number",
                table: "DocumentRedlines",
                columns: new[] { "CompanyId", "RedlineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeImpactLines");

            migrationBuilder.DropTable(
                name: "DocumentRedlines");

            migrationBuilder.DropTable(
                name: "ChangeImpactAnalyses");
        }
    }
}
