using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B11_R3_8_OperationCapabilityRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationCapabilityRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    RoutingOperationId = table.Column<int>(type: "integer", nullable: false),
                    CapabilityId = table.Column<int>(type: "integer", nullable: false),
                    ToolId = table.Column<int>(type: "integer", nullable: true),
                    RequirementType = table.Column<int>(type: "integer", nullable: false),
                    MinProficiency = table.Column<int>(type: "integer", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    QuantityRequired = table.Column<int>(type: "integer", nullable: true),
                    RequiredEnvelopeMin = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    RequiredEnvelopeMax = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationCapabilityRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationCapabilityRequirements_Capabilities_CapabilityId",
                        column: x => x.CapabilityId,
                        principalTable: "Capabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationCapabilityRequirements_RoutingOperations_RoutingOp~",
                        column: x => x.RoutingOperationId,
                        principalTable: "RoutingOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationCapabilityRequirements_Tools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "Tools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationCapabilityRequirements_CapabilityId",
                table: "OperationCapabilityRequirements",
                column: "CapabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationCapabilityRequirements_CompanyId",
                table: "OperationCapabilityRequirements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationCapabilityRequirements_RoutingOperationId",
                table: "OperationCapabilityRequirements",
                column: "RoutingOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationCapabilityRequirements_Tool_Partial",
                table: "OperationCapabilityRequirements",
                column: "ToolId",
                filter: "\"ToolId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_OperationCapabilityRequirements_Op_Capability_Type",
                table: "OperationCapabilityRequirements",
                columns: new[] { "RoutingOperationId", "CapabilityId", "RequirementType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationCapabilityRequirements");
        }
    }
}
