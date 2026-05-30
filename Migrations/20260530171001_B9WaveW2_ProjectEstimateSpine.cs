using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9WaveW2_ProjectEstimateSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectEstimates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectQuoteId = table.Column<int>(type: "integer", nullable: true),
                    EstimateNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TargetMarginPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    ContingencyPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    EstimatorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEstimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectEstimates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectEstimates_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectEstimates_ProjectQuotes_ProjectQuoteId",
                        column: x => x.ProjectQuoteId,
                        principalTable: "ProjectQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEstimateLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectEstimateId = table.Column<int>(type: "integer", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    CostElementType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ExtendedCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Hours = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEstimateLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectEstimateLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectEstimateLines_ProjectEstimates_ProjectEstimateId",
                        column: x => x.ProjectEstimateId,
                        principalTable: "ProjectEstimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEstimateSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectEstimateId = table.Column<int>(type: "integer", nullable: true),
                    ProjectQuoteRevisionId = table.Column<int>(type: "integer", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    MaterialCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LaborCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SubcontractCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OverheadCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OtherCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DirectTotalCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ContingencyPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuotedPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    EstimatedMarginPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    TargetMarginPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    FrozenLinesJson = table.Column<string>(type: "text", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapturedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEstimateSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectEstimateSnapshots_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectEstimateSnapshots_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectEstimateSnapshots_ProjectEstimates_ProjectEstimateId",
                        column: x => x.ProjectEstimateId,
                        principalTable: "ProjectEstimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQuoteRevisions_SourceEstimateSnapshotId",
                table: "ProjectQuoteRevisions",
                column: "SourceEstimateSnapshotId");

            migrationBuilder.CreateIndex(
                name: "ix_projectestimatelines_costelement",
                table: "ProjectEstimateLines",
                column: "CostElementType");

            migrationBuilder.CreateIndex(
                name: "ix_projectestimatelines_item",
                table: "ProjectEstimateLines",
                column: "ItemId",
                filter: "\"ItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_projectestimatelines_estimate_lineno",
                table: "ProjectEstimateLines",
                columns: new[] { "ProjectEstimateId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectestimates_customerproject",
                table: "ProjectEstimates",
                column: "CustomerProjectId");

            migrationBuilder.CreateIndex(
                name: "ix_projectestimates_quote",
                table: "ProjectEstimates",
                column: "ProjectQuoteId",
                filter: "\"ProjectQuoteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectestimates_status",
                table: "ProjectEstimates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_projectestimates_company_estimatenumber",
                table: "ProjectEstimates",
                columns: new[] { "CompanyId", "EstimateNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEstimateSnapshots_CompanyId",
                table: "ProjectEstimateSnapshots",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "ix_projectestimatesnapshots_customerproject",
                table: "ProjectEstimateSnapshots",
                column: "CustomerProjectId");

            migrationBuilder.CreateIndex(
                name: "ix_projectestimatesnapshots_estimate",
                table: "ProjectEstimateSnapshots",
                column: "ProjectEstimateId",
                filter: "\"ProjectEstimateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectestimatesnapshots_revision",
                table: "ProjectEstimateSnapshots",
                column: "ProjectQuoteRevisionId",
                filter: "\"ProjectQuoteRevisionId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectQuoteRevisions_ProjectEstimateSnapshots_SourceEstima~",
                table: "ProjectQuoteRevisions",
                column: "SourceEstimateSnapshotId",
                principalTable: "ProjectEstimateSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectQuoteRevisions_ProjectEstimateSnapshots_SourceEstima~",
                table: "ProjectQuoteRevisions");

            migrationBuilder.DropTable(
                name: "ProjectEstimateLines");

            migrationBuilder.DropTable(
                name: "ProjectEstimateSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectEstimates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectQuoteRevisions_SourceEstimateSnapshotId",
                table: "ProjectQuoteRevisions");
        }
    }
}
