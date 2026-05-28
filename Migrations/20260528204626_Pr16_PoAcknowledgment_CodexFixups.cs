using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Pr16_PoAcknowledgment_CodexFixups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_POAcknowledgments_PurchaseOrderId",
                table: "POAcknowledgments");

            migrationBuilder.DropIndex(
                name: "IX_POAcknowledgments_PurchaseOrderId_IsCurrent",
                table: "POAcknowledgments");

            migrationBuilder.CreateIndex(
                name: "UX_POAcknowledgments_PurchaseOrderId_IsCurrent",
                table: "POAcknowledgments",
                column: "PurchaseOrderId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_POAcknowledgments_PurchaseOrderId_IsCurrent",
                table: "POAcknowledgments");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgments_PurchaseOrderId",
                table: "POAcknowledgments",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_POAcknowledgments_PurchaseOrderId_IsCurrent",
                table: "POAcknowledgments",
                columns: new[] { "PurchaseOrderId", "IsCurrent" });
        }
    }
}
