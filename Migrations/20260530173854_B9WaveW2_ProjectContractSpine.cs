using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B9WaveW2_ProjectContractSpine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ContractNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReviewRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReviewDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AwardedProjectQuoteId = table.Column<int>(type: "integer", nullable: true),
                    AwardedRevisionId = table.Column<int>(type: "integer", nullable: true),
                    AwardDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BaselineContractValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    LaunchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectContracts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectContracts_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectContractLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectContractId = table.Column<int>(type: "integer", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    ContractLineReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    PartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ExtendedPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    BaselineStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BaselineFinish = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectContractLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectContractLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectContractLines_ProjectContracts_ProjectContractId",
                        column: x => x.ProjectContractId,
                        principalTable: "ProjectContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCustomerPOs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CustomerProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectContractId = table.Column<int>(type: "integer", nullable: true),
                    CustomerPoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PoDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PoValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCustomerPOs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCustomerPOs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectCustomerPOs_CustomerProjects_CustomerProjectId",
                        column: x => x.CustomerProjectId,
                        principalTable: "CustomerProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCustomerPOs_ProjectContracts_ProjectContractId",
                        column: x => x.ProjectContractId,
                        principalTable: "ProjectContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projectcontractlines_item",
                table: "ProjectContractLines",
                column: "ItemId",
                filter: "\"ItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_projectcontractlines_contract_lineno",
                table: "ProjectContractLines",
                columns: new[] { "ProjectContractId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectcontracts_customerproject",
                table: "ProjectContracts",
                column: "CustomerProjectId");

            migrationBuilder.CreateIndex(
                name: "ix_projectcontracts_status",
                table: "ProjectContracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_projectcontracts_company_contractnumber",
                table: "ProjectContracts",
                columns: new[] { "CompanyId", "ContractNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projectcustomerpos_contract",
                table: "ProjectCustomerPOs",
                column: "ProjectContractId",
                filter: "\"ProjectContractId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_projectcustomerpos_customerproject",
                table: "ProjectCustomerPOs",
                column: "CustomerProjectId");

            migrationBuilder.CreateIndex(
                name: "ix_projectcustomerpos_status",
                table: "ProjectCustomerPOs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ux_projectcustomerpos_company_ponumber",
                table: "ProjectCustomerPOs",
                columns: new[] { "CompanyId", "CustomerPoNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectContractLines");

            migrationBuilder.DropTable(
                name: "ProjectCustomerPOs");

            migrationBuilder.DropTable(
                name: "ProjectContracts");
        }
    }
}
