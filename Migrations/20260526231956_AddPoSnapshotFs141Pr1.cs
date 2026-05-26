using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddPoSnapshotFs141Pr1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotCapturedAtUtc",
                table: "ProductionOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotCapturedBy",
                table: "ProductionOrders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceItemRevisionId",
                table: "ProductionOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceMaterialStructureRevision",
                table: "ProductionOrders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductionMaterialStructures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    SourceMaterialStructureLineId = table.Column<int>(type: "integer", nullable: true),
                    SourceMaterialStructureId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    ChildItemId = table.Column<int>(type: "integer", nullable: false),
                    ChildPartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChildRevision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ChildItemRevisionId = table.Column<int>(type: "integer", nullable: true),
                    ChildItemFingerprintHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    QuantityPer = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ScrapPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    PhaseSequence = table.Column<int>(type: "integer", nullable: true),
                    LineKind = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IssueMethod = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsPhantom = table.Column<bool>(type: "boolean", nullable: false),
                    FrozenStandardCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    FrozenExtendedCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    TypeSpecificProperties = table.Column<string>(type: "jsonb", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CapturedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionMaterialStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialStructures_ItemRevisions_ChildItemRevisio~",
                        column: x => x.ChildItemRevisionId,
                        principalTable: "ItemRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialStructures_Items_ChildItemId",
                        column: x => x.ChildItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialStructures_MaterialStructureLines_SourceM~",
                        column: x => x.SourceMaterialStructureLineId,
                        principalTable: "MaterialStructureLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialStructures_MaterialStructures_SourceMater~",
                        column: x => x.SourceMaterialStructureId,
                        principalTable: "MaterialStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialStructures_ProductionOrders_ProductionOrd~",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_SnapshotCaptured_Partial",
                table: "ProductionOrders",
                column: "SnapshotCapturedAtUtc",
                filter: "\"SnapshotCapturedAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_SourceItemRevisionId",
                table: "ProductionOrders",
                column: "SourceItemRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_ChildItem",
                table: "ProductionMaterialStructures",
                column: "ChildItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_Company",
                table: "ProductionMaterialStructures",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_PO",
                table: "ProductionMaterialStructures",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialStructures_ChildItemRevisionId",
                table: "ProductionMaterialStructures",
                column: "ChildItemRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialStructures_SourceMaterialStructureId",
                table: "ProductionMaterialStructures",
                column: "SourceMaterialStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialStructures_SourceMaterialStructureLineId",
                table: "ProductionMaterialStructures",
                column: "SourceMaterialStructureLineId");

            migrationBuilder.CreateIndex(
                name: "UX_ProdMatStruct_PO_Sequence",
                table: "ProductionMaterialStructures",
                columns: new[] { "ProductionOrderId", "Sequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_ItemRevisions_SourceItemRevisionId",
                table: "ProductionOrders",
                column: "SourceItemRevisionId",
                principalTable: "ItemRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_ItemRevisions_SourceItemRevisionId",
                table: "ProductionOrders");

            migrationBuilder.DropTable(
                name: "ProductionMaterialStructures");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_SnapshotCaptured_Partial",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_SourceItemRevisionId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "SnapshotCapturedAtUtc",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "SnapshotCapturedBy",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "SourceItemRevisionId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "SourceMaterialStructureRevision",
                table: "ProductionOrders");
        }
    }
}
