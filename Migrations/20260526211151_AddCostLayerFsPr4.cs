using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddCostLayerFsPr4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostLayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    LayerNumber = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiptType = table.Column<int>(type: "integer", nullable: false),
                    ReceiptReferenceId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptDocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HeatNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VendorLot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VendorReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExhaustedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostLayers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostLayers_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostLayers_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayer_ItemSiteStatus_ReceivedAt",
                table: "CostLayers",
                columns: new[] { "ItemId", "SiteId", "Status", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayer_ReceiptType_Ref",
                table: "CostLayers",
                columns: new[] { "ReceiptType", "ReceiptReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_CompanyId",
                table: "CostLayers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_HeatNumber",
                table: "CostLayers",
                column: "HeatNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_LotNumber",
                table: "CostLayers",
                column: "LotNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_SerialNumber",
                table: "CostLayers",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_SiteId",
                table: "CostLayers",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_CostLayers_TenantId",
                table: "CostLayers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_CostLayer_Item_Site_Layer_NullTenant",
                table: "CostLayers",
                columns: new[] { "ItemId", "SiteId", "LayerNumber" },
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_CostLayer_Tenant_Item_Site_Layer",
                table: "CostLayers",
                columns: new[] { "TenantId", "ItemId", "SiteId", "LayerNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostLayers");
        }
    }
}
