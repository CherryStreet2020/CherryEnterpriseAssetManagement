using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Sprint11ItemCrossReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "ChangedBy",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "ItemRevisions");

            migrationBuilder.RenameColumn(
                name: "SupersededDate",
                table: "ItemRevisions",
                newName: "ReleasedAtUtc");

            migrationBuilder.RenameColumn(
                name: "Revision",
                table: "ItemRevisions",
                newName: "RevisionCode");

            migrationBuilder.RenameColumn(
                name: "EffectiveDate",
                table: "ItemRevisions",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ChangeDescription",
                table: "ItemRevisions",
                newName: "ChangeReason");

            migrationBuilder.RenameColumn(
                name: "ApprovedDate",
                table: "ItemRevisions",
                newName: "ObsoletedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_ItemRevisions_ItemId_Revision",
                table: "ItemRevisions",
                newName: "IX_ItemRevisions_ItemId_RevisionCode");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Manufacturers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Manufacturers\" SET \"Code\" = 'MFR-' || \"Id\"::text WHERE \"Code\" IS NULL OR \"Code\" = ''");
            
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Manufacturers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Manufacturers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentReleasedRevisionId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "ItemRevisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "ItemRevisions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "ItemRevisions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ItemRevisions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFromUtc",
                table: "ItemRevisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveToUtc",
                table: "ItemRevisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ItemRevisions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ItemRevisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SupersedesItemRevisionId",
                table: "ItemRevisions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemManufacturerParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ManufacturerId = table.Column<int>(type: "integer", nullable: false),
                    MfrPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LifecycleStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DatasheetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemManufacturerParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemManufacturerParts_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemManufacturerParts_Manufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "Manufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorItemParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    VendorPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemManufacturerPartId = table.Column<int>(type: "integer", nullable: true),
                    VendorUom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PackQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    MinOrderQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Preferred = table.Column<bool>(type: "boolean", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ProductPageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorItemParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorItemParts_ItemManufacturerParts_ItemManufacturerPartId",
                        column: x => x.ItemManufacturerPartId,
                        principalTable: "ItemManufacturerParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorItemParts_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorItemParts_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_TenantId_Code",
                table: "Manufacturers",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_CurrentReleasedRevisionId",
                table: "Items",
                column: "CurrentReleasedRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemRevisions_SupersedesItemRevisionId",
                table: "ItemRevisions",
                column: "SupersedesItemRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemManufacturerParts_ItemId_ManufacturerId_MfrPartNumber",
                table: "ItemManufacturerParts",
                columns: new[] { "ItemId", "ManufacturerId", "MfrPartNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemManufacturerParts_ManufacturerId",
                table: "ItemManufacturerParts",
                column: "ManufacturerId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemManufacturerParts_MfrPartNumber",
                table: "ItemManufacturerParts",
                column: "MfrPartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_VendorItemParts_ItemId",
                table: "VendorItemParts",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorItemParts_ItemManufacturerPartId",
                table: "VendorItemParts",
                column: "ItemManufacturerPartId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorItemParts_VendorId_VendorPartNumber",
                table: "VendorItemParts",
                columns: new[] { "VendorId", "VendorPartNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorItemParts_VendorPartNumber",
                table: "VendorItemParts",
                column: "VendorPartNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemRevisions_ItemRevisions_SupersedesItemRevisionId",
                table: "ItemRevisions",
                column: "SupersedesItemRevisionId",
                principalTable: "ItemRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_ItemRevisions_CurrentReleasedRevisionId",
                table: "Items",
                column: "CurrentReleasedRevisionId",
                principalTable: "ItemRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Manufacturers_Tenants_TenantId",
                table: "Manufacturers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemRevisions_ItemRevisions_SupersedesItemRevisionId",
                table: "ItemRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_ItemRevisions_CurrentReleasedRevisionId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Manufacturers_Tenants_TenantId",
                table: "Manufacturers");

            migrationBuilder.DropTable(
                name: "VendorItemParts");

            migrationBuilder.DropTable(
                name: "ItemManufacturerParts");

            migrationBuilder.DropIndex(
                name: "IX_Manufacturers_TenantId_Code",
                table: "Manufacturers");

            migrationBuilder.DropIndex(
                name: "IX_Items_CurrentReleasedRevisionId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_ItemRevisions_SupersedesItemRevisionId",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "CurrentReleasedRevisionId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "EffectiveFromUtc",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "EffectiveToUtc",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "SupersedesItemRevisionId",
                table: "ItemRevisions");

            migrationBuilder.RenameColumn(
                name: "RevisionCode",
                table: "ItemRevisions",
                newName: "Revision");

            migrationBuilder.RenameColumn(
                name: "ReleasedAtUtc",
                table: "ItemRevisions",
                newName: "SupersededDate");

            migrationBuilder.RenameColumn(
                name: "ObsoletedAtUtc",
                table: "ItemRevisions",
                newName: "ApprovedDate");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "ItemRevisions",
                newName: "EffectiveDate");

            migrationBuilder.RenameColumn(
                name: "ChangeReason",
                table: "ItemRevisions",
                newName: "ChangeDescription");

            migrationBuilder.RenameIndex(
                name: "IX_ItemRevisions_ItemId_RevisionCode",
                table: "ItemRevisions",
                newName: "IX_ItemRevisions_ItemId_Revision");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "ItemRevisions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedBy",
                table: "ItemRevisions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ItemRevisions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "ItemRevisions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
