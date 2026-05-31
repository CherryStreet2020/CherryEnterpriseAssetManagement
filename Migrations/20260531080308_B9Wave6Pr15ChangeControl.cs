using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave6Pr15ChangeControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceChangeRequestId",
                table: "ProjectAmendments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ChangeRequestNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Category = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    RequestedByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RequestDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CostImpact = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RevenueImpact = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MarginImpactPct = table.Column<decimal>(type: "numeric", nullable: true),
                    ScheduleImpactDays = table.Column<int>(type: "integer", nullable: true),
                    RiskImpact = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    ImpactNarrative = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    AffectedPhaseId = table.Column<int>(type: "integer", nullable: true),
                    RequiresInternalApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresCustomerApproval = table.Column<bool>(type: "boolean", nullable: false),
                    InternalApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InternalApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SubmittedToCustomerAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CustomerReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerPoRevision = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    BillingTreatment = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CostTreatment = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    ResultingProjectAmendmentId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectChangeRequests", x => x.Id);
                    table.CheckConstraint("ck_projectchangerequests_billingtreatment_range", "\"BillingTreatment\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projectchangerequests_category_range", "\"Category\" BETWEEN 0 AND 13");
                    table.CheckConstraint("ck_projectchangerequests_costtreatment_range", "\"CostTreatment\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectchangerequests_number_pos", "\"ChangeRequestNumber\" >= 1");
                    table.CheckConstraint("ck_projectchangerequests_risk_range", "\"RiskImpact\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectchangerequests_source_range", "\"Source\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projectchangerequests_status_range", "\"Status\" BETWEEN 0 AND 8");
                    table.ForeignKey(
                        name: "FK_ProjectChangeRequests_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectChangeRequests_ProjectAmendments_ResultingProjectAme~",
                        column: x => x.ResultingProjectAmendmentId,
                        principalTable: "ProjectAmendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectChangeRequests_ProjectPhases_AffectedPhaseId",
                        column: x => x.AffectedPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectamendments_sourcechangerequest",
                table: "ProjectAmendments",
                column: "SourceChangeRequestId",
                filter: "\"SourceChangeRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectchangerequests_phase",
                table: "ProjectChangeRequests",
                column: "AffectedPhaseId",
                filter: "\"AffectedPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectchangerequests_project_status",
                table: "ProjectChangeRequests",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_projectchangerequests_resultingamendment",
                table: "ProjectChangeRequests",
                column: "ResultingProjectAmendmentId",
                filter: "\"ResultingProjectAmendmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_projectchangerequests_project_number",
                table: "ProjectChangeRequests",
                columns: new[] { "CustomerProjectId", "ChangeRequestNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectAmendments_ProjectChangeRequests_SourceChangeRequest~",
                table: "ProjectAmendments",
                column: "SourceChangeRequestId",
                principalTable: "ProjectChangeRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectAmendments_ProjectChangeRequests_SourceChangeRequest~",
                table: "ProjectAmendments");

            migrationBuilder.DropTable(
                name: "ProjectChangeRequests");

            migrationBuilder.DropIndex(
                name: "ix_projectamendments_sourcechangerequest",
                table: "ProjectAmendments");

            migrationBuilder.DropColumn(
                name: "SourceChangeRequestId",
                table: "ProjectAmendments");
        }
    }
}
