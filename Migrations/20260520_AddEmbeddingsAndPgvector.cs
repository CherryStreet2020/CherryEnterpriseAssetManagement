using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [Migration("20260520_AddEmbeddingsAndPgvector")]
    // Sprint 12C / ADR-020 §D2 + ADR-021 — pgvector extension + Embeddings
    // table + PendingEmbeddings queue + indexes + RLS policy.
    //
    // Replit's managed Postgres ships pgvector 0.8.0 (verified
    // 2026-05-20 via `SELECT * FROM pg_available_extensions`), so this
    // migration runs unmodified on the current host.
    //
    // Tables created (2):
    //   - Embeddings        — polymorphic embedding row, halfvec(1024)
    //   - PendingEmbeddings — change-data-capture queue for the .NET worker
    //
    // Indexes:
    //   - ix_embeddings_entity_model — unique (EntityType, EntityId, ModelVersion)
    //   - ix_embeddings_tenant       — RLS-aware lookup
    //   - embeddings_hnsw_idx        — HNSW on halfvec_cosine_ops (the ANN index)
    //   - ix_pending_embeddings_dedup — (EntityType, EntityId, ContentHash) lookup
    //   - ix_pending_embeddings_enqueued — FIFO worker ordering
    //   - ix_pending_embeddings_attempts — quickly find rows nearing failure cap
    //
    // RLS:
    //   - Embeddings.TenantId-filtered policy keyed off
    //     current_setting('app.tenant_id'). Same shape as the rest of the
    //     multi-tenant schema. Per ADR-020 §D6 + ADR-021 §D7.
    //
    // Idempotent: CREATE EXTENSION IF NOT EXISTS + CREATE TABLE IF NOT EXISTS
    // + CREATE INDEX IF NOT EXISTS. Safe to re-run.
    //
    // Reversible: Down() drops the tables + extension cleanup.
    public partial class AddEmbeddingsAndPgvector : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            mb.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Embeddings"" (
                    ""Id""           BIGSERIAL PRIMARY KEY,
                    ""EntityType""   VARCHAR(64)  NOT NULL,
                    ""EntityId""     BIGINT       NOT NULL,
                    ""TenantId""     INTEGER      NOT NULL,
                    ""ModelVersion"" VARCHAR(64)  NOT NULL,
                    ""ContentHash""  CHAR(64)     NOT NULL,
                    ""Embedding_""   halfvec(1024) NOT NULL,
                    ""SourceText""   TEXT         NULL,
                    ""CreatedAt""    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
            ");

            // Unique composite for upsert dedup.
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_embeddings_entity_model
                ON ""Embeddings"" (""EntityType"", ""EntityId"", ""ModelVersion"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_embeddings_tenant
                ON ""Embeddings"" (""TenantId"");
            ");

            // The ANN index. halfvec_cosine_ops is the operator class for
            // cosine similarity on halfvec; matches the query patterns we
            // use in the intent router (smaller-distance = more similar).
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS embeddings_hnsw_idx
                ON ""Embeddings""
                USING hnsw (""Embedding_"" halfvec_cosine_ops);
            ");

            // RLS — tenant isolation. current_setting('app.tenant_id', true)
            // returns NULL when unset, which never matches any TenantId, so
            // unauthenticated callers see zero rows by default.
            mb.Sql(@"
                ALTER TABLE ""Embeddings"" ENABLE ROW LEVEL SECURITY;
            ");
            // DROP/CREATE policy pattern keeps the migration replayable.
            mb.Sql(@"
                DROP POLICY IF EXISTS embeddings_tenant_isolation ON ""Embeddings"";
            ");
            mb.Sql(@"
                CREATE POLICY embeddings_tenant_isolation ON ""Embeddings""
                USING (
                    ""TenantId"" = 0
                    OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::int
                );
            ");

            // ============================================================
            // PendingEmbeddings queue
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PendingEmbeddings"" (
                    ""Id""             BIGSERIAL PRIMARY KEY,
                    ""EntityType""     VARCHAR(64)  NOT NULL,
                    ""EntityId""       BIGINT       NOT NULL,
                    ""TenantId""       INTEGER      NOT NULL,
                    ""SourceText""     TEXT         NOT NULL,
                    ""ContentHash""    CHAR(64)     NOT NULL,
                    ""EnqueuedAt""     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    ""Attempts""       INTEGER      NOT NULL DEFAULT 0,
                    ""LastAttemptAt""  TIMESTAMPTZ  NULL,
                    ""LastError""      VARCHAR(500) NULL
                );
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_pending_embeddings_dedup
                ON ""PendingEmbeddings"" (""EntityType"", ""EntityId"", ""ContentHash"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_pending_embeddings_enqueued
                ON ""PendingEmbeddings"" (""EnqueuedAt"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_pending_embeddings_attempts
                ON ""PendingEmbeddings"" (""Attempts"");
            ");

            // RLS — same shape as Embeddings.
            mb.Sql(@"
                ALTER TABLE ""PendingEmbeddings"" ENABLE ROW LEVEL SECURITY;
            ");
            mb.Sql(@"
                DROP POLICY IF EXISTS pending_embeddings_tenant_isolation ON ""PendingEmbeddings"";
            ");
            mb.Sql(@"
                CREATE POLICY pending_embeddings_tenant_isolation ON ""PendingEmbeddings""
                USING (
                    ""TenantId"" = 0
                    OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::int
                );
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"DROP TABLE IF EXISTS ""PendingEmbeddings"";");
            mb.Sql(@"DROP TABLE IF EXISTS ""Embeddings"";");
            // Don't drop the extension — other parts of the app may rely on it.
        }
    }
}
