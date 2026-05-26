using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSiteFsPr2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemSites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsStocked = table.Column<bool>(type: "boolean", nullable: true),
                    IsPurchasable = table.Column<bool>(type: "boolean", nullable: true),
                    IsCriticalSpare = table.Column<bool>(type: "boolean", nullable: true),
                    StockPolicy = table.Column<int>(type: "integer", nullable: true),
                    ABCClass = table.Column<int>(type: "integer", nullable: true),
                    MinQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaxQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ReorderQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    SafetyStock = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    EOQ = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    ReorderMethod = table.Column<int>(type: "integer", nullable: true),
                    AutoReorderEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    CostMethod = table.Column<int>(type: "integer", nullable: true),
                    StandardCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AverageCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    LastPurchaseCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ListPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PreferredVendorId = table.Column<int>(type: "integer", nullable: true),
                    DefaultBuyerId = table.Column<int>(type: "integer", nullable: true),
                    DefaultLocationId = table.Column<int>(type: "integer", nullable: true),
                    DefaultWarehouse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultBin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TrackingType = table.Column<int>(type: "integer", nullable: true),
                    ShelfLifeDays = table.Column<int>(type: "integer", nullable: true),
                    IsHazmat = table.Column<bool>(type: "boolean", nullable: true),
                    StorageRequirements = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ItemGroupId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSites_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemSites_ItemGroups_ItemGroupId",
                        column: x => x.ItemGroupId,
                        principalTable: "ItemGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemSites_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSites_Locations_DefaultLocationId",
                        column: x => x.DefaultLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemSites_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSites_Vendors_PreferredVendorId",
                        column: x => x.PreferredVendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_CompanyId",
                table: "ItemSites",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_DefaultLocationId",
                table: "ItemSites",
                column: "DefaultLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_ItemGroupId",
                table: "ItemSites",
                column: "ItemGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_ItemId",
                table: "ItemSites",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_ItemId_SiteId",
                table: "ItemSites",
                columns: new[] { "ItemId", "SiteId" },
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_PreferredVendorId",
                table: "ItemSites",
                column: "PreferredVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_SiteId",
                table: "ItemSites",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_TenantId",
                table: "ItemSites",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSites_TenantId_ItemId_SiteId",
                table: "ItemSites",
                columns: new[] { "TenantId", "ItemId", "SiteId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemSites");
        }
    }
}
