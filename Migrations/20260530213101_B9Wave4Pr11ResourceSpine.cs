using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave4Pr11ResourceSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    ProjectTaskId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    ExpenseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                    IsReimbursable = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReceiptReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectExpenses", x => x.Id);
                    table.CheckConstraint("ck_projectexpenses_amount_nonneg", "\"Amount\" >= 0");
                    table.CheckConstraint("ck_projectexpenses_category_range", "\"Category\" BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_projectexpenses_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_ProjectTasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectResourcePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    ProjectTaskId = table.Column<int>(type: "integer", nullable: true),
                    WorkCenterId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResourceType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RoleOrSkill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PlannedHours = table.Column<decimal>(type: "numeric", nullable: true),
                    PlannedRate = table.Column<decimal>(type: "numeric", nullable: true),
                    PlannedCost = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectResourcePlans", x => x.Id);
                    table.CheckConstraint("ck_projectresourceplans_amounts_nonneg", "(\"PlannedHours\" IS NULL OR \"PlannedHours\" >= 0) AND (\"PlannedRate\" IS NULL OR \"PlannedRate\" >= 0) AND (\"PlannedCost\" IS NULL OR \"PlannedCost\" >= 0)");
                    table.CheckConstraint("ck_projectresourceplans_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectresourceplans_type_range", "\"ResourceType\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ProjectResourcePlans_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectResourcePlans_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectResourcePlans_ProjectTasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectResourcePlans_WorkCenters_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "WorkCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectResourceAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectResourcePlanId = table.Column<int>(type: "integer", nullable: true),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    ProjectTaskId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    WorkCenterId = table.Column<int>(type: "integer", nullable: true),
                    AssetId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AllocationPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    PlannedHours = table.Column<decimal>(type: "numeric", nullable: true),
                    CostRate = table.Column<decimal>(type: "numeric", nullable: true),
                    BillRate = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectResourceAssignments", x => x.Id);
                    table.CheckConstraint("ck_projectresourceassignments_allocation_range", "\"AllocationPercent\" IS NULL OR (\"AllocationPercent\" >= 0 AND \"AllocationPercent\" <= 100)");
                    table.CheckConstraint("ck_projectresourceassignments_amounts_nonneg", "(\"PlannedHours\" IS NULL OR \"PlannedHours\" >= 0) AND (\"CostRate\" IS NULL OR \"CostRate\" >= 0) AND (\"BillRate\" IS NULL OR \"BillRate\" >= 0)");
                    table.CheckConstraint("ck_projectresourceassignments_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projectresourceassignments_type_range", "\"ResourceType\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ProjectResourceAssignments_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectResourceAssignments_CustomerProjects_CustomerProject~",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectResourceAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectResourceAssignments_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectResourceAssignments_ProjectResourcePlans_ProjectReso~",
                        column: x => x.ProjectResourcePlanId,
                        principalTable: "ProjectResourcePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectResourceAssignments_ProjectTasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectResourceAssignments_WorkCenters_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "WorkCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTimeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectResourceAssignmentId = table.Column<int>(type: "integer", nullable: true),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    ProjectTaskId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    WorkDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                    CostRate = table.Column<decimal>(type: "numeric", nullable: true),
                    BillRate = table.Column<decimal>(type: "numeric", nullable: true),
                    ComputedCost = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTimeEntries", x => x.Id);
                    table.CheckConstraint("ck_projecttimeentries_amounts_nonneg", "\"Hours\" >= 0 AND (\"CostRate\" IS NULL OR \"CostRate\" >= 0) AND (\"BillRate\" IS NULL OR \"BillRate\" >= 0) AND (\"ComputedCost\" IS NULL OR \"ComputedCost\" >= 0)");
                    table.CheckConstraint("ck_projecttimeentries_category_range", "\"Category\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projecttimeentries_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectTimeEntries_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTimeEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectTimeEntries_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectTimeEntries_ProjectResourceAssignments_ProjectResour~",
                        column: x => x.ProjectResourceAssignmentId,
                        principalTable: "ProjectResourceAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectTimeEntries_ProjectTasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_EmployeeId",
                table: "ProjectExpenses",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "ix_projectexpenses_project_status",
                table: "ProjectExpenses",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_ProjectPhaseId",
                table: "ProjectExpenses",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_ProjectTaskId",
                table: "ProjectExpenses",
                column: "ProjectTaskId");

            migrationBuilder.CreateIndex(
                name: "ux_projectexpenses_project_code",
                table: "ProjectExpenses",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResourceAssignments_AssetId",
                table: "ProjectResourceAssignments",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResourceAssignments_EmployeeId",
                table: "ProjectResourceAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "ix_projectresourceassignments_plan",
                table: "ProjectResourceAssignments",
                column: "ProjectResourcePlanId",
                filter: "\"ProjectResourcePlanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectresourceassignments_project_status",
                table: "ProjectResourceAssignments",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResourceAssignments_ProjectPhaseId",
                table: "ProjectResourceAssignments",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResourceAssignments_ProjectTaskId",
                table: "ProjectResourceAssignments",
                column: "ProjectTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResourceAssignments_WorkCenterId",
                table: "ProjectResourceAssignments",
                column: "WorkCenterId");

            migrationBuilder.CreateIndex(
                name: "ux_projectresourceassignments_project_code",
                table: "ProjectResourceAssignments",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResourcePlans_ProjectPhaseId",
                table: "ProjectResourcePlans",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResourcePlans_ProjectTaskId",
                table: "ProjectResourcePlans",
                column: "ProjectTaskId");

            migrationBuilder.CreateIndex(
                name: "ix_projectresourceplans_status",
                table: "ProjectResourcePlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResourcePlans_WorkCenterId",
                table: "ProjectResourcePlans",
                column: "WorkCenterId");

            migrationBuilder.CreateIndex(
                name: "ux_projectresourceplans_project_code",
                table: "ProjectResourcePlans",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projecttimeentries_assignment",
                table: "ProjectTimeEntries",
                column: "ProjectResourceAssignmentId",
                filter: "\"ProjectResourceAssignmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimeEntries_EmployeeId",
                table: "ProjectTimeEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "ix_projecttimeentries_project_date",
                table: "ProjectTimeEntries",
                columns: new[] { "CustomerProjectId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimeEntries_ProjectPhaseId",
                table: "ProjectTimeEntries",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTimeEntries_ProjectTaskId",
                table: "ProjectTimeEntries",
                column: "ProjectTaskId");

            migrationBuilder.CreateIndex(
                name: "ix_projecttimeentries_status",
                table: "ProjectTimeEntries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectExpenses");

            migrationBuilder.DropTable(
                name: "ProjectTimeEntries");

            migrationBuilder.DropTable(
                name: "ProjectResourceAssignments");

            migrationBuilder.DropTable(
                name: "ProjectResourcePlans");
        }
    }
}
