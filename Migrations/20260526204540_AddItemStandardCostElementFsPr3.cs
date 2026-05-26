using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddItemStandardCostElementFsPr3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemStandardCostElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    ElementType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CalculationNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemStandardCostElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemStandardCostElements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemStandardCostElements_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemStandardCostElements_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemStandardCostElements_CompanyId",
                table: "ItemStandardCostElements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemStandardCostElements_ItemId",
                table: "ItemStandardCostElements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemStandardCostElements_ItemId_ElementType_EffectiveToUtc",
                table: "ItemStandardCostElements",
                columns: new[] { "ItemId", "ElementType", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemStandardCostElements_SiteId",
                table: "ItemStandardCostElements",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemStandardCostElements_TenantId",
                table: "ItemStandardCostElements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_ItemStdCost_Item_Site_Elem_From_NullTenant",
                table: "ItemStandardCostElements",
                columns: new[] { "ItemId", "SiteId", "ElementType", "EffectiveFromUtc" },
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ItemStdCost_Tenant_Item_Site_Elem_From",
                table: "ItemStandardCostElements",
                columns: new[] { "TenantId", "ItemId", "SiteId", "ElementType", "EffectiveFromUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemStandardCostElements");
        }
    }
}
