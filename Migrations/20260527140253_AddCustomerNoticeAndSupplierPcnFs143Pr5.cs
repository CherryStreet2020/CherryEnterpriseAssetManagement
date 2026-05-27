using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerNoticeAndSupplierPcnFs143Pr5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerNotices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    NoticeNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    OriginatingEcrId = table.Column<int>(type: "integer", nullable: true),
                    OriginatingDeviationId = table.Column<int>(type: "integer", nullable: true),
                    OriginatingWaiverId = table.Column<int>(type: "integer", nullable: true),
                    OriginatingConcessionId = table.Column<int>(type: "integer", nullable: true),
                    ChangeDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ImpactDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChangeEffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AffectedSalesOrderReferences = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AffectedContractReferences = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AffectsForm = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFit = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFunction = table.Column<bool>(type: "boolean", nullable: false),
                    SafetyImpact = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryMethod = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SentBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiredResponseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerRespondent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerResponseDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomerResponseText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisputeReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DisputeResolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DisputeResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisputeResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OutboxCorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerNotices_Concessions_OriginatingConcessionId",
                        column: x => x.OriginatingConcessionId,
                        principalTable: "Concessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerNotices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerNotices_Deviations_OriginatingDeviationId",
                        column: x => x.OriginatingDeviationId,
                        principalTable: "Deviations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerNotices_EngineeringChangeRequests_OriginatingEcrId",
                        column: x => x.OriginatingEcrId,
                        principalTable: "EngineeringChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerNotices_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerNotices_Waivers_OriginatingWaiverId",
                        column: x => x.OriginatingWaiverId,
                        principalTable: "Waivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProcessChangeNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PcnNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    VendorId = table.Column<int>(type: "integer", nullable: true),
                    SupplierContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupplierContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    OriginatingEcrId = table.Column<int>(type: "integer", nullable: true),
                    ChangeDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ImpactDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProposedEffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentSpecification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProposedSpecification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AffectsForm = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFit = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsFunction = table.Column<bool>(type: "boolean", nullable: false),
                    SafetyImpact = table.Column<bool>(type: "boolean", nullable: false),
                    FirstArticleRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PpapRequired = table.Column<bool>(type: "boolean", nullable: false),
                    QualityPlanUpdateRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SampleQuantityRequired = table.Column<int>(type: "integer", nullable: true),
                    DeliveryMethod = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SentBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiredResponseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupplierRespondent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupplierResponseDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupplierResponseText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SupplierAcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupplierImpactAssessment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SupplierEstimatedCostImpact = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    SupplierEstimatedLeadTimeImpactDays = table.Column<int>(type: "integer", nullable: true),
                    ImpactAssessmentReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FirstConformingShipmentRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OutboxCorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProcessChangeNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierProcessChangeNotifications_EngineeringChangeRequest~",
                        column: x => x.OriginatingEcrId,
                        principalTable: "EngineeringChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierProcessChangeNotifications_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProcessChangeNotifications_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotice_Status",
                table: "CustomerNotices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotices_CompanyId",
                table: "CustomerNotices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotices_CustomerId",
                table: "CustomerNotices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotices_ItemId",
                table: "CustomerNotices",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotices_OriginatingConcessionId",
                table: "CustomerNotices",
                column: "OriginatingConcessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotices_OriginatingDeviationId",
                table: "CustomerNotices",
                column: "OriginatingDeviationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotices_OriginatingEcrId",
                table: "CustomerNotices",
                column: "OriginatingEcrId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotices_OriginatingWaiverId",
                table: "CustomerNotices",
                column: "OriginatingWaiverId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotices_TenantId",
                table: "CustomerNotices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_CustomerNotice_Company_Number",
                table: "CustomerNotices",
                columns: new[] { "CompanyId", "NoticeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPcn_Status",
                table: "SupplierProcessChangeNotifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProcessChangeNotifications_CompanyId",
                table: "SupplierProcessChangeNotifications",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProcessChangeNotifications_ItemId",
                table: "SupplierProcessChangeNotifications",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProcessChangeNotifications_OriginatingEcrId",
                table: "SupplierProcessChangeNotifications",
                column: "OriginatingEcrId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProcessChangeNotifications_TenantId",
                table: "SupplierProcessChangeNotifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProcessChangeNotifications_VendorId",
                table: "SupplierProcessChangeNotifications",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "UX_SupplierPcn_Company_Number",
                table: "SupplierProcessChangeNotifications",
                columns: new[] { "CompanyId", "PcnNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerNotices");

            migrationBuilder.DropTable(
                name: "SupplierProcessChangeNotifications");
        }
    }
}
