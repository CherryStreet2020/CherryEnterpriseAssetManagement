using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddConcessionFs143Pr4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Concessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ConcessionNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    AffectedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AffectedLotSerials = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginatingEcrId = table.Column<int>(type: "integer", nullable: true),
                    RelatedDeviationId = table.Column<int>(type: "integer", nullable: true),
                    NcrReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InspectionReportReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerContractReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerAcceptingAuthority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerAcceptanceDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerAcceptanceDocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OriginalSpecification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActualCondition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Justification = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Disposition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AffectsForm = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFit = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFunction = table.Column<bool>(type: "boolean", nullable: false),
                    SafetyImpact = table.Column<bool>(type: "boolean", nullable: false),
                    RejectedDisposition = table.Column<int>(type: "integer", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Concessions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Concessions_Deviations_RelatedDeviationId",
                        column: x => x.RelatedDeviationId,
                        principalTable: "Deviations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Concessions_EngineeringChangeRequests_OriginatingEcrId",
                        column: x => x.OriginatingEcrId,
                        principalTable: "EngineeringChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Concessions_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Concessions_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Concession_Status",
                table: "Concessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Concessions_CompanyId",
                table: "Concessions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Concessions_CustomerId",
                table: "Concessions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Concessions_ItemId",
                table: "Concessions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Concessions_OriginatingEcrId",
                table: "Concessions",
                column: "OriginatingEcrId");

            migrationBuilder.CreateIndex(
                name: "IX_Concessions_ProductionOrderId",
                table: "Concessions",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Concessions_RelatedDeviationId",
                table: "Concessions",
                column: "RelatedDeviationId");

            migrationBuilder.CreateIndex(
                name: "IX_Concessions_TenantId",
                table: "Concessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_Concession_Company_Number",
                table: "Concessions",
                columns: new[] { "CompanyId", "ConcessionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Concessions");
        }
    }
}
