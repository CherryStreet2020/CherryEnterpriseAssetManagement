using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePaymentVoidColumns_PR336 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContraJournalEntryId",
                table: "InvoicePayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "InvoicePayments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "InvoicePayments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "InvoicePayments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContraJournalEntryId",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "InvoicePayments");
        }
    }
}
