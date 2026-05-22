using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [Migration("20260522_AddChainOfCustodyGraph")]
    // Sprint 12D PR #2 / ADR-022 — chain-of-custody graph layer (virtual Apache AGE).
    //
    // Two regular Postgres tables traversed via recursive CTE. No extension
    // dependency — Replit's managed Postgres handles this on Day 0. The Q3
    // 2026 migration to real Apache AGE per ADR-022 D7 swaps the storage
    // backend only; the IChainOfCustodyService interface and these table
    // shapes stay.
    //
    // Tables created (2):
    //   - ChainNodes  — polymorphic graph node, (NodeType, EntityId) is the
    //                   business key. NodeType covers PurchaseOrder, Receipt,
    //                   IQC, Cert, Heat, MaterialMaster, Vendor, Carrier,
    //                   WorkOrder, Invoice, GLEntry, etc.
    //   - ChainEdges  — typed directional edge between two ChainNodes.
    //                   EdgeType examples: RECEIVED_AT, INSPECTED_BY,
    //                   CERTIFIED_BY, MELTED_FROM, SUPPLIED_BY, CARRIED_BY,
    //                   CONSUMED_BY, APPROVED_BY, POSTED_TO.
    //
    // Indexes:
    //   - ix_chainnodes_entity  — UNIQUE (NodeType, EntityId, TenantId)
    //   - ix_chainnodes_tenant  — RLS-aware lookup
    //   - ix_chainedges_from    — outbound traversal (FromNodeId, EdgeType)
    //   - ix_chainedges_to      — inbound traversal (ToNodeId, EdgeType)
    //
    // RLS — same template as Embeddings (TenantId = 0 OR app.tenant_id).
    //
    // Idempotent: CREATE TABLE / INDEX / POLICY all use IF NOT EXISTS or
    // DROP-and-recreate so the migration is safe to replay.
    public partial class AddChainOfCustodyGraph : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ============================================================
            // ChainNodes
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ChainNodes"" (
                    ""Id""        BIGSERIAL    PRIMARY KEY,
                    ""NodeType""  VARCHAR(40)  NOT NULL,
                    ""EntityId""  BIGINT       NOT NULL,
                    ""TenantId""  INTEGER      NOT NULL,
                    ""Label""     TEXT         NOT NULL,
                    ""Metadata""  JSONB        NULL,
                    ""CreatedAt"" TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
            ");

            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_chainnodes_entity
                ON ""ChainNodes"" (""NodeType"", ""EntityId"", ""TenantId"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_chainnodes_tenant
                ON ""ChainNodes"" (""TenantId"");
            ");

            // RLS — same shape as Embeddings (ADR-020 §D6 + ADR-022 §D6).
            mb.Sql(@"ALTER TABLE ""ChainNodes"" ENABLE ROW LEVEL SECURITY;");
            mb.Sql(@"DROP POLICY IF EXISTS chainnodes_tenant_isolation ON ""ChainNodes"";");
            mb.Sql(@"
                CREATE POLICY chainnodes_tenant_isolation ON ""ChainNodes""
                USING (
                    ""TenantId"" = 0
                    OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::int
                );
            ");

            // ============================================================
            // ChainEdges
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ChainEdges"" (
                    ""Id""         BIGSERIAL    PRIMARY KEY,
                    ""FromNodeId"" BIGINT       NOT NULL REFERENCES ""ChainNodes"" (""Id"") ON DELETE CASCADE,
                    ""ToNodeId""   BIGINT       NOT NULL REFERENCES ""ChainNodes"" (""Id"") ON DELETE CASCADE,
                    ""EdgeType""   VARCHAR(40)  NOT NULL,
                    ""TenantId""   INTEGER      NOT NULL,
                    ""Metadata""   JSONB        NULL,
                    ""CreatedAt""  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
            ");

            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_chainedges_from
                ON ""ChainEdges"" (""FromNodeId"", ""EdgeType"");
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_chainedges_to
                ON ""ChainEdges"" (""ToNodeId"", ""EdgeType"");
            ");

            mb.Sql(@"ALTER TABLE ""ChainEdges"" ENABLE ROW LEVEL SECURITY;");
            mb.Sql(@"DROP POLICY IF EXISTS chainedges_tenant_isolation ON ""ChainEdges"";");
            mb.Sql(@"
                CREATE POLICY chainedges_tenant_isolation ON ""ChainEdges""
                USING (
                    ""TenantId"" = 0
                    OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::int
                );
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"DROP TABLE IF EXISTS ""ChainEdges"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""ChainNodes"" CASCADE;");
        }
    }
}
