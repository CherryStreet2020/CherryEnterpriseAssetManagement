using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Pr17_PoChangeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "POChangeHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    PurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    AmendmentNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Reason = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ReasonNarrative = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DraftedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedForApprovalAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AffectedDemandLinkCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedProductionOrderCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedOperationCount = table.Column<int>(type: "integer", nullable: false),
                    ShipDateRiskFlag = table.Column<bool>(type: "boolean", nullable: false),
                    TotalValueDelta = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalQuantityDelta = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ImpactNarrative = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    VendorReAcknowledgmentRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ApprovalNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POChangeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POChangeHistories_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POChangeHistories_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_POChangeHistories_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_POChangeHistories_Users_DraftedByUserId",
                        column: x => x.DraftedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "POChangeHistoryLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    POChangeHistoryId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    ChangeType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OriginalQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OriginalUnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OriginalPromiseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginalRequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NewUnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NewPromiseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewRequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AffectedDemandLinkCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedProductionOrderCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedProductionOrderNumbers = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MaxDatePushOutDays = table.Column<int>(type: "integer", nullable: true),
                    ValueDelta = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineNarrative = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POChangeHistoryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POChangeHistoryLines_POChangeHistories_POChangeHistoryId",
                        column: x => x.POChangeHistoryId,
                        principalTable: "POChangeHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_POChangeHistoryLines_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POChangeHistories_ApprovedByUserId",
                table: "POChangeHistories",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_POChangeHistories_CompanyId_AmendmentNumber",
                table: "POChangeHistories",
                columns: new[] { "CompanyId", "AmendmentNumber" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_POChangeHistories_DraftedByUserId",
                table: "POChangeHistories",
                column: "DraftedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_POChangeHistories_Reason",
                table: "POChangeHistories",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_POChangeHistories_Status",
                table: "POChangeHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_POChangeHistories_PurchaseOrderId_IsCurrent",
                table: "POChangeHistories",
                column: "PurchaseOrderId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_POChangeHistoryLines_ChangeType",
                table: "POChangeHistoryLines",
                column: "ChangeType");

            migrationBuilder.CreateIndex(
                name: "IX_POChangeHistoryLines_POChangeHistoryId",
                table: "POChangeHistoryLines",
                column: "POChangeHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_POChangeHistoryLines_PurchaseOrderLineId",
                table: "POChangeHistoryLines",
                column: "PurchaseOrderLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "POChangeHistoryLines");

            migrationBuilder.DropTable(
                name: "POChangeHistories");
        }
    }
}
