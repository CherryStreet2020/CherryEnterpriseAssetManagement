using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Adds PayloadVersion column to OutboxEvents to carry the strongly-typed
    // IDomainEvent.Version of each enqueued payload. Phase 1 of the
    // typed-outbox-payloads work — see docs/design/OUTBOX_TYPED_PAYLOADS.md.
    //
    // Schema-only and additive: nullable column, no backfill. Existing rows
    // keep PayloadVersion = NULL; the dispatcher and envelope builder
    // interpret NULL as 1 (the implicit V1 used by every legacy payload),
    // so already-queued events dispatch identically. No partner-facing
    // wire change beyond the additive `payloadVersion` field on the
    // envelope, which subscribers MUST tolerate per the existing
    // schemaVersion-1.0 contract.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260508000000_AddPayloadVersionToOutboxEvents")]
    public partial class AddPayloadVersionToOutboxEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""OutboxEvents""
                ADD COLUMN IF NOT EXISTS ""PayloadVersion"" integer NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""OutboxEvents""
                DROP COLUMN IF EXISTS ""PayloadVersion"";
            ");
        }
    }
}
