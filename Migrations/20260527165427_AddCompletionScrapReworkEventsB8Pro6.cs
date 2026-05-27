using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletionScrapReworkEventsB8Pro6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionCompletionEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CompletionNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<int>(type: "integer", nullable: false),
                    TransactionId = table.Column<int>(type: "integer", nullable: true),
                    WipMoveId = table.Column<int>(type: "integer", nullable: true),
                    GoodQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ScrapQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ReworkQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    RejectQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    CompleteRemaining = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinalOperation = table.Column<bool>(type: "boolean", nullable: false),
                    MoveQuantityToNextOp = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    ResourceWorkCenterId = table.Column<int>(type: "integer", nullable: true),
                    BackflushMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    AutoIssuePullMaterials = table.Column<bool>(type: "boolean", nullable: false),
                    InspectionRequired = table.Column<bool>(type: "boolean", nullable: false),
                    LotNumbers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SerialNumbers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LaborCostPosted = table.Column<decimal>(type: "numeric", nullable: true),
                    MaterialCostPosted = table.Column<decimal>(type: "numeric", nullable: true),
                    OverheadCostPosted = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionCompletionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionCompletionEvents_ProductionOperationTransactions_~",
                        column: x => x.TransactionId,
                        principalTable: "ProductionOperationTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionCompletionEvents_ProductionOperations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionCompletionEvents_ProductionOrders_ProductionOrder~",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionCompletionEvents_ProductionWipMoves_WipMoveId",
                        column: x => x.WipMoveId,
                        principalTable: "ProductionWipMoves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductionReworkEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ReworkNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    SourceOperationId = table.Column<int>(type: "integer", nullable: false),
                    ReworkOperationId = table.Column<int>(type: "integer", nullable: true),
                    WipMoveId = table.Column<int>(type: "integer", nullable: true),
                    ReworkQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    RoutingType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReworkInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReworkReasonCodeId = table.Column<int>(type: "integer", nullable: true),
                    ReworkMaterialRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveDefectiveComponent = table.Column<bool>(type: "boolean", nullable: false),
                    AdditionalLaborPlannedMins = table.Column<decimal>(type: "numeric", nullable: false),
                    AssignedWorkCenterId = table.Column<int>(type: "integer", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QualityHold = table.Column<bool>(type: "boolean", nullable: false),
                    ReinspectRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ScrapAfterFailedReworkAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnToOriginalFlow = table.Column<bool>(type: "boolean", nullable: false),
                    CostTreatment = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EstimatedReworkCost = table.Column<decimal>(type: "numeric", nullable: true),
                    NcrId = table.Column<int>(type: "integer", nullable: true),
                    CarId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReworkDecisionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionReworkEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionReworkEvents_ProductionOperations_ReworkOperation~",
                        column: x => x.ReworkOperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionReworkEvents_ProductionOperations_SourceOperation~",
                        column: x => x.SourceOperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionReworkEvents_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionReworkEvents_ProductionWipMoves_WipMoveId",
                        column: x => x.WipMoveId,
                        principalTable: "ProductionWipMoves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductionScrapEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ScrapNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    DetectedAtOperationId = table.Column<int>(type: "integer", nullable: false),
                    CausedAtOperationId = table.Column<int>(type: "integer", nullable: true),
                    ScrapQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ScrapUom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ScrapReasonCodeId = table.Column<int>(type: "integer", nullable: true),
                    DefectCodeId = table.Column<int>(type: "integer", nullable: true),
                    CauseCodeId = table.Column<int>(type: "integer", nullable: true),
                    ResponsibleArea = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Disposition = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsComponentScrap = table.Column<bool>(type: "boolean", nullable: false),
                    IsOperationScrap = table.Column<bool>(type: "boolean", nullable: false),
                    ReplacementRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CostTreatment = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ScrapCost = table.Column<decimal>(type: "numeric", nullable: true),
                    NcrRequired = table.Column<bool>(type: "boolean", nullable: false),
                    NcrId = table.Column<int>(type: "integer", nullable: true),
                    SupervisorApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SupervisorApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LotNumbers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SerialNumbers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ScrapRecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionScrapEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionScrapEvents_ProductionOperations_CausedAtOperatio~",
                        column: x => x.CausedAtOperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionScrapEvents_ProductionOperations_DetectedAtOperat~",
                        column: x => x.DetectedAtOperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionScrapEvents_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CmpEvt_Date",
                table: "ProductionCompletionEvents",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CmpEvt_Op",
                table: "ProductionCompletionEvents",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_CmpEvt_PRO",
                table: "ProductionCompletionEvents",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCompletionEvents_CompanyId",
                table: "ProductionCompletionEvents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCompletionEvents_TenantId",
                table: "ProductionCompletionEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCompletionEvents_TransactionId",
                table: "ProductionCompletionEvents",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCompletionEvents_WipMoveId",
                table: "ProductionCompletionEvents",
                column: "WipMoveId");

            migrationBuilder.CreateIndex(
                name: "UX_CmpEvt_Company_Number",
                table: "ProductionCompletionEvents",
                columns: new[] { "CompanyId", "CompletionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworkEvents_CompanyId",
                table: "ProductionReworkEvents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworkEvents_ReworkOperationId",
                table: "ProductionReworkEvents",
                column: "ReworkOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworkEvents_TenantId",
                table: "ProductionReworkEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworkEvents_WipMoveId",
                table: "ProductionReworkEvents",
                column: "WipMoveId");

            migrationBuilder.CreateIndex(
                name: "IX_RwkEvt_Date",
                table: "ProductionReworkEvents",
                column: "ReworkDecisionAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RwkEvt_PRO",
                table: "ProductionReworkEvents",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RwkEvt_SrcOp",
                table: "ProductionReworkEvents",
                column: "SourceOperationId");

            migrationBuilder.CreateIndex(
                name: "UX_RwkEvt_Company_Number",
                table: "ProductionReworkEvents",
                columns: new[] { "CompanyId", "ReworkNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionScrapEvents_CausedAtOperationId",
                table: "ProductionScrapEvents",
                column: "CausedAtOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionScrapEvents_CompanyId",
                table: "ProductionScrapEvents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionScrapEvents_TenantId",
                table: "ProductionScrapEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ScpEvt_Date",
                table: "ProductionScrapEvents",
                column: "ScrapRecordedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScpEvt_DetOp",
                table: "ProductionScrapEvents",
                column: "DetectedAtOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScpEvt_Disp",
                table: "ProductionScrapEvents",
                column: "Disposition");

            migrationBuilder.CreateIndex(
                name: "IX_ScpEvt_PRO",
                table: "ProductionScrapEvents",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "UX_ScpEvt_Company_Number",
                table: "ProductionScrapEvents",
                columns: new[] { "CompanyId", "ScrapNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionCompletionEvents");

            migrationBuilder.DropTable(
                name: "ProductionReworkEvents");

            migrationBuilder.DropTable(
                name: "ProductionScrapEvents");
        }
    }
}
