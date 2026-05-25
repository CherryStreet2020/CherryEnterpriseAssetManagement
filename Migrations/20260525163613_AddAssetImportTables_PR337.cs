using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetImportTables_PR337 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SheetName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    ValidRowCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorRowCount = table.Column<int>(type: "integer", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommittedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DiscardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscardedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetImportBatches", x => x.Id);
                    table.CheckConstraint("ck_assetimportbatches_rowcounts_balanced", "\"ValidRowCount\" + \"ErrorRowCount\" <= \"RowCount\"");
                    table.CheckConstraint("ck_assetimportbatches_rowcounts_nonneg", "\"RowCount\" >= 0 AND \"ValidRowCount\" >= 0 AND \"ErrorRowCount\" >= 0");
                    table.CheckConstraint("ck_assetimportbatches_status_range", "\"Status\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_AssetImportBatches_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetImportBatches_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssetImportBatches_Users_CommittedByUserId",
                        column: x => x.CommittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssetImportBatches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetImportRows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    AssetNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LongDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TagNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ManufacturerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResolvedManufacturerId = table.Column<int>(type: "integer", nullable: true),
                    AcquisitionCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ReplacementCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FiscalPurchaseYear = table.Column<int>(type: "integer", nullable: true),
                    UsefulLifeMonths = table.Column<int>(type: "integer", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResolvedLocationId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResolvedDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    SiteCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResolvedSiteId = table.Column<int>(type: "integer", nullable: true),
                    StatusSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResolvedStatus = table.Column<int>(type: "integer", nullable: true),
                    ValidationErrors = table.Column<string>(type: "text", nullable: true),
                    CommittedAssetId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetImportRows", x => x.Id);
                    table.CheckConstraint("ck_assetimportrows_rownumber_pos", "\"RowNumber\" >= 2");
                    table.CheckConstraint("ck_assetimportrows_status_range", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_AssetImportRows_AssetImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "AssetImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetImportBatches_CommittedByUserId",
                table: "AssetImportBatches",
                column: "CommittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetImportBatches_CompanyId_CreatedAt",
                table: "AssetImportBatches",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetImportBatches_CreatedByUserId",
                table: "AssetImportBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetImportBatches_SiteId",
                table: "AssetImportBatches",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetImportRows_BatchId_RowNumber",
                table: "AssetImportRows",
                columns: new[] { "BatchId", "RowNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetImportRows");

            migrationBuilder.DropTable(
                name: "AssetImportBatches");
        }
    }
}
