using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9Wave4Pr10ProcurementSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerProjectId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectPhaseId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectProcurementPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PlannedQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PlannedAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    NeedByDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsLongLead = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectProcurementPlans", x => x.Id);
                    table.CheckConstraint("ck_projectprocplans_amounts_nonneg", "(\"PlannedAmount\" IS NULL OR \"PlannedAmount\" >= 0) AND (\"PlannedQuantity\" IS NULL OR \"PlannedQuantity\" >= 0)");
                    table.CheckConstraint("ck_projectprocplans_category_range", "\"Category\" BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_projectprocplans_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectProcurementPlans_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectProcurementPlans_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectProcurementPlans_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCommitments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectProcurementPlanId = table.Column<int>(type: "integer", nullable: true),
                    ProjectPhaseId = table.Column<int>(type: "integer", nullable: true),
                    PurchaseOrderId = table.Column<int>(type: "integer", nullable: true),
                    VendorId = table.Column<int>(type: "integer", nullable: true),
                    CommitmentType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CommittedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    CommittedQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CommittedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCommitments", x => x.Id);
                    table.CheckConstraint("ck_projectcommitments_amount_nonneg", "\"CommittedAmount\" >= 0 AND (\"CommittedQuantity\" IS NULL OR \"CommittedQuantity\" >= 0)");
                    table.CheckConstraint("ck_projectcommitments_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.CheckConstraint("ck_projectcommitments_type_range", "\"CommitmentType\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ProjectCommitments_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCommitments_ProjectPhases_ProjectPhaseId",
                        column: x => x.ProjectPhaseId,
                        principalTable: "ProjectPhases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectCommitments_ProjectProcurementPlans_ProjectProcureme~",
                        column: x => x.ProjectProcurementPlanId,
                        principalTable: "ProjectProcurementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectCommitments_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectCommitments_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectCommitmentId = table.Column<int>(type: "integer", nullable: false),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    GoodsReceiptId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReceivedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReceipts", x => x.Id);
                    table.CheckConstraint("ck_projectreceipts_amount_nonneg", "\"ReceivedAmount\" >= 0 AND (\"ReceivedQuantity\" IS NULL OR \"ReceivedQuantity\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ProjectReceipts_ProjectCommitments_ProjectCommitmentId",
                        column: x => x.ProjectCommitmentId,
                        principalTable: "ProjectCommitments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchaseorders_customerproject",
                table: "PurchaseOrders",
                column: "CustomerProjectId",
                filter: "\"CustomerProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_purchaseorders_projectphase",
                table: "PurchaseOrders",
                column: "ProjectPhaseId",
                filter: "\"ProjectPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectcommitments_plan",
                table: "ProjectCommitments",
                column: "ProjectProcurementPlanId",
                filter: "\"ProjectProcurementPlanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectcommitments_po",
                table: "ProjectCommitments",
                column: "PurchaseOrderId",
                filter: "\"PurchaseOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectcommitments_project_status",
                table: "ProjectCommitments",
                columns: new[] { "CustomerProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCommitments_ProjectPhaseId",
                table: "ProjectCommitments",
                column: "ProjectPhaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCommitments_VendorId",
                table: "ProjectCommitments",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "ux_projectcommitments_project_code",
                table: "ProjectCommitments",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectprocplans_phase",
                table: "ProjectProcurementPlans",
                column: "ProjectPhaseId",
                filter: "\"ProjectPhaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectprocplans_status",
                table: "ProjectProcurementPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProcurementPlans_ItemId",
                table: "ProjectProcurementPlans",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "ux_projectprocplans_project_code",
                table: "ProjectProcurementPlans",
                columns: new[] { "CustomerProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectreceipts_commitment",
                table: "ProjectReceipts",
                column: "ProjectCommitmentId");

            migrationBuilder.CreateIndex(
                name: "ix_projectreceipts_project",
                table: "ProjectReceipts",
                column: "CustomerProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_CustomerProjects_CustomerProjectId",
                table: "PurchaseOrders",
                column: "CustomerProjectId",
                principalTable: "CustomerProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_ProjectPhases_ProjectPhaseId",
                table: "PurchaseOrders",
                column: "ProjectPhaseId",
                principalTable: "ProjectPhases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_CustomerProjects_CustomerProjectId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_ProjectPhases_ProjectPhaseId",
                table: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "ProjectReceipts");

            migrationBuilder.DropTable(
                name: "ProjectCommitments");

            migrationBuilder.DropTable(
                name: "ProjectProcurementPlans");

            migrationBuilder.DropIndex(
                name: "ix_purchaseorders_customerproject",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "ix_purchaseorders_projectphase",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CustomerProjectId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ProjectPhaseId",
                table: "PurchaseOrders");
        }
    }
}
