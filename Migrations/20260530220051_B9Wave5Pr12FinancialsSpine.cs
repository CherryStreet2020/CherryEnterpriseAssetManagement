using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave5Pr12FinancialsSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectActualCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    ProjectTaskId = table.Column<int>(type: "integer", nullable: true),
                    CostElementType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SourceType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    PostingReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    PostingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectActualCosts", x => x.Id);
                    table.CheckConstraint("ck_projectactualcosts_amount_nonneg", "\"Amount\" >= 0");
                    table.CheckConstraint("ck_projectactualcosts_source_range", "\"SourceType\" BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_ProjectActualCosts_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectActualCosts_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectActualCosts_ProjectTasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BudgetType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBudgets", x => x.Id);
                    table.CheckConstraint("ck_projectbudgets_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projectbudgets_type_range", "\"BudgetType\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectBudgets_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectBudgetLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectBudgetId = table.Column<int>(type: "integer", nullable: false),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    CostElementType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBudgetLines", x => x.Id);
                    table.CheckConstraint("ck_projectbudgetlines_amounts_nonneg", "\"BudgetAmount\" >= 0 AND (\"Quantity\" IS NULL OR \"Quantity\" >= 0) AND (\"UnitCost\" IS NULL OR \"UnitCost\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ProjectBudgetLines_ProjectBudgets_ProjectBudgetId",
                        column: x => x.ProjectBudgetId,
                        principalTable: "ProjectBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectBudgetLines_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEacSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectBudgetId = table.Column<int>(type: "integer", nullable: true),
                    SnapshotDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContractValue = table.Column<decimal>(type: "numeric", nullable: false),
                    BudgetTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualCostToDate = table.Column<decimal>(type: "numeric", nullable: false),
                    CommittedCost = table.Column<decimal>(type: "numeric", nullable: false),
                    EstimateToComplete = table.Column<decimal>(type: "numeric", nullable: false),
                    EstimateAtCompletion = table.Column<decimal>(type: "numeric", nullable: false),
                    PercentComplete = table.Column<decimal>(type: "numeric", nullable: true),
                    ProjectedMargin = table.Column<decimal>(type: "numeric", nullable: false),
                    ProjectedMarginPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    FrozenBreakdownJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEacSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectEacSnapshots_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectEacSnapshots_ProjectBudgets_ProjectBudgetId",
                        column: x => x.ProjectBudgetId,
                        principalTable: "ProjectBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectForecasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectBudgetId = table.Column<int>(type: "integer", nullable: true),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    CostElementType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Method = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EstimateToComplete = table.Column<decimal>(type: "numeric", nullable: true),
                    EstimateAtCompletion = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    ForecastDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectForecasts", x => x.Id);
                    table.CheckConstraint("ck_projectforecasts_amounts_nonneg", "(\"EstimateToComplete\" IS NULL OR \"EstimateToComplete\" >= 0) AND (\"EstimateAtCompletion\" IS NULL OR \"EstimateAtCompletion\" >= 0)");
                    table.CheckConstraint("ck_projectforecasts_method_range", "\"Method\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectForecasts_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectForecasts_ProjectBudgets_ProjectBudgetId",
                        column: x => x.ProjectBudgetId,
                        principalTable: "ProjectBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectForecasts_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectactualcosts_project_date",
                table: "ProjectActualCosts",
                columns: new[] { "CustomerProjectId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "ix_projectactualcosts_project_element",
                table: "ProjectActualCosts",
                columns: new[] { "CustomerProjectId", "CostElementType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActualCosts_ProjectPhaseId",
                table: "ProjectActualCosts",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectActualCosts_ProjectTaskId",
                table: "ProjectActualCosts",
                column: "ProjectTaskId");

            migrationBuilder.CreateIndex(
                name: "ix_projectbudgetlines_phase",
                table: "ProjectBudgetLines",
                column: "ProjectPhaseId",
                filter: "\"ProjectPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_projectbudgetlines_budget_lineno",
                table: "ProjectBudgetLines",
                columns: new[] { "ProjectBudgetId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectbudgets_project_status",
                table: "ProjectBudgets",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectbudgets_project_code",
                table: "ProjectBudgets",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projecteacsnapshots_project_date",
                table: "ProjectEacSnapshots",
                columns: new[] { "CustomerProjectId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEacSnapshots_ProjectBudgetId",
                table: "ProjectEacSnapshots",
                column: "ProjectBudgetId");

            migrationBuilder.CreateIndex(
                name: "ix_projectforecasts_budget",
                table: "ProjectForecasts",
                column: "ProjectBudgetId",
                filter: "\"ProjectBudgetId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectforecasts_project_date",
                table: "ProjectForecasts",
                columns: new[] { "CustomerProjectId", "ForecastDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectForecasts_ProjectPhaseId",
                table: "ProjectForecasts",
                column: "ProjectPhaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectActualCosts");

            migrationBuilder.DropTable(
                name: "ProjectBudgetLines");

            migrationBuilder.DropTable(
                name: "ProjectEacSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectForecasts");

            migrationBuilder.DropTable(
                name: "ProjectBudgets");
        }
    }
}
