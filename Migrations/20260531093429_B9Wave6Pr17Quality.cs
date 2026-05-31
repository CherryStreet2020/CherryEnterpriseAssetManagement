using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave6Pr17Quality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectAcceptances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    AcceptanceNumber = table.Column<int>(type: "integer", nullable: false),
                    AcceptanceType = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CustomerContact = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RequiredCriteria = table.Column<string>(type: "text", nullable: true),
                    RequiredDocuments = table.Column<string>(type: "text", nullable: true),
                    InspectionResult = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcceptedQuantity = table.Column<int>(type: "integer", nullable: false),
                    RejectedQuantity = table.Column<int>(type: "integer", nullable: false),
                    AcceptanceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Signature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RevenueTrigger = table.Column<bool>(type: "boolean", nullable: false),
                    WarrantyTrigger = table.Column<bool>(type: "boolean", nullable: false),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAcceptances", x => x.Id);
                    table.CheckConstraint("ck_projectacceptances_number_pos", "\"AcceptanceNumber\" >= 1");
                    table.CheckConstraint("ck_projectacceptances_qty_nonneg", "\"AcceptedQuantity\" >= 0 AND \"RejectedQuantity\" >= 0");
                    table.CheckConstraint("ck_projectacceptances_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectacceptances_type_range", "\"AcceptanceType\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ProjectAcceptances_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectAcceptances_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectInspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    InspectionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InspectionType = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Result = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    InspectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Inspector = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    QuantityInspected = table.Column<int>(type: "integer", nullable: false),
                    QuantityAccepted = table.Column<int>(type: "integer", nullable: false),
                    QuantityRejected = table.Column<int>(type: "integer", nullable: false),
                    ReportReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInspections", x => x.Id);
                    table.CheckConstraint("ck_projectinspections_number_pos", "\"InspectionNumber\" >= 1");
                    table.CheckConstraint("ck_projectinspections_qty_nonneg", "\"QuantityInspected\" >= 0 AND \"QuantityAccepted\" >= 0 AND \"QuantityRejected\" >= 0");
                    table.CheckConstraint("ck_projectinspections_result_range", "\"Result\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectinspections_type_range", "\"InspectionType\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_ProjectInspections_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectInspections_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPunchItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    PunchNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CustomerVisible = table.Column<bool>(type: "boolean", nullable: false),
                    BlockingShipment = table.Column<bool>(type: "boolean", nullable: false),
                    BlockingInvoice = table.Column<bool>(type: "boolean", nullable: false),
                    BlockingAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    CorrectiveAction = table.Column<string>(type: "text", nullable: true),
                    CompletionEvidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPunchItems", x => x.Id);
                    table.CheckConstraint("ck_projectpunchitems_number_pos", "\"PunchNumber\" >= 1");
                    table.CheckConstraint("ck_projectpunchitems_priority_range", "\"Priority\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectpunchitems_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ProjectPunchItems_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectPunchItems_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectNCRs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    NcrNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Severity = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    DetectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuantityAffected = table.Column<int>(type: "integer", nullable: false),
                    ContainmentAction = table.Column<string>(type: "text", nullable: true),
                    Disposition = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    RootCause = table.Column<string>(type: "text", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    BlocksShipment = table.Column<bool>(type: "boolean", nullable: false),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    LinkedInspectionId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectNCRs", x => x.Id);
                    table.CheckConstraint("ck_projectncrs_disposition_range", "\"Disposition\" BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_projectncrs_number_pos", "\"NcrNumber\" >= 1");
                    table.CheckConstraint("ck_projectncrs_qty_nonneg", "\"QuantityAffected\" >= 0");
                    table.CheckConstraint("ck_projectncrs_severity_range", "\"Severity\" BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_projectncrs_source_range", "\"Source\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectncrs_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectNCRs_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectNCRs_ProjectInspections_LinkedInspectionId",
                        column: x => x.LinkedInspectionId,
                        principalTable: "ProjectInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectNCRs_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMRBs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    MrbNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LinkedNcrId = table.Column<int>(type: "integer", nullable: true),
                    BoardMembers = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReviewDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Disposition = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Justification = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CustomerApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerApproved = table.Column<bool>(type: "boolean", nullable: false),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMRBs", x => x.Id);
                    table.CheckConstraint("ck_projectmrbs_disposition_range", "\"Disposition\" BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_projectmrbs_number_pos", "\"MrbNumber\" >= 1");
                    table.CheckConstraint("ck_projectmrbs_status_range", "\"Status\" BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_ProjectMRBs_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMRBs_ProjectNCRs_LinkedNcrId",
                        column: x => x.LinkedNcrId,
                        principalTable: "ProjectNCRs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectMRBs_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectacceptances_phase",
                table: "ProjectAcceptances",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectacceptances_project_status",
                table: "ProjectAcceptances",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectacceptances_project_number",
                table: "ProjectAcceptances",
                columns: new[] { "CustomerProjectId", "AcceptanceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectinspections_phase",
                table: "ProjectInspections",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_projectinspections_project_number",
                table: "ProjectInspections",
                columns: new[] { "CustomerProjectId", "InspectionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectmrbs_ncr",
                table: "ProjectMRBs",
                column: "LinkedNcrId",
                filter: "\"LinkedNcrId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectmrbs_phase",
                table: "ProjectMRBs",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectmrbs_project_status",
                table: "ProjectMRBs",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectmrbs_project_number",
                table: "ProjectMRBs",
                columns: new[] { "CustomerProjectId", "MrbNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectncrs_inspection",
                table: "ProjectNCRs",
                column: "LinkedInspectionId",
                filter: "\"LinkedInspectionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectncrs_phase",
                table: "ProjectNCRs",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectncrs_project_status",
                table: "ProjectNCRs",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectncrs_project_number",
                table: "ProjectNCRs",
                columns: new[] { "CustomerProjectId", "NcrNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectpunchitems_phase",
                table: "ProjectPunchItems",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectpunchitems_project_status",
                table: "ProjectPunchItems",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectpunchitems_project_number",
                table: "ProjectPunchItems",
                columns: new[] { "CustomerProjectId", "PunchNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAcceptances");

            migrationBuilder.DropTable(
                name: "ProjectMRBs");

            migrationBuilder.DropTable(
                name: "ProjectPunchItems");

            migrationBuilder.DropTable(
                name: "ProjectNCRs");

            migrationBuilder.DropTable(
                name: "ProjectInspections");
        }
    }
}
