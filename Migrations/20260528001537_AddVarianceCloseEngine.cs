using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddVarianceCloseEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionCloseEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EstimatedTotalAtClose = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualTotalAtClose = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalVarianceAtClose = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WipBalanceAtClose = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VarianceLineCount = table.Column<int>(type: "integer", nullable: false),
                    VarianceJeCount = table.Column<int>(type: "integer", nullable: false),
                    UnresolvedExceptionCount = table.Column<int>(type: "integer", nullable: false),
                    CloseSuccessful = table.Column<bool>(type: "boolean", nullable: false),
                    CloseMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsReversal = table.Column<bool>(type: "boolean", nullable: false),
                    ReversalOfEventId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionCloseEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionVariances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    VarianceType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EstimatedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VarianceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VariancePercent = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsFavorable = table.Column<bool>(type: "boolean", nullable: false),
                    StandardQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ActualQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    StandardRate = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ActualRate = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    JournalEntryId = table.Column<int>(type: "integer", nullable: true),
                    IsPosted = table.Column<bool>(type: "boolean", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionVariances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProdClose_Company_PRO",
                table: "ProductionCloseEvents",
                columns: new[] { "CompanyId", "ProductionOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProdClose_PRO",
                table: "ProductionCloseEvents",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdClose_Step",
                table: "ProductionCloseEvents",
                column: "Step");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionCloseEvents_TenantId",
                table: "ProductionCloseEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionVariances_TenantId",
                table: "ProductionVariances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdVar_Company_PRO",
                table: "ProductionVariances",
                columns: new[] { "CompanyId", "ProductionOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProdVar_PRO",
                table: "ProductionVariances",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdVar_Type",
                table: "ProductionVariances",
                column: "VarianceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionCloseEvents");

            migrationBuilder.DropTable(
                name: "ProductionVariances");
        }
    }
}
