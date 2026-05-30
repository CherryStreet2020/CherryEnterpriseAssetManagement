using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave5Pr14BillingSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectBillingSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectMilestoneId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BillingType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ScheduledAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PercentOfContract = table.Column<decimal>(type: "numeric", nullable: true),
                    RequiresAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptanceConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptanceConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptanceConfirmedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBillingSchedules", x => x.Id);
                    table.CheckConstraint("ck_projectbillingschedules_amount_nonneg", "\"ScheduledAmount\" >= 0");
                    table.CheckConstraint("ck_projectbillingschedules_status_range", "\"Status\" BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_projectbillingschedules_type_range", "\"BillingType\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ProjectBillingSchedules_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectBillingSchedules_ProjectMilestones_ProjectMilestoneId",
                        column: x => x.ProjectMilestoneId,
                        principalTable: "ProjectMilestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectInvoiceLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectBillingScheduleId = table.Column<int>(type: "integer", nullable: true),
                    ExternalInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvoicedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInvoiceLinks", x => x.Id);
                    table.CheckConstraint("ck_projectinvoicelinks_amount_nonneg", "\"InvoicedAmount\" >= 0");
                    table.CheckConstraint("ck_projectinvoicelinks_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectInvoiceLinks_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectInvoiceLinks_ProjectBillingSchedules_ProjectBillingS~",
                        column: x => x.ProjectBillingScheduleId,
                        principalTable: "ProjectBillingSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRevenueRecognitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectBillingScheduleId = table.Column<int>(type: "integer", nullable: true),
                    PeriodLabel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Method = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RecognizedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    RecognitionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PercentComplete = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRevenueRecognitions", x => x.Id);
                    table.CheckConstraint("ck_projectrevenuerecognitions_amount_nonneg", "\"RecognizedAmount\" >= 0");
                    table.CheckConstraint("ck_projectrevenuerecognitions_method_range", "\"Method\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectRevenueRecognitions_CustomerProjects_CustomerProject~",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectRevenueRecognitions_ProjectBillingSchedules_ProjectB~",
                        column: x => x.ProjectBillingScheduleId,
                        principalTable: "ProjectBillingSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectbillingschedules_milestone",
                table: "ProjectBillingSchedules",
                column: "ProjectMilestoneId",
                filter: "\"ProjectMilestoneId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectbillingschedules_project_status",
                table: "ProjectBillingSchedules",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_projectbillingschedules_project_code",
                table: "ProjectBillingSchedules",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectinvoicelinks_project_date",
                table: "ProjectInvoiceLinks",
                columns: new[] { "CustomerProjectId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "ix_projectinvoicelinks_schedule",
                table: "ProjectInvoiceLinks",
                column: "ProjectBillingScheduleId",
                filter: "\"ProjectBillingScheduleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectrevenuerecognitions_project_date",
                table: "ProjectRevenueRecognitions",
                columns: new[] { "CustomerProjectId", "RecognitionDate" });

            migrationBuilder.CreateIndex(
                name: "ix_projectrevenuerecognitions_schedule",
                table: "ProjectRevenueRecognitions",
                column: "ProjectBillingScheduleId",
                filter: "\"ProjectBillingScheduleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectInvoiceLinks");

            migrationBuilder.DropTable(
                name: "ProjectRevenueRecognitions");

            migrationBuilder.DropTable(
                name: "ProjectBillingSchedules");
        }
    }
}
