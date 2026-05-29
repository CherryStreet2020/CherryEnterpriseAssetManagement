using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B7WaveC_MakeBuyDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MakeBuyDecisionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    CapacityThresholdPct = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                    DrumOffloadCostTolerancePct = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                    BuyDecisionScoreThreshold = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    WeightCapacity = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    WeightCostDelta = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    WeightBreakEven = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    WeightLeadTime = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    WeightQualityRisk = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    FinalTieBreak = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakeBuyDecisionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MakeBuyDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Context = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    BuyScore = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    WasHardGated = table.Column<bool>(type: "boolean", nullable: false),
                    HardGateReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RationaleText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FactorBreakdown = table.Column<string>(type: "jsonb", nullable: true),
                    MakeCostFullyLoaded = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    BuyCostLanded = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    BottleneckWorkCenterCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BottleneckLoadPct = table.Column<decimal>(type: "numeric(7,2)", nullable: true),
                    RoutedThroughDrum = table.Column<bool>(type: "boolean", nullable: false),
                    MakeCompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VendorDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChosenSupplierId = table.Column<int>(type: "integer", nullable: true),
                    ChosenQuoteId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakeBuyDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MakeBuyDecisions_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MakeBuyDecisionPolicies_Company_Default",
                table: "MakeBuyDecisionPolicies",
                column: "CompanyId",
                unique: true,
                filter: "\"SiteId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_MakeBuyDecisionPolicies_Company_Site",
                table: "MakeBuyDecisionPolicies",
                columns: new[] { "CompanyId", "SiteId" },
                unique: true,
                filter: "\"SiteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MakeBuyDecisions_Company_DecidedAt",
                table: "MakeBuyDecisions",
                columns: new[] { "CompanyId", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MakeBuyDecisions_Company_Item",
                table: "MakeBuyDecisions",
                columns: new[] { "CompanyId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_MakeBuyDecisions_ItemId",
                table: "MakeBuyDecisions",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MakeBuyDecisionPolicies");

            migrationBuilder.DropTable(
                name: "MakeBuyDecisions");
        }
    }
}
