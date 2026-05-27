using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddEcrEcoChangeControlFs143Pr1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EcoApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EcoId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    StageOrder = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiredApprover = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcoApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EcoLineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EcoId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    AffectedItemId = table.Column<int>(type: "integer", nullable: true),
                    AffectedDocumentId = table.Column<int>(type: "integer", nullable: true),
                    AffectedDocumentVersionId = table.Column<int>(type: "integer", nullable: true),
                    NewDocumentVersionId = table.Column<int>(type: "integer", nullable: true),
                    ChangeDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BeforeValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AfterValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Disposition = table.Column<int>(type: "integer", nullable: false, defaultValue: 99),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcoLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcoLineItems_DocumentVersions_AffectedDocumentVersionId",
                        column: x => x.AffectedDocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcoLineItems_DocumentVersions_NewDocumentVersionId",
                        column: x => x.NewDocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcoLineItems_Documents_AffectedDocumentId",
                        column: x => x.AffectedDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcoLineItems_Items_AffectedItemId",
                        column: x => x.AffectedItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EngineeringChangeOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    EcoNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SourceEcrId = table.Column<int>(type: "integer", nullable: false),
                    Urgency = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EffectivityType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectivitySerialFrom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EffectivitySerialTo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EffectivityLotFrom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EffectivityLotTo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EffectivityProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    RequiresFaiRetrigger = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresCustomerNotice = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerNoticeSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiresRegulatoryNotice = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImplementedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImplementedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngineeringChangeOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngineeringChangeOrders_ProductionOrders_EffectivityProduct~",
                        column: x => x.EffectivityProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EngineeringChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    EcrNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ChangeReason = table.Column<int>(type: "integer", nullable: false, defaultValue: 99),
                    Urgency = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AffectsForm = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFit = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFunction = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsSafety = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsCustomers = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsRegulatory = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedItemId = table.Column<int>(type: "integer", nullable: true),
                    LinkedDocumentId = table.Column<int>(type: "integer", nullable: true),
                    LinkedProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    LinkedCustomerId = table.Column<int>(type: "integer", nullable: true),
                    ResultingEcoId = table.Column<int>(type: "integer", nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngineeringChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngineeringChangeRequests_Customers_LinkedCustomerId",
                        column: x => x.LinkedCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EngineeringChangeRequests_Documents_LinkedDocumentId",
                        column: x => x.LinkedDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EngineeringChangeRequests_EngineeringChangeOrders_Resulting~",
                        column: x => x.ResultingEcoId,
                        principalTable: "EngineeringChangeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EngineeringChangeRequests_Items_LinkedItemId",
                        column: x => x.LinkedItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EngineeringChangeRequests_ProductionOrders_LinkedProduction~",
                        column: x => x.LinkedProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EcoApproval_Status",
                table: "EcoApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EcoApprovals_CompanyId",
                table: "EcoApprovals",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoApprovals_EcoId",
                table: "EcoApprovals",
                column: "EcoId");

            migrationBuilder.CreateIndex(
                name: "UX_EcoApproval_Eco_Stage",
                table: "EcoApprovals",
                columns: new[] { "EcoId", "StageOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcoLineItems_AffectedDocumentId",
                table: "EcoLineItems",
                column: "AffectedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoLineItems_AffectedDocumentVersionId",
                table: "EcoLineItems",
                column: "AffectedDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoLineItems_AffectedItemId",
                table: "EcoLineItems",
                column: "AffectedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoLineItems_CompanyId",
                table: "EcoLineItems",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoLineItems_EcoId",
                table: "EcoLineItems",
                column: "EcoId");

            migrationBuilder.CreateIndex(
                name: "IX_EcoLineItems_NewDocumentVersionId",
                table: "EcoLineItems",
                column: "NewDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "UX_EcoLineItem_Eco_Sequence",
                table: "EcoLineItems",
                columns: new[] { "EcoId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Eco_EffectiveFrom_Partial",
                table: "EngineeringChangeOrders",
                column: "EffectiveFromUtc",
                filter: "\"EffectiveFromUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Eco_EffType",
                table: "EngineeringChangeOrders",
                column: "EffectivityType");

            migrationBuilder.CreateIndex(
                name: "IX_Eco_Status",
                table: "EngineeringChangeOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Eco_Urgency",
                table: "EngineeringChangeOrders",
                column: "Urgency");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeOrders_CompanyId",
                table: "EngineeringChangeOrders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeOrders_EffectivityProductionOrderId",
                table: "EngineeringChangeOrders",
                column: "EffectivityProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeOrders_SourceEcrId",
                table: "EngineeringChangeOrders",
                column: "SourceEcrId");

            migrationBuilder.CreateIndex(
                name: "UX_Eco_Company_Number",
                table: "EngineeringChangeOrders",
                columns: new[] { "CompanyId", "EcoNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ecr_Status",
                table: "EngineeringChangeRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Ecr_Urgency",
                table: "EngineeringChangeRequests",
                column: "Urgency");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeRequests_CompanyId",
                table: "EngineeringChangeRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeRequests_LinkedCustomerId",
                table: "EngineeringChangeRequests",
                column: "LinkedCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeRequests_LinkedDocumentId",
                table: "EngineeringChangeRequests",
                column: "LinkedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeRequests_LinkedItemId",
                table: "EngineeringChangeRequests",
                column: "LinkedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeRequests_LinkedProductionOrderId",
                table: "EngineeringChangeRequests",
                column: "LinkedProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringChangeRequests_ResultingEcoId",
                table: "EngineeringChangeRequests",
                column: "ResultingEcoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Ecr_Company_Number",
                table: "EngineeringChangeRequests",
                columns: new[] { "CompanyId", "EcrNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EcoApprovals_EngineeringChangeOrders_EcoId",
                table: "EcoApprovals",
                column: "EcoId",
                principalTable: "EngineeringChangeOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EcoLineItems_EngineeringChangeOrders_EcoId",
                table: "EcoLineItems",
                column: "EcoId",
                principalTable: "EngineeringChangeOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EngineeringChangeOrders_EngineeringChangeRequests_SourceEcr~",
                table: "EngineeringChangeOrders",
                column: "SourceEcrId",
                principalTable: "EngineeringChangeRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EngineeringChangeRequests_EngineeringChangeOrders_Resulting~",
                table: "EngineeringChangeRequests");

            migrationBuilder.DropTable(
                name: "EcoApprovals");

            migrationBuilder.DropTable(
                name: "EcoLineItems");

            migrationBuilder.DropTable(
                name: "EngineeringChangeOrders");

            migrationBuilder.DropTable(
                name: "EngineeringChangeRequests");
        }
    }
}
