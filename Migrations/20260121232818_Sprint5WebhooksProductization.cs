using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5WebhooksProductization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "WebhookSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisabledAt",
                table: "WebhookSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabledReason",
                table: "WebhookSubscriptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailureCountLifetime",
                table: "WebhookSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxConsecutiveFailures",
                table: "WebhookSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SuccessCountLifetime",
                table: "WebhookSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PayloadSent",
                table: "WebhookDeliveryLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "DisabledReason",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "FailureCountLifetime",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "MaxConsecutiveFailures",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "SuccessCountLifetime",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "PayloadSent",
                table: "WebhookDeliveryLogs");
        }
    }
}
