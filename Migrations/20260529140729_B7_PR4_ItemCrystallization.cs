using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B7_PR4_ItemCrystallization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemCrystallizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    SourceProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    CrystallizationNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedItemId = table.Column<int>(type: "integer", nullable: true),
                    MatchedItemId = table.Column<int>(type: "integer", nullable: true),
                    StructureFingerprintHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SeededStandardCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CostSource = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AsBuiltPartNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AsBuiltPartRev = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AsBuiltDrawingNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AsBuiltDrawingRev = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AsBuiltEcoEffectivity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RationaleText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CrystallizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CrystallizedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsReversed = table.Column<bool>(type: "boolean", nullable: false),
                    ReversedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemCrystallizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemCrystallizations_Items_CreatedItemId",
                        column: x => x.CreatedItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemCrystallizations_Items_MatchedItemId",
                        column: x => x.MatchedItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemCrystallizations_ProductionOrders_SourceProductionOrder~",
                        column: x => x.SourceProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemCrystallization_Company",
                table: "ItemCrystallizations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCrystallization_CreatedItem_Partial",
                table: "ItemCrystallizations",
                column: "CreatedItemId",
                filter: "\"CreatedItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCrystallization_Fingerprint_Partial",
                table: "ItemCrystallizations",
                column: "StructureFingerprintHash",
                filter: "\"StructureFingerprintHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCrystallization_SourcePRO",
                table: "ItemCrystallizations",
                column: "SourceProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCrystallizations_MatchedItemId",
                table: "ItemCrystallizations",
                column: "MatchedItemId");

            migrationBuilder.CreateIndex(
                name: "UX_ItemCrystallization_Company_Number",
                table: "ItemCrystallizations",
                columns: new[] { "CompanyId", "CrystallizationNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemCrystallizations");
        }
    }
}
