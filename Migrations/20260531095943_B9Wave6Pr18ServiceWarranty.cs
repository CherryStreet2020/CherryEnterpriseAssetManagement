using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave6Pr18ServiceWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectServiceHandoffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    HandoffNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InstalledAssetId = table.Column<int>(type: "integer", nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CustomerAssetNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InstallLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InstallDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommissioningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ServiceContractReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PmTemplateReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AsBuiltBomReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AsBuiltDrawingReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartupChecklistComplete = table.Column<bool>(type: "boolean", nullable: false),
                    TrainingCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerSignoff = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerSignoffBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CustomerSignoffAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectServiceHandoffs", x => x.Id);
                    table.CheckConstraint("ck_projectservicehandoffs_number_pos", "\"HandoffNumber\" >= 1");
                    table.CheckConstraint("ck_projectservicehandoffs_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectServiceHandoffs_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectServiceHandoffs_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectWarranties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    WarrantyNumber = table.Column<int>(type: "integer", nullable: false),
                    ProjectServiceHandoffId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WarrantyType = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Terms = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    ClaimCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectWarranties", x => x.Id);
                    table.CheckConstraint("ck_projectwarranties_claimcount_nonneg", "\"ClaimCount\" >= 0");
                    table.CheckConstraint("ck_projectwarranties_number_pos", "\"WarrantyNumber\" >= 1");
                    table.CheckConstraint("ck_projectwarranties_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projectwarranties_type_range", "\"WarrantyType\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectWarranties_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectWarranties_ProjectServiceHandoffs_ProjectServiceHand~",
                        column: x => x.ProjectServiceHandoffId,
                        principalTable: "ProjectServiceHandoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectservicehandoffs_asset",
                table: "ProjectServiceHandoffs",
                column: "InstalledAssetId",
                filter: "\"InstalledAssetId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectservicehandoffs_phase",
                table: "ProjectServiceHandoffs",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectservicehandoffs_project_status",
                table: "ProjectServiceHandoffs",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectservicehandoffs_project_number",
                table: "ProjectServiceHandoffs",
                columns: new[] { "CustomerProjectId", "HandoffNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectwarranties_handoff",
                table: "ProjectWarranties",
                column: "ProjectServiceHandoffId",
                filter: "\"ProjectServiceHandoffId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectwarranties_project_status",
                table: "ProjectWarranties",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectwarranties_project_number",
                table: "ProjectWarranties",
                columns: new[] { "CustomerProjectId", "WarrantyNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectWarranties");

            migrationBuilder.DropTable(
                name: "ProjectServiceHandoffs");
        }
    }
}
