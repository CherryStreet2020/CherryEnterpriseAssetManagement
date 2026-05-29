using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B11_R3_7_CapabilityModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Capabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSpecialProcess = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresQualification = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultQualificationValidityMonths = table.Column<int>(type: "integer", nullable: true),
                    IsParameterized = table.Column<bool>(type: "boolean", nullable: false),
                    EnvelopeUom = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    EnvelopeMin = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    EnvelopeMax = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GoverningStandard = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Capabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceCapabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    ProductionResourceId = table.Column<int>(type: "integer", nullable: false),
                    CapabilityId = table.Column<int>(type: "integer", nullable: false),
                    Proficiency = table.Column<int>(type: "integer", nullable: false),
                    QualifiedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QualifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CertificateReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EnvelopeValue = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceCapabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceCapabilities_Capabilities_CapabilityId",
                        column: x => x.CapabilityId,
                        principalTable: "Capabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceCapabilities_ProductionResources_ProductionResource~",
                        column: x => x.ProductionResourceId,
                        principalTable: "ProductionResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Capabilities_Category",
                table: "Capabilities",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Capabilities_CompanyId",
                table: "Capabilities",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UX_Capabilities_Company_Code",
                table: "Capabilities",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCapabilities_CapabilityId",
                table: "ResourceCapabilities",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCapabilities_CompanyId",
                table: "ResourceCapabilities",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UX_ResourceCapabilities_Resource_Capability",
                table: "ResourceCapabilities",
                columns: new[] { "ProductionResourceId", "CapabilityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceCapabilities");

            migrationBuilder.DropTable(
                name: "Capabilities");
        }
    }
}
