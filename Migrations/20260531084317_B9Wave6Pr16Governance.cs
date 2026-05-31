using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave6Pr16Governance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    DecisionNumber = table.Column<int>(type: "integer", nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DecisionMaker = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AlternativesConsidered = table.Column<string>(type: "text", nullable: true),
                    Impact = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    LinkedChangeRequestId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDecisions", x => x.Id);
                    table.CheckConstraint("ck_projectdecisions_number_pos", "\"DecisionNumber\" >= 1");
                    table.CheckConstraint("ck_projectdecisions_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectDecisions_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDecisions_ProjectChangeRequests_LinkedChangeRequestId",
                        column: x => x.LinkedChangeRequestId,
                        principalTable: "ProjectChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectDecisions_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    IssueNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Priority = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OpenDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    RootCause = table.Column<string>(type: "text", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "text", nullable: true),
                    CustomerImpact = table.Column<bool>(type: "boolean", nullable: false),
                    CostImpact = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ScheduleImpactDays = table.Column<int>(type: "integer", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    LinkedChangeRequestId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_ProjectIssues", x => x.Id);
                    table.CheckConstraint("ck_projectissues_number_pos", "\"IssueNumber\" >= 1");
                    table.CheckConstraint("ck_projectissues_priority_range", "\"Priority\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectissues_severity_range", "\"Severity\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectissues_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ProjectIssues_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_ProjectChangeRequests_LinkedChangeRequestId",
                        column: x => x.LinkedChangeRequestId,
                        principalTable: "ProjectChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMeetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    MeetingNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MeetingType = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    MeetingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Attendees = table.Column<string>(type: "text", nullable: true),
                    Agenda = table.Column<string>(type: "text", nullable: true),
                    Minutes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMeetings", x => x.Id);
                    table.CheckConstraint("ck_projectmeetings_number_pos", "\"MeetingNumber\" >= 1");
                    table.CheckConstraint("ck_projectmeetings_status_range", "\"Status\" BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_projectmeetings_type_range", "\"MeetingType\" BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_ProjectMeetings_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRisks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    RiskNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Probability = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Impact = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Exposure = table.Column<int>(type: "integer", nullable: false),
                    Owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MitigationPlan = table.Column<string>(type: "text", nullable: true),
                    ContingencyPlan = table.Column<string>(type: "text", nullable: true),
                    Trigger = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CostExposure = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ScheduleExposureDays = table.Column<int>(type: "integer", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    CustomerImpact = table.Column<bool>(type: "boolean", nullable: false),
                    SupplierImpact = table.Column<bool>(type: "boolean", nullable: false),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    LinkedChangeRequestId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_ProjectRisks", x => x.Id);
                    table.CheckConstraint("ck_projectrisks_category_range", "\"Category\" BETWEEN 0 AND 7");
                    table.CheckConstraint("ck_projectrisks_impact_range", "\"Impact\" BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_projectrisks_number_pos", "\"RiskNumber\" >= 1");
                    table.CheckConstraint("ck_projectrisks_probability_range", "\"Probability\" BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_projectrisks_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ProjectRisks_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectRisks_ProjectChangeRequests_LinkedChangeRequestId",
                        column: x => x.LinkedChangeRequestId,
                        principalTable: "ProjectChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectRisks_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectActionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ActionNumber = table.Column<int>(type: "integer", nullable: false),
                    ProjectMeetingId = table.Column<int>(type: "integer", nullable: true),
                    Owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_ProjectActionItems", x => x.Id);
                    table.CheckConstraint("ck_projectactionitems_number_pos", "\"ActionNumber\" >= 1");
                    table.CheckConstraint("ck_projectactionitems_priority_range", "\"Priority\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectactionitems_source_range", "\"Source\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projectactionitems_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectActionItems_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectActionItems_ProjectMeetings_ProjectMeetingId",
                        column: x => x.ProjectMeetingId,
                        principalTable: "ProjectMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectactionitems_meeting",
                table: "ProjectActionItems",
                column: "ProjectMeetingId",
                filter: "\"ProjectMeetingId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectactionitems_project_status",
                table: "ProjectActionItems",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectactionitems_project_number",
                table: "ProjectActionItems",
                columns: new[] { "CustomerProjectId", "ActionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectdecisions_changerequest",
                table: "ProjectDecisions",
                column: "LinkedChangeRequestId",
                filter: "\"LinkedChangeRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectdecisions_phase",
                table: "ProjectDecisions",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectdecisions_project_status",
                table: "ProjectDecisions",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectdecisions_project_number",
                table: "ProjectDecisions",
                columns: new[] { "CustomerProjectId", "DecisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectissues_changerequest",
                table: "ProjectIssues",
                column: "LinkedChangeRequestId",
                filter: "\"LinkedChangeRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectissues_phase",
                table: "ProjectIssues",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectissues_project_status",
                table: "ProjectIssues",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectissues_project_number",
                table: "ProjectIssues",
                columns: new[] { "CustomerProjectId", "IssueNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectmeetings_project_date",
                table: "ProjectMeetings",
                columns: new[] { "CustomerProjectId", "MeetingDate" });

            migrationBuilder.CreateIndex(
                name: "ux_projectmeetings_project_number",
                table: "ProjectMeetings",
                columns: new[] { "CustomerProjectId", "MeetingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectrisks_changerequest",
                table: "ProjectRisks",
                column: "LinkedChangeRequestId",
                filter: "\"LinkedChangeRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectrisks_phase",
                table: "ProjectRisks",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectrisks_project_status",
                table: "ProjectRisks",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectrisks_project_number",
                table: "ProjectRisks",
                columns: new[] { "CustomerProjectId", "RiskNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectActionItems");

            migrationBuilder.DropTable(
                name: "ProjectDecisions");

            migrationBuilder.DropTable(
                name: "ProjectIssues");

            migrationBuilder.DropTable(
                name: "ProjectRisks");

            migrationBuilder.DropTable(
                name: "ProjectMeetings");
        }
    }
}
