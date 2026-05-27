using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddCostRollupEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostRollupRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    RunNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RootCostObjectType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RootCostObjectId = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    GraphNodeCount = table.Column<int>(type: "integer", nullable: false),
                    GraphEdgeCount = table.Column<int>(type: "integer", nullable: false),
                    GraphMaxDepth = table.Column<int>(type: "integer", nullable: false),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    ExceptionCount = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalAdditiveCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTransferCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalDrilldownCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalExplodedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaterialTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LaborTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OverheadTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SubcontractTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtherTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExecutedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostRollupRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostRollupExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CostRollupRunId = table.Column<int>(type: "integer", nullable: false),
                    ExceptionType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Severity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CostObjectType = table.Column<int>(type: "integer", nullable: true),
                    CostObjectId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    OperationId = table.Column<int>(type: "integer", nullable: true),
                    BomLineId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    CostTransactionId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EstimatedImpact = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BlocksClose = table.Column<bool>(type: "boolean", nullable: false),
                    Acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostRollupExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostRollupExceptions_CostRollupRuns_CostRollupRunId",
                        column: x => x.CostRollupRunId,
                        principalTable: "CostRollupRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostRollupLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CostRollupRunId = table.Column<int>(type: "integer", nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CostObjectType = table.Column<int>(type: "integer", nullable: false),
                    CostObjectId = table.Column<int>(type: "integer", nullable: false),
                    ParentCostObjectType = table.Column<int>(type: "integer", nullable: true),
                    ParentCostObjectId = table.Column<int>(type: "integer", nullable: true),
                    CostTransactionId = table.Column<int>(type: "integer", nullable: true),
                    CostTransferId = table.Column<int>(type: "integer", nullable: true),
                    Classification = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CostBucket = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TransactionType = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExtendedCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaterialCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LaborCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OverheadCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SubcontractCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OtherCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    IsRollupAdditive = table.Column<bool>(type: "boolean", nullable: false),
                    IsProvisional = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostRollupLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostRollupLines_CostRollupRuns_CostRollupRunId",
                        column: x => x.CostRollupRunId,
                        principalTable: "CostRollupRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupExc_PRO",
                table: "CostRollupExceptions",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupExc_RunId",
                table: "CostRollupExceptions",
                column: "CostRollupRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupExc_Severity",
                table: "CostRollupExceptions",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupExc_Type",
                table: "CostRollupExceptions",
                column: "ExceptionType");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupLine_Classification",
                table: "CostRollupLines",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupLine_CostObject",
                table: "CostRollupLines",
                columns: new[] { "CostObjectType", "CostObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupLine_RunId",
                table: "CostRollupLines",
                column: "CostRollupRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollup_Mode",
                table: "CostRollupRuns",
                column: "Mode");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollup_PRO",
                table: "CostRollupRuns",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollup_Started",
                table: "CostRollupRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollup_Status",
                table: "CostRollupRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupRuns_CompanyId",
                table: "CostRollupRuns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CostRollupRuns_TenantId",
                table: "CostRollupRuns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_CostRollup_Company_Number",
                table: "CostRollupRuns",
                columns: new[] { "CompanyId", "RunNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostRollupExceptions");

            migrationBuilder.DropTable(
                name: "CostRollupLines");

            migrationBuilder.DropTable(
                name: "CostRollupRuns");
        }
    }
}
