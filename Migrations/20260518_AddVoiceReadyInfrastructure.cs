using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-014 / Sprint 4 PR #1 — Foundational voice-ready infrastructure.
    //
    // Tables created (2):
    //   - VoiceSessions     — Sprint 5 multi-turn conversation state
    //                         (Postgres-backed, NOT ASP.NET Session)
    //   - IdempotencyKeys   — Stripe-pattern (UserId, Key) UNIQUE
    //                         dedup, 24h TTL
    //
    // Columns added to existing AuditLogs (7 AI columns, mirrors
    // Microsoft Purview CopilotInteraction schema):
    //   - ActorKind          smallint enum (User=0, AiOnBehalfOf=1, System=2)
    //   - OnBehalfOfUserId   int? — human when ActorKind=AiOnBehalfOf
    //   - AiSessionId        uuid? — multi-turn correlation
    //   - AiCommandText      text? — raw natural-language utterance
    //   - AiModelVersion     varchar(64)?
    //   - AiToolName         varchar(128)?
    //   - AiConfidence       numeric(4,3)?
    //
    // NULL defaults so existing AuditService.LogAsync calls keep
    // working unchanged. AI-mediated actions will populate these.
    //
    // Reference: ADR-014 §"Decisions" D3, D4, D8.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260518_AddVoiceReadyInfrastructure")]
    public partial class AddVoiceReadyInfrastructure : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1) VoiceSessions ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""VoiceSessions"" (
                    ""Id""           uuid        PRIMARY KEY,
                    ""TenantId""     integer     NOT NULL,
                    ""UserId""       integer     NOT NULL,
                    ""StartedAt""    timestamptz NOT NULL DEFAULT now(),
                    ""LastTurnAt""   timestamptz NOT NULL DEFAULT now(),
                    ""StateJson""    jsonb       NOT NULL DEFAULT '{}'::jsonb,
                    ""ExpiresAt""    timestamptz NOT NULL DEFAULT now() + INTERVAL '4 hours'
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_VoiceSessions_TenantId_UserId_LastTurnAt""
                ON ""VoiceSessions"" (""TenantId"", ""UserId"", ""LastTurnAt"" DESC);
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_VoiceSessions_ExpiresAt""
                ON ""VoiceSessions"" (""ExpiresAt"");
            ");

            // ---------- 2) IdempotencyKeys ----------
            // Composite primary key (UserId, Key) enforced at table level.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""IdempotencyKeys"" (
                    ""UserId""          integer     NOT NULL,
                    ""Key""             uuid        NOT NULL,
                    ""RequestHash""     bytea       NOT NULL,
                    ""ResponseStatus""  integer     NULL,
                    ""ResponseBody""    jsonb       NULL,
                    ""LockedAt""        timestamptz NULL,
                    ""CompletedAt""     timestamptz NULL,
                    ""ExpiresAt""       timestamptz NOT NULL DEFAULT now() + INTERVAL '24 hours',
                    PRIMARY KEY (""UserId"", ""Key"")
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_IdempotencyKeys_ExpiresAt""
                ON ""IdempotencyKeys"" (""ExpiresAt"");
            ");

            // ---------- 3) AuditLogs AI columns ----------
            migrationBuilder.Sql(@"
                ALTER TABLE ""AuditLogs""
                ADD COLUMN IF NOT EXISTS ""ActorKind""         smallint     NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS ""OnBehalfOfUserId""  integer      NULL,
                ADD COLUMN IF NOT EXISTS ""AiSessionId""       uuid         NULL,
                ADD COLUMN IF NOT EXISTS ""AiCommandText""     text         NULL,
                ADD COLUMN IF NOT EXISTS ""AiModelVersion""    varchar(64)  NULL,
                ADD COLUMN IF NOT EXISTS ""AiToolName""        varchar(128) NULL,
                ADD COLUMN IF NOT EXISTS ""AiConfidence""      numeric(4,3) NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_ActorKind""
                ON ""AuditLogs"" (""ActorKind"");
            ");

            // Partial index — only the rows that actually have an AI
            // session ID get indexed. Most rows are direct-user actions
            // with NULL session ID, so this stays small.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_AuditLogs_AiSessionId""
                ON ""AuditLogs"" (""AiSessionId"")
                WHERE ""AiSessionId"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 3) AuditLogs rollback
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AuditLogs_AiSessionId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AuditLogs_ActorKind"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""AuditLogs""
                DROP COLUMN IF EXISTS ""AiConfidence"",
                DROP COLUMN IF EXISTS ""AiToolName"",
                DROP COLUMN IF EXISTS ""AiModelVersion"",
                DROP COLUMN IF EXISTS ""AiCommandText"",
                DROP COLUMN IF EXISTS ""AiSessionId"",
                DROP COLUMN IF EXISTS ""OnBehalfOfUserId"",
                DROP COLUMN IF EXISTS ""ActorKind"";
            ");

            // 2) IdempotencyKeys
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_IdempotencyKeys_ExpiresAt"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""IdempotencyKeys"";");

            // 1) VoiceSessions
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_VoiceSessions_ExpiresAt"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_VoiceSessions_TenantId_UserId_LastTurnAt"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""VoiceSessions"";");
        }
    }
}
