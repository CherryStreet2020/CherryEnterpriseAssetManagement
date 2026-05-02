using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Sprint12ProcurementGradeParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemAlternates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    AlternateItemId = table.Column<int>(type: "integer", nullable: false),
                    AlternateType = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemAlternates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemAlternates_Items_AlternateItemId",
                        column: x => x.AlternateItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemAlternates_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemAlternates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemAlternates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItemApprovedVendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemApprovedVendors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemApprovedVendors_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemApprovedVendors_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemApprovedVendors_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemApprovedVendors_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemApprovedVendors_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemApprovedVendors_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemSupersessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    OldItemId = table.Column<int>(type: "integer", nullable: false),
                    NewItemId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSupersessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSupersessions_Items_NewItemId",
                        column: x => x.NewItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSupersessions_Items_OldItemId",
                        column: x => x.OldItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSupersessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemSupersessions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemAlternates_AlternateItemId",
                table: "ItemAlternates",
                column: "AlternateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAlternates_CreatedByUserId",
                table: "ItemAlternates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAlternates_ItemId",
                table: "ItemAlternates",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemAlternates_TenantId_ItemId_AlternateItemId",
                table: "ItemAlternates",
                columns: new[] { "TenantId", "ItemId", "AlternateItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemApprovedVendors_CompanyId",
                table: "ItemApprovedVendors",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemApprovedVendors_CreatedByUserId",
                table: "ItemApprovedVendors",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemApprovedVendors_ItemId",
                table: "ItemApprovedVendors",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemApprovedVendors_SiteId",
                table: "ItemApprovedVendors",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemApprovedVendors_TenantId_ItemId_VendorId",
                table: "ItemApprovedVendors",
                columns: new[] { "TenantId", "ItemId", "VendorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemApprovedVendors_VendorId",
                table: "ItemApprovedVendors",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSupersessions_CreatedByUserId",
                table: "ItemSupersessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSupersessions_NewItemId",
                table: "ItemSupersessions",
                column: "NewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSupersessions_OldItemId",
                table: "ItemSupersessions",
                column: "OldItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSupersessions_TenantId_OldItemId",
                table: "ItemSupersessions",
                columns: new[] { "TenantId", "OldItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemAlternates");

            migrationBuilder.DropTable(
                name: "ItemApprovedVendors");

            migrationBuilder.DropTable(
                name: "ItemSupersessions");
        }
    }
}
