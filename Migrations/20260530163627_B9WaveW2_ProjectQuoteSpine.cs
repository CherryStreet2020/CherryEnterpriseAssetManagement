using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9WaveW2_ProjectQuoteSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectRfqs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    RfqNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomerRfqReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OwnerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstimatorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SalespersonName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRfqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectRfqs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectRfqs_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectQuotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectRfqId = table.Column<int>(type: "integer", nullable: true),
                    QuoteNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QuoteType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Scenario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    OwnerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstimatorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SalespersonName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AwardedRevisionId = table.Column<int>(type: "integer", nullable: true),
                    Probability = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    LostReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NoBidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Competitor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerFeedback = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectQuotes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectQuotes_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectQuotes_ProjectRfqs_ProjectRfqId",
                        column: x => x.ProjectRfqId,
                        principalTable: "ProjectRfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectQuoteRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectQuoteId = table.Column<int>(type: "integer", nullable: false),
                    RevisionLabel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    VersionStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SubmittedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidityDays = table.Column<int>(type: "integer", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    TargetMarginPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    EstimatedMarginPct = table.Column<decimal>(type: "numeric(9,4)", nullable: true),
                    QuotedLeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    QuotedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Assumptions = table.Column<string>(type: "text", nullable: true),
                    Inclusions = table.Column<string>(type: "text", nullable: true),
                    Exclusions = table.Column<string>(type: "text", nullable: true),
                    Exceptions = table.Column<string>(type: "text", nullable: true),
                    CommercialNotes = table.Column<string>(type: "text", nullable: true),
                    TechnicalNotes = table.Column<string>(type: "text", nullable: true),
                    CustomerFacingNotes = table.Column<string>(type: "text", nullable: true),
                    InternalNotes = table.Column<string>(type: "text", nullable: true),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ApprovedById = table.Column<int>(type: "integer", nullable: true),
                    ApprovedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSnapshotLocked = table.Column<bool>(type: "boolean", nullable: false),
                    SnapshotLockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceEstimateSnapshotId = table.Column<int>(type: "integer", nullable: true),
                    ScopeSnapshot = table.Column<string>(type: "text", nullable: true),
                    ConvertedToBaseline = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectQuoteRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectQuoteRevisions_ProjectQuotes_ProjectQuoteId",
                        column: x => x.ProjectQuoteId,
                        principalTable: "ProjectQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectQuoteRevisions_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectQuoteLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectQuoteRevisionId = table.Column<int>(type: "integer", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    PartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ExtendedPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectQuoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectQuoteLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectQuoteLines_ProjectQuoteRevisions_ProjectQuoteRevisio~",
                        column: x => x.ProjectQuoteRevisionId,
                        principalTable: "ProjectQuoteRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectquotelines_item",
                table: "ProjectQuoteLines",
                column: "ItemId",
                filter: "\"ItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_projectquotelines_revision_lineno",
                table: "ProjectQuoteLines",
                columns: new[] { "ProjectQuoteRevisionId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectQuoteRevisions_ApprovedById",
                table: "ProjectQuoteRevisions",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "ix_projectquoterevisions_versionstatus",
                table: "ProjectQuoteRevisions",
                column: "VersionStatus");

            migrationBuilder.CreateIndex(
                name: "ux_projectquoterevisions_quote_number",
                table: "ProjectQuoteRevisions",
                columns: new[] { "ProjectQuoteId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectquotes_customerproject",
                table: "ProjectQuotes",
                column: "CustomerProjectId");

            migrationBuilder.CreateIndex(
                name: "ix_projectquotes_rfq",
                table: "ProjectQuotes",
                column: "ProjectRfqId",
                filter: "\"ProjectRfqId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectquotes_status",
                table: "ProjectQuotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_projectquotes_company_quotenumber",
                table: "ProjectQuotes",
                columns: new[] { "CompanyId", "QuoteNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectrfqs_customerproject",
                table: "ProjectRfqs",
                column: "CustomerProjectId");

            migrationBuilder.CreateIndex(
                name: "ix_projectrfqs_status",
                table: "ProjectRfqs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_projectrfqs_company_rfqnumber",
                table: "ProjectRfqs",
                columns: new[] { "CompanyId", "RfqNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectQuoteLines");

            migrationBuilder.DropTable(
                name: "ProjectQuoteRevisions");

            migrationBuilder.DropTable(
                name: "ProjectQuotes");

            migrationBuilder.DropTable(
                name: "ProjectRfqs");
        }
    }
}
