using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviationFs143Pr2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Deviations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    DeviationNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    OriginatingEcrId = table.Column<int>(type: "integer", nullable: true),
                    MaxQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ConsumedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxProductionOrders = table.Column<int>(type: "integer", nullable: true),
                    ConsumedProductionOrders = table.Column<int>(type: "integer", nullable: false),
                    AffectsForm = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFit = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFunction = table.Column<bool>(type: "boolean", nullable: false),
                    SafetyImpact = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerApprovalReceived = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerApprovalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OriginalSpecification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeviatedCondition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Justification = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Disposition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deviations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deviations_EngineeringChangeRequests_OriginatingEcrId",
                        column: x => x.OriginatingEcrId,
                        principalTable: "EngineeringChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Deviations_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Deviations_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deviation_Expiry",
                table: "Deviations",
                column: "ExpirationDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Deviation_Status",
                table: "Deviations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Deviations_CompanyId",
                table: "Deviations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Deviations_ItemId",
                table: "Deviations",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Deviations_OriginatingEcrId",
                table: "Deviations",
                column: "OriginatingEcrId");

            migrationBuilder.CreateIndex(
                name: "IX_Deviations_ProductionOrderId",
                table: "Deviations",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Deviations_TenantId",
                table: "Deviations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_Deviation_Company_Number",
                table: "Deviations",
                columns: new[] { "CompanyId", "DeviationNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deviations");
        }
    }
}
