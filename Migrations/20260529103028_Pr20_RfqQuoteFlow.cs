using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Pr20_RfqQuoteFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierRFQs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    RfqNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RequiredByDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuotesDueUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AwardedQuoteId = table.Column<int>(type: "integer", nullable: true),
                    ResultingPurchaseOrderId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRFQs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierRFQs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierRFQs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierQuotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    SupplierRFQId = table.Column<int>(type: "integer", nullable: false),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    VendorQuoteReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntilDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    TotalQuotedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CompositeScore = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    PriceScore = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    LeadTimeScore = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    RankPosition = table.Column<int>(type: "integer", nullable: true),
                    IsWinner = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ScoreReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupplierOnTimeDeliveryPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierQuotes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierQuotes_SupplierRFQs_SupplierRFQId",
                        column: x => x.SupplierRFQId,
                        principalTable: "SupplierRFQs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierQuotes_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierRFQLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierRFQId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    PartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UOM = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProductionSupplyDemandId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    BomLineId = table.Column<int>(type: "integer", nullable: true),
                    OperationSequence = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRFQLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierRFQLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierRFQLines_SupplierRFQs_SupplierRFQId",
                        column: x => x.SupplierRFQId,
                        principalTable: "SupplierRFQs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierQuoteLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierQuoteId = table.Column<int>(type: "integer", nullable: false),
                    SupplierRFQLineId = table.Column<int>(type: "integer", nullable: false),
                    QuotedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuotedUnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierQuoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierQuoteLines_SupplierQuotes_SupplierQuoteId",
                        column: x => x.SupplierQuoteId,
                        principalTable: "SupplierQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierQuoteLines_SupplierRFQLines_SupplierRFQLineId",
                        column: x => x.SupplierRFQLineId,
                        principalTable: "SupplierRFQLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuoteLines_SupplierQuoteId",
                table: "SupplierQuoteLines",
                column: "SupplierQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuoteLines_SupplierRFQLineId",
                table: "SupplierQuoteLines",
                column: "SupplierRFQLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotes_CompanyId",
                table: "SupplierQuotes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotes_Status",
                table: "SupplierQuotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotes_SupplierRFQId",
                table: "SupplierQuotes",
                column: "SupplierRFQId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotes_SupplierRFQId_VendorId",
                table: "SupplierQuotes",
                columns: new[] { "SupplierRFQId", "VendorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotes_VendorId",
                table: "SupplierQuotes",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRFQLines_ItemId",
                table: "SupplierRFQLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRFQLines_SupplierRFQId",
                table: "SupplierRFQLines",
                column: "SupplierRFQId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRFQs_CompanyId_RfqNumber",
                table: "SupplierRFQs",
                columns: new[] { "CompanyId", "RfqNumber" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRFQs_CreatedByUserId",
                table: "SupplierRFQs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRFQs_Status",
                table: "SupplierRFQs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierQuoteLines");

            migrationBuilder.DropTable(
                name: "SupplierQuotes");

            migrationBuilder.DropTable(
                name: "SupplierRFQLines");

            migrationBuilder.DropTable(
                name: "SupplierRFQs");
        }
    }
}
