using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Pr19_InvoiceMatchResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceMatchResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    VendorInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    MatchRunNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TolerancePriceAbs = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TolerancePricePct = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    ToleranceQtyAbs = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ToleranceQtyPct = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    ToleranceDateDays = table.Column<int>(type: "integer", nullable: false),
                    LinesTotal = table.Column<int>(type: "integer", nullable: false),
                    LinesMatched = table.Column<int>(type: "integer", nullable: false),
                    LinesWithinTolerance = table.Column<int>(type: "integer", nullable: false),
                    LinesException = table.Column<int>(type: "integer", nullable: false),
                    TotalPriceVariance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PostedOnMatch = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PostedJournalEntryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceMatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceMatchResults_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceMatchResults_VendorInvoices_VendorInvoiceId",
                        column: x => x.VendorInvoiceId,
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceMatchResultLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceMatchResultId = table.Column<int>(type: "integer", nullable: false),
                    VendorInvoiceLineId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    GoodsReceiptLineId = table.Column<int>(type: "integer", nullable: true),
                    InvoicedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    InvoicedUnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PoQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PoUnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PriceVariance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PriceVariancePct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    QuantityVariance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DateVarianceDays = table.Column<int>(type: "integer", nullable: true),
                    ExtendedPriceVariance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceMatchResultLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceMatchResultLines_GoodsReceiptLines_GoodsReceiptLineId",
                        column: x => x.GoodsReceiptLineId,
                        principalTable: "GoodsReceiptLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InvoiceMatchResultLines_InvoiceMatchResults_InvoiceMatchRes~",
                        column: x => x.InvoiceMatchResultId,
                        principalTable: "InvoiceMatchResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceMatchResultLines_PurchaseOrderLines_PurchaseOrderLin~",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InvoiceMatchResultLines_VendorInvoiceLines_VendorInvoiceLin~",
                        column: x => x.VendorInvoiceLineId,
                        principalTable: "VendorInvoiceLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceMatchResultLines_GoodsReceiptLineId",
                table: "InvoiceMatchResultLines",
                column: "GoodsReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceMatchResultLines_InvoiceMatchResultId",
                table: "InvoiceMatchResultLines",
                column: "InvoiceMatchResultId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceMatchResultLines_Outcome",
                table: "InvoiceMatchResultLines",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceMatchResultLines_PurchaseOrderLineId",
                table: "InvoiceMatchResultLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceMatchResultLines_VendorInvoiceLineId",
                table: "InvoiceMatchResultLines",
                column: "VendorInvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceMatchResults_CompanyId_MatchRunNumber",
                table: "InvoiceMatchResults",
                columns: new[] { "CompanyId", "MatchRunNumber" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceMatchResults_Outcome",
                table: "InvoiceMatchResults",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "UX_InvoiceMatchResults_VendorInvoiceId_IsCurrent",
                table: "InvoiceMatchResults",
                column: "VendorInvoiceId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceMatchResultLines");

            migrationBuilder.DropTable(
                name: "InvoiceMatchResults");
        }
    }
}
