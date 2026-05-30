using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave3Pr8ScheduleSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MilestoneType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    BaselineDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForecastDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    WeightPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    CustomerVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsBillingMilestone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    BillingAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AchievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AchievedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMilestones", x => x.Id);
                    table.CheckConstraint("ck_projectmilestones_billingamount_nonneg", "\"BillingAmount\" IS NULL OR \"BillingAmount\" >= 0");
                    table.CheckConstraint("ck_projectmilestones_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projectmilestones_type_range", "\"MilestoneType\" BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_projectmilestones_weightpercent_range", "\"WeightPercent\" IS NULL OR (\"WeightPercent\" >= 0 AND \"WeightPercent\" <= 100)");
                    table.ForeignKey(
                        name: "FK_ProjectMilestones_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMilestones_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    ParentTaskId = table.Column<int>(type: "integer", nullable: true),
                    ProjectMilestoneId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TaskType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsMilestoneBlocking = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsCriticalPath = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CustomerVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ResponsibleOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResponsibleDepartment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    BlockReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PercentComplete = table.Column<decimal>(type: "numeric", nullable: true),
                    PlannedStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlannedFinish = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForecastStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForecastFinish = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualFinish = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConstraintType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ConstraintDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkHoursPlanned = table.Column<decimal>(type: "numeric", nullable: true),
                    WorkHoursActual = table.Column<decimal>(type: "numeric", nullable: true),
                    CostPlanned = table.Column<decimal>(type: "numeric", nullable: true),
                    CostActual = table.Column<decimal>(type: "numeric", nullable: true),
                    WeightPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTasks", x => x.Id);
                    table.CheckConstraint("ck_projecttasks_constrainttype_range", "\"ConstraintType\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projecttasks_costs_nonneg", "(\"CostPlanned\" IS NULL OR \"CostPlanned\" >= 0) AND (\"CostActual\" IS NULL OR \"CostActual\" >= 0)");
                    table.CheckConstraint("ck_projecttasks_hours_nonneg", "(\"WorkHoursPlanned\" IS NULL OR \"WorkHoursPlanned\" >= 0) AND (\"WorkHoursActual\" IS NULL OR \"WorkHoursActual\" >= 0)");
                    table.CheckConstraint("ck_projecttasks_percentcomplete_range", "\"PercentComplete\" IS NULL OR (\"PercentComplete\" >= 0 AND \"PercentComplete\" <= 100)");
                    table.CheckConstraint("ck_projecttasks_priority_range", "\"Priority\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projecttasks_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projecttasks_type_range", "\"TaskType\" BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_projecttasks_weightpercent_range", "\"WeightPercent\" IS NULL OR (\"WeightPercent\" >= 0 AND \"WeightPercent\" <= 100)");
                    table.ForeignKey(
                        name: "FK_ProjectTasks_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectTasks_ProjectMilestones_ProjectMilestoneId",
                        column: x => x.ProjectMilestoneId,
                        principalTable: "ProjectMilestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectTasks_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectTasks_ProjectTasks_ParentTaskId",
                        column: x => x.ParentTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTaskDependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PredecessorTaskId = table.Column<int>(type: "integer", nullable: false),
                    SuccessorTaskId = table.Column<int>(type: "integer", nullable: false),
                    DependencyType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LagDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTaskDependencies", x => x.Id);
                    table.CheckConstraint("ck_projecttaskdeps_no_self", "\"PredecessorTaskId\" <> \"SuccessorTaskId\"");
                    table.CheckConstraint("ck_projecttaskdeps_type_range", "\"DependencyType\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectTaskDependencies_ProjectTasks_PredecessorTaskId",
                        column: x => x.PredecessorTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectTaskDependencies_ProjectTasks_SuccessorTaskId",
                        column: x => x.SuccessorTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectmilestones_phase",
                table: "ProjectMilestones",
                column: "ProjectPhaseId",
                filter: "\"ProjectPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectmilestones_status",
                table: "ProjectMilestones",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_projectmilestones_project_code",
                table: "ProjectMilestones",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projecttaskdeps_successor",
                table: "ProjectTaskDependencies",
                column: "SuccessorTaskId");

            migrationBuilder.CreateIndex(
                name: "ux_projecttaskdeps_pred_succ",
                table: "ProjectTaskDependencies",
                columns: new[] { "PredecessorTaskId", "SuccessorTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projecttasks_milestone",
                table: "ProjectTasks",
                column: "ProjectMilestoneId",
                filter: "\"ProjectMilestoneId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projecttasks_parent",
                table: "ProjectTasks",
                column: "ParentTaskId",
                filter: "\"ParentTaskId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projecttasks_phase",
                table: "ProjectTasks",
                column: "ProjectPhaseId",
                filter: "\"ProjectPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projecttasks_status",
                table: "ProjectTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_projecttasks_project_code",
                table: "ProjectTasks",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectTaskDependencies");

            migrationBuilder.DropTable(
                name: "ProjectTasks");

            migrationBuilder.DropTable(
                name: "ProjectMilestones");
        }
    }
}
