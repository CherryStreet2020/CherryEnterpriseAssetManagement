using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B11_R2_5_ToolAndResourceBridges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "ProductionResources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToolId",
                table: "ProductionResources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VendorId",
                table: "ProductionResources",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ToolType = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CribLocation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsControlled = table.Column<bool>(type: "boolean", nullable: false),
                    CalibrationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    LastCalibratedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CalibrationDueUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: true),
                    AssetId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tools_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionResources_Employee_Partial",
                table: "ProductionResources",
                column: "EmployeeId",
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionResources_Tool_Partial",
                table: "ProductionResources",
                column: "ToolId",
                filter: "\"ToolId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionResources_Vendor_Partial",
                table: "ProductionResources",
                column: "VendorId",
                filter: "\"VendorId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_Asset_Partial",
                table: "Tools",
                column: "AssetId",
                filter: "\"AssetId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_CompanyId",
                table: "Tools",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_ToolType",
                table: "Tools",
                column: "ToolType");

            migrationBuilder.CreateIndex(
                name: "UX_Tools_Company_Code",
                table: "Tools",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionResources_Employees_EmployeeId",
                table: "ProductionResources",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionResources_Tools_ToolId",
                table: "ProductionResources",
                column: "ToolId",
                principalTable: "Tools",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionResources_Vendors_VendorId",
                table: "ProductionResources",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionResources_Employees_EmployeeId",
                table: "ProductionResources");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionResources_Tools_ToolId",
                table: "ProductionResources");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionResources_Vendors_VendorId",
                table: "ProductionResources");

            migrationBuilder.DropTable(
                name: "Tools");

            migrationBuilder.DropIndex(
                name: "IX_ProductionResources_Employee_Partial",
                table: "ProductionResources");

            migrationBuilder.DropIndex(
                name: "IX_ProductionResources_Tool_Partial",
                table: "ProductionResources");

            migrationBuilder.DropIndex(
                name: "IX_ProductionResources_Vendor_Partial",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "ToolId",
                table: "ProductionResources");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "ProductionResources");
        }
    }
}
