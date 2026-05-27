using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrectiveActionRequestFs143Pr6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrectiveActionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Severity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    VendorId = table.Column<int>(type: "integer", nullable: true),
                    OriginatingEcrId = table.Column<int>(type: "integer", nullable: true),
                    RelatedDeviationId = table.Column<int>(type: "integer", nullable: true),
                    RelatedConcessionId = table.Column<int>(type: "integer", nullable: true),
                    NcrReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CustomerComplaintReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AuditFindingReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NonConformanceDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AffectedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AffectedLotSerials = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AffectsForm = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFit = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFunction = table.Column<bool>(type: "boolean", nullable: false),
                    SafetyImpact = table.Column<bool>(type: "boolean", nullable: false),
                    RegulatoryImpact = table.Column<bool>(type: "boolean", nullable: false),
                    RootCauseAnalysis = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RootCauseMethodology = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RootCauseIdentifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RootCauseIdentifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContainmentAction = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ContainmentCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrectiveActionPlan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CorrectiveActionDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreventiveActionPlan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ImplementationNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ImplementationCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImplementedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VerificationMethod = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    VerificationResults = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VerificationEffective = table.Column<bool>(type: "boolean", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DaysToClose = table.Column<int>(type: "integer", nullable: true),
                    IssuedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedTo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponsibleDepartment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionRequests_Concessions_RelatedConcessionId",
                        column: x => x.RelatedConcessionId,
                        principalTable: "Concessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionRequests_Deviations_RelatedDeviationId",
                        column: x => x.RelatedDeviationId,
                        principalTable: "Deviations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionRequests_EngineeringChangeRequests_Originat~",
                        column: x => x.OriginatingEcrId,
                        principalTable: "EngineeringChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionRequests_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionRequests_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionRequests_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Car_Severity",
                table: "CorrectiveActionRequests",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Car_Status",
                table: "CorrectiveActionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_CompanyId",
                table: "CorrectiveActionRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_CustomerId",
                table: "CorrectiveActionRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_ItemId",
                table: "CorrectiveActionRequests",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_OriginatingEcrId",
                table: "CorrectiveActionRequests",
                column: "OriginatingEcrId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_ProductionOrderId",
                table: "CorrectiveActionRequests",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_RelatedConcessionId",
                table: "CorrectiveActionRequests",
                column: "RelatedConcessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_RelatedDeviationId",
                table: "CorrectiveActionRequests",
                column: "RelatedDeviationId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_TenantId",
                table: "CorrectiveActionRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionRequests_VendorId",
                table: "CorrectiveActionRequests",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "UX_Car_Company_Number",
                table: "CorrectiveActionRequests",
                columns: new[] { "CompanyId", "CarNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrectiveActionRequests");
        }
    }
}
