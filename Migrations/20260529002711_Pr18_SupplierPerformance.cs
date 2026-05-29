using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Pr18_SupplierPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierPerformances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    PeriodType = table.Column<int>(type: "integer", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiptEventsTotal = table.Column<int>(type: "integer", nullable: false),
                    ReceiptEventsOnTime = table.Column<int>(type: "integer", nullable: false),
                    OnTimeDeliveryPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    QuantityReceivedTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityRejectedTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QualityPPM = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    NcrCount = table.Column<int>(type: "integer", nullable: false),
                    PriceVarianceBasisLineCount = table.Column<int>(type: "integer", nullable: false),
                    StandardCostBasisAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ActualCostAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PriceVariancePct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPerformances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPerformances_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformances_CompanyId",
                table: "SupplierPerformances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformances_VendorId",
                table: "SupplierPerformances",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierPerformances_Vendor_Period_IsCurrent",
                table: "SupplierPerformances",
                columns: new[] { "VendorId", "PeriodType" },
                unique: true,
                filter: "\"IsCurrent\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierPerformances");
        }
    }
}
