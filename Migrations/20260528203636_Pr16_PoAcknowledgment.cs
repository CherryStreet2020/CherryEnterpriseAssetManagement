using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Pr16_PoAcknowledgment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "POAcknowledgments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    PurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    AcknowledgmentNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Method = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseDueByUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedByVendorContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedPromiseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VendorNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BuyerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AllLinesAcceptedAsOrdered = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POAcknowledgments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POAcknowledgments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POAcknowledgments_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_POAcknowledgments_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "POAcknowledgmentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    POAcknowledgmentId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderLineId = table.Column<int>(type: "integer", nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ConfirmedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OrderedUnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ConfirmedUnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedPromiseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ExceptionType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ExceptionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExceptionApproved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ExceptionApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ExceptionApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POAcknowledgmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POAcknowledgmentLines_POAcknowledgments_POAcknowledgmentId",
                        column: x => x.POAcknowledgmentId,
                        principalTable: "POAcknowledgments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_POAcknowledgmentLines_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POAcknowledgmentLines_Users_ExceptionApprovedByUserId",
                        column: x => x.ExceptionApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgmentLines_ExceptionApprovedByUserId",
                table: "POAcknowledgmentLines",
                column: "ExceptionApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgmentLines_ExceptionType",
                table: "POAcknowledgmentLines",
                column: "ExceptionType");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgmentLines_POAcknowledgmentId",
                table: "POAcknowledgmentLines",
                column: "POAcknowledgmentId");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgmentLines_POAcknowledgmentId_PurchaseOrderLineId",
                table: "POAcknowledgmentLines",
                columns: new[] { "POAcknowledgmentId", "PurchaseOrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgmentLines_PurchaseOrderLineId",
                table: "POAcknowledgmentLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgments_CompanyId_AcknowledgmentNumber",
                table: "POAcknowledgments",
                columns: new[] { "CompanyId", "AcknowledgmentNumber" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgments_PurchaseOrderId",
                table: "POAcknowledgments",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgments_PurchaseOrderId_IsCurrent",
                table: "POAcknowledgments",
                columns: new[] { "PurchaseOrderId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgments_RequestedByUserId",
                table: "POAcknowledgments",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgments_ResponseDueByUtc",
                table: "POAcknowledgments",
                column: "ResponseDueByUtc");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgments_Status",
                table: "POAcknowledgments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "POAcknowledgmentLines");

            migrationBuilder.DropTable(
                name: "POAcknowledgments");
        }
    }
}
