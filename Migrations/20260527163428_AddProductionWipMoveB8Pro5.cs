using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionWipMoveB8Pro5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoAdvanceOnCompletion",
                table: "ProductionOperations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableQty",
                table: "ProductionOperations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "QualityHoldActive",
                table: "ProductionOperations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QualityHoldReason",
                table: "ProductionOperations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductionWipMoves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    MoveNumber = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    FromOperationId = table.Column<int>(type: "integer", nullable: false),
                    ToOperationId = table.Column<int>(type: "integer", nullable: false),
                    FromSequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    ToSequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    MoveType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MoveReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TriggeredByTransactionId = table.Column<int>(type: "integer", nullable: true),
                    OriginalMoveId = table.Column<int>(type: "integer", nullable: true),
                    QualityHoldBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    QualityHoldReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QualityHoldReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QualityHoldReleasedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UnitCostAtMove = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalCostAtMove = table.Column<decimal>(type: "numeric", nullable: true),
                    LotNumbers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SerialNumbers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionWipMoves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionWipMoves_ProductionOperationTransactions_Triggere~",
                        column: x => x.TriggeredByTransactionId,
                        principalTable: "ProductionOperationTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionWipMoves_ProductionOperations_FromOperationId",
                        column: x => x.FromOperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionWipMoves_ProductionOperations_ToOperationId",
                        column: x => x.ToOperationId,
                        principalTable: "ProductionOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionWipMoves_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionWipMoves_ProductionWipMoves_OriginalMoveId",
                        column: x => x.OriginalMoveId,
                        principalTable: "ProductionWipMoves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWipMoves_CompanyId",
                table: "ProductionWipMoves",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWipMoves_OriginalMoveId",
                table: "ProductionWipMoves",
                column: "OriginalMoveId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWipMoves_TenantId",
                table: "ProductionWipMoves",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWipMoves_TriggeredByTransactionId",
                table: "ProductionWipMoves",
                column: "TriggeredByTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WipMove_Date",
                table: "ProductionWipMoves",
                column: "MovedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WipMove_FromOp",
                table: "ProductionWipMoves",
                column: "FromOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WipMove_PRO",
                table: "ProductionWipMoves",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WipMove_ToOp",
                table: "ProductionWipMoves",
                column: "ToOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WipMove_Type",
                table: "ProductionWipMoves",
                column: "MoveType");

            migrationBuilder.CreateIndex(
                name: "UX_WipMove_Company_Number",
                table: "ProductionWipMoves",
                columns: new[] { "CompanyId", "MoveNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionWipMoves");

            migrationBuilder.DropColumn(
                name: "AutoAdvanceOnCompletion",
                table: "ProductionOperations");

            migrationBuilder.DropColumn(
                name: "AvailableQty",
                table: "ProductionOperations");

            migrationBuilder.DropColumn(
                name: "QualityHoldActive",
                table: "ProductionOperations");

            migrationBuilder.DropColumn(
                name: "QualityHoldReason",
                table: "ProductionOperations");
        }
    }
}
