using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerItemXrefFsPr6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerItemXrefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CustomerPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerPartDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomerRevision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CustomerDrawingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerDrawingRevision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CustomerSpecificationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerEcoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SupersededByXrefId = table.Column<int>(type: "integer", nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerItemXrefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerItemXrefs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerItemXrefs_CustomerItemXrefs_SupersededByXrefId",
                        column: x => x.SupersededByXrefId,
                        principalTable: "CustomerItemXrefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerItemXrefs_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerItemXrefs_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustItmXref_Cust_PN",
                table: "CustomerItemXrefs",
                columns: new[] { "CustomerId", "CustomerPartNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CustItmXref_Item_Cust",
                table: "CustomerItemXrefs",
                columns: new[] { "ItemId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerItemXrefs_CompanyId",
                table: "CustomerItemXrefs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerItemXrefs_CustomerDrawingNumber",
                table: "CustomerItemXrefs",
                column: "CustomerDrawingNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerItemXrefs_CustomerSpecificationNumber",
                table: "CustomerItemXrefs",
                column: "CustomerSpecificationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerItemXrefs_SupersededByXrefId",
                table: "CustomerItemXrefs",
                column: "SupersededByXrefId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerItemXrefs_TenantId",
                table: "CustomerItemXrefs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_CustItmXref_Cust_PN_Rev_Active_NullTenant",
                table: "CustomerItemXrefs",
                columns: new[] { "CustomerId", "CustomerPartNumber", "CustomerRevision" },
                unique: true,
                filter: "\"TenantId\" IS NULL AND \"Status\" = 0 AND \"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "UX_CustItmXref_Tenant_Cust_PN_Rev_Active",
                table: "CustomerItemXrefs",
                columns: new[] { "TenantId", "CustomerId", "CustomerPartNumber", "CustomerRevision" },
                unique: true,
                filter: "\"Status\" = 0 AND \"IsActive\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerItemXrefs");
        }
    }
}
