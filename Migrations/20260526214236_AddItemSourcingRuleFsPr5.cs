using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSourcingRuleFsPr5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemSourcingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    VendorId = table.Column<int>(type: "integer", nullable: true),
                    TransferFromSiteId = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    SourceMethod = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    AllocationPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    MinOrderQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaxOrderQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    LeadTimeDaysOverride = table.Column<int>(type: "integer", nullable: true),
                    ApprovalState = table.Column<int>(type: "integer", nullable: false),
                    IsCustomerMandated = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SuspendedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSourcingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemSourcingRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemSourcingRules_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemSourcingRules_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemSourcingRules_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemSourcingRules_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemSourcingRules_CompanyId",
                table: "ItemSourcingRules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSourcingRules_CustomerId",
                table: "ItemSourcingRules",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSourcingRules_SiteId",
                table: "ItemSourcingRules",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSourcingRules_TenantId",
                table: "ItemSourcingRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSourcingRules_TransferFromSiteId",
                table: "ItemSourcingRules",
                column: "TransferFromSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSourcingRules_VendorId",
                table: "ItemSourcingRules",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemSrcRule_ItemSiteState_Prio",
                table: "ItemSourcingRules",
                columns: new[] { "ItemId", "SiteId", "ApprovalState", "Priority" });

            migrationBuilder.CreateIndex(
                name: "UX_ItemSrcRule_Item_Site_Vendor_Prio_Active_NullTenant",
                table: "ItemSourcingRules",
                columns: new[] { "ItemId", "SiteId", "VendorId", "Priority" },
                unique: true,
                filter: "\"TenantId\" IS NULL AND \"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "UX_ItemSrcRule_Tenant_Item_Site_Vendor_Prio_Active",
                table: "ItemSourcingRules",
                columns: new[] { "TenantId", "ItemId", "SiteId", "VendorId", "Priority" },
                unique: true,
                filter: "\"IsActive\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemSourcingRules");
        }
    }
}
