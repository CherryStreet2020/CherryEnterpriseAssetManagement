# ADR-020 — Postgres-as-AI-Native-OS Database Architecture

**Status:** Proposed — awaiting Dean sign-off
**Date:** 2026-05-19
**Author:** Architecture
**Supersedes:** N/A (first DB-architecture ADR)
**Builds on:** ADR-011 (Industrial Sensor Data — TimescaleDB), ADR-014 (Voice-AI readiness), ADR-015 (Industry-Agnostic Receipt Schema — jsonb), ADR-016 (Receiving Control Center voice tools)
**Foreshadows:** ADR-021 (Embedding pipeline + model choice), ADR-022 (Apache AGE graph schemas for chain-of-custody + BOM + APS-deps), ADR-023 (Multi-modal embeddings for mill certs + shop photos)
**Project memory:** [[project_database_direction_2026_05_19]]

---

## Question

Sprint 12A closed the Receiving Control Center. The Voice MVP shipped today (PRs #254-#258). The next 2-3 sprints will demand:

- **Semantic voice intent classification** — "find me steel plates near heat number H-12345" needs vector similarity, not keyword regex
- **Graph reasoning for the AI-APS pitch to Joe (EVS) on June 3** — "show me why this job is late" is a graph traversal across operations + resources + dependencies
- **Hybrid search across audit logs + voice transcripts + mill cert OCR text** — for Shadi (ABS) and Joe (EVS) to ask natural-language questions across history
- **Multi-modal lookup of mill certs + shop floor photos** — Voyage `multimodal-3` puts image + text in the same vector space

**The question:** what database architecture supports all five — relational, vector, graph, time-series, full-text — without forcing us into a multi-database operations nightmare that breaks voice UX with cross-store latency?

Two architectural paths are open:

1. **Best-of-breed-per-workload (2022 consensus)** — Postgres for OLTP + Pinecone for vectors + Neo4j for graphs + ElasticSearch for full-text. Each component dominates its category in isolation; the cost is operational complexity, cross-store sync, billing fragmentation, latency at query-time, and the inability to write a single SQL that joins the relational, vector, and graph results an AI agent needs.

2. **Converged Postgres (2025/2026 consensus)** — One Postgres instance with extensions: `pgvector` + `pgvectorscale` for vectors, `Apache AGE` for graphs, `TimescaleDB` (already have) for time-series, `ParadeDB pg_search` for BM25. Single backup, single RLS plane, single EF migration pipeline, single transaction boundary. The cost is being slightly behind specialized vendors on the cutting edge of each workload; the gain is operational simplicity and the ability to write one SQL that does what an AI tool call needs.

We pick **(2). Converged Postgres.** Rationale + decisions below.

---

## State of practice (research-validated)

Research pass 2026-05-19 surveyed the public state of vector + graph + hybrid-search ecosystems as of May 2026:

**Vector inside Postgres:**
- **pgvector 0.7+** ships `halfvec` (16-bit floats — 50% storage win, negligible recall loss vs `vector(N)`) and `sparsevec`. HNSW + IVFFlat indexes are production-grade for ~10M vectors.
- **pgvectorscale** (Timescale, Apache 2.0, late 2024) ships **StreamingDiskANN** — disk-resident ANN that outperforms HNSW by 10-50× at billion-vector scale while staying memory-efficient. Benchmarked against Pinecone p1 + Weaviate HNSW; pgvectorscale wins on cost-normalized recall@10.
- **pgai** (Timescale, Apache 2.0) — embed + retrieve + LLM-call inside Postgres SQL. Calls OpenAI / Voyage / Cohere / Ollama from a SQL function. Eliminates the "embed pipeline" as a separate service.

**Graph inside Postgres:**
- **Apache AGE 1.5+** — OpenCypher inside Postgres. Cypher queries return as Postgres result sets; can be embedded in EF `FromSqlInterpolated`. Mature for production graphs up to ~100M edges; beyond that, dedicated graph DBs (Neo4j Enterprise) still win.

**Full-text inside Postgres:**
- **ParadeDB pg_search** — BM25 ranking + multi-field search + Tantivy-backed indexes. Replaces ElasticSearch for OLTP-shaped corpora. Series A funded 2025; production-grade.
- Built-in `tsvector` / `tsquery` still works for simple cases; ParadeDB only needed when ranking quality matters.

**Time-series inside Postgres:**
- **TimescaleDB Apache** (already deployed) — hypertables + continuous aggregates work with regular MATERIALIZED VIEWs (no TSL `add_*_policy` helpers — see `feedback_timescaledb_apache_vs_tsl`).

**Industry consolidation signals (2024-2026):**
- Supabase + pgvector — crossed 100K production deployments mid-2025.
- Neon + pgvector — first-class on every new instance.
- Timescale pivoted from pure time-series to "vector + AI in Postgres" with pgai + pgvectorscale.
- ParadeDB raised Series A explicitly to make Postgres the ElasticSearch killer.
- AWS RDS, Azure Database for PostgreSQL, GCP Cloud SQL all ship pgvector by default.

**The dissenting case (why some still pick Pinecone / Neo4j):**
- True billion-vector workloads with sub-50ms p99 SLAs at sustained 10K QPS — Pinecone still wins on raw throughput.
- True 10B+ edge graph workloads with complex multi-hop Cypher under low latency — Neo4j Enterprise still wins.
- Neither describes CherryAI at v1 launch (3 customers, <10M operational rows total, <100M embedding rows at maximum).

**Reference architectures that match (2025-2026):**
- **Supabase's "single-database AI app" pattern** — relational + vector + RLS multi-tenant in one Postgres.
- **Timescale's "Postgres + pgai + pgvectorscale" pattern** — explicitly marketed for AI-native operational apps.
- **Akari Health, Builder.io, Posthog, ShipShape AI** — public case studies of production AI apps converging on Postgres.

---

## Decisions

### D1 — Postgres-as-AI-Native-OS — single converged database

CherryAI runs on **one Postgres 16+ instance** (today: Replit-managed; later: a hardened production tier — Neon or self-managed on a tier-1 cloud) with the following extensions enabled:

| Extension | Workload | Source | Status today |
| --- | --- | --- | --- |
| `vector` (pgvector) | Vector storage + ANN | Apache 2.0 | **To add** |
| `vectorscale` (pgvectorscale) | StreamingDiskANN for vectors at scale | Apache 2.0 (Timescale) | **To add** |
| `ai` (pgai) | In-DB embed + LLM calls | Apache 2.0 (Timescale) | **To add** |
| `age` (Apache AGE) | Cypher graph queries | Apache 2.0 | **To add** |
| `pg_search` (ParadeDB) | BM25 full-text search | Apache 2.0 (defer to Phase 3) | **To add** |
| `timescaledb` (Apache) | Hypertables, continuous aggs | Apache 2.0 | **Have** |
| `pgcrypto`, `pg_stat_statements`, `uuid-ossp` | Standard | PG core | **Have** |

**No second database.** No Pinecone, no Weaviate, no Qdrant, no Neo4j, no ElasticSearch, no MongoDB, no DocumentDB. Every AI workload either lives in Postgres or is rewritten to.

**Why single-database matters specifically for voice/AI:** the voice tool loop is `mic → STT → intent → SQL+vector+graph join → narrate`. Every cross-database hop adds RTT to the narrate step and breaks the "feels instant" UX that's the whole point of voice. SAP / Plex / NetSuite cannot deliver this because their data is sharded across modules with cross-system ETL between them.

### D2 — Vector layer architecture

**Storage shape.** A single `embeddings` table, polymorphic across entity types, keyed by `(EntityType, EntityId, ModelVersion)`:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS vectorscale;

CREATE TABLE "Embeddings" (
    "Id"           BIGSERIAL PRIMARY KEY,
    "EntityType"   VARCHAR(64)  NOT NULL,         -- e.g. "Item", "ReceiptProfile", "WorkOrder"
    "EntityId"     BIGINT       NOT NULL,         -- FK-shaped; no constraint (entity may be soft-deleted)
    "TenantId"     INT          NOT NULL,         -- RLS partition key
    "ModelVersion" VARCHAR(64)  NOT NULL,         -- e.g. "voyage-3-large/v1", "text-embedding-3-large/v1"
    "ContentHash"  CHAR(64)     NOT NULL,         -- SHA-256 of source text; re-embed only on hash change
    "Embedding"    halfvec(1536) NOT NULL,        -- 16-bit floats; 50% storage win vs vector(1536)
    "SourceText"   TEXT,                          -- For debug + cache invalidation
    "CreatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE("EntityType", "EntityId", "ModelVersion")
);

-- StreamingDiskANN (pgvectorscale) — better than HNSW at scale
CREATE INDEX embeddings_ann_idx
    ON "Embeddings"
    USING diskann ("Embedding" halfvec_cosine_ops);

-- RLS multi-tenant
ALTER TABLE "Embeddings" ENABLE ROW LEVEL SECURITY;
CREATE POLICY embeddings_tenant_isolation ON "Embeddings"
    USING ("TenantId" = current_setting('app.tenant_id', true)::int);
```

**Embedding model choice (locked here, full ADR-021 to follow):**
- **Default text model:** Voyage `voyage-3-large` (1024 dims, halfvec for storage, top-tier MTEB benchmark, $0.06 / M tokens). OpenAI `text-embedding-3-large` (3072 dims, truncate to 1536) is the runner-up.
- **Multi-modal model (Phase 2):** Voyage `multimodal-3` for mill cert images + text in shared space.
- **Local fallback (air-gapped customers):** Ollama running `nomic-embed-text` (768 dims). Stored in a separate `Embeddings_OnPrem` column-set when needed.

**What gets embedded.** High-value, low-volume, high-semantic-density first:

| Entity | Source field(s) | Why |
| --- | --- | --- |
| `ReceiptProfile` | Code + Description + Schema fields | Voice intent ("steel receipt", "pharma receipt") routes here |
| `Item` | ItemNumber + Description + Notes | "Find similar items" search; voice lookup; orphan match |
| `Vendor` | Name + Description + tags | Voice vendor lookup; orphan AI match |
| `WorkOrder` | Title + Description + classification satellites | Project Management cockpit (Shadi's #1) "find similar past work" |
| `AuditLog.AiCommandText` | Voice utterances | "Find every conversation where Joe asked about ITAR" |
| `KbArticle` (future) | Help body | Voice-driven help |

**What does NOT get embedded yet:** high-cardinality transactional rows (StockReceipts, JournalEntries, sensor events). Phase 4 or later, with careful index sizing.

**Embedding pipeline.** Two-mode:
- **Mode A — `pgai` in-DB (default).** `SELECT ai.embed('voyage-3-large', "Description") FROM ...` — Postgres calls Voyage API directly. Simplest, lowest-latency, lowest-operational-surface.
- **Mode B — .NET background worker.** For batch-reembedding (model swaps, bulk seeds) or air-gapped deployments. Reads change-data-capture from a `pending_embeddings` queue table.

Default to A; switch to B only when batch volumes exceed pgai's serial throughput.

**Hybrid search query.** Single SQL fuses BM25 (Phase 3 — ParadeDB) + vector cosine via Reciprocal Rank Fusion:

```sql
WITH vector_hits AS (
    SELECT "EntityType", "EntityId",
           1.0 / (60 + ROW_NUMBER() OVER (ORDER BY "Embedding" <=> $1)) AS rrf_score
    FROM "Embeddings"
    WHERE "EntityType" = $2
    ORDER BY "Embedding" <=> $1
    LIMIT 50
),
bm25_hits AS (
    SELECT "EntityType", "EntityId",
           1.0 / (60 + ROW_NUMBER() OVER (ORDER BY paradedb.score(id) DESC)) AS rrf_score
    FROM "Items" @@@ $3
    LIMIT 50
)
SELECT "EntityType", "EntityId", SUM(rrf_score) AS combined_score
FROM (SELECT * FROM vector_hits UNION ALL SELECT * FROM bm25_hits) u
GROUP BY "EntityType", "EntityId"
ORDER BY combined_score DESC
LIMIT 10;
```

### D3 — Graph layer architecture (Apache AGE)

**Three graph schemas defined on the same Postgres instance (full schemas land in ADR-022):**

1. **`chain_of_custody`** — Nodes: `StockReceipt`, `Nest`, `Remnant`, `CutListLine`, `ProductionBatch`, `Shipment`. Edges: `cut_from`, `produced`, `shipped_as`, `quarantined_by`. Powers `IReceiptVoiceTools.TraceChainOfCustodyAsync` (stub today; real implementation here).

2. **`bom`** — Nodes: `Item`, `MaterialStructure`, `Bom`, `BomLine`. Edges: `parent_of`, `consumes`, `revises`. Powers BOM explosion + where-used queries. Voice: "what items use heat number H-12345?"

3. **`aps_dependencies`** — Nodes: `Operation`, `Resource`, `Skill`, `Constraint`. Edges: `precedes`, `requires`, `competes_with`. **This is the EVS/Joe demo graph.** Voice: "show me why job X is late" → Cypher path traversal narrated by the LLM.

**EF Core integration.** AGE queries flow through `FromSqlInterpolated`:

```csharp
var late = await _db.Database
    .SqlQueryRaw<LateReasonRow>(
        @"SELECT * FROM ag_catalog.cypher('aps_dependencies', $$
            MATCH (op:Operation {id: $1})-[:precedes*1..5]->(blocker:Operation)
            WHERE blocker.completed_at IS NULL
            RETURN blocker.id, blocker.name, blocker.expected_finish
          $$) AS r(blocker_id BIGINT, blocker_name TEXT, expected_finish TIMESTAMPTZ)",
        operationId)
    .ToListAsync();
```

**Sync between relational tables and graph.** Two patterns coexist:
- **View-backed graph (default).** AGE graph is computed from relational tables via a refresh function. Stale-tolerant; refresh on demand or every N minutes.
- **Trigger-backed graph (for hot paths).** Postgres triggers maintain AGE graph rows on INSERT/UPDATE/DELETE to the source tables. Higher consistency, higher write cost.

Pick view-backed for v1; promote individual graphs to trigger-backed if measurement shows lag impacting voice UX.

### D4 — Time-series layer (no change to TimescaleDB Apache)

Existing decision in ADR-011 holds. Sensor events + KPI rollups stay on TimescaleDB Apache hypertables with regular MATERIALIZED VIEWs. The TSL paywall constraint stays captured in [[feedback_timescaledb_apache_vs_tsl]].

Future option: **continuous aggregates via Timescale Cloud** if/when we move off Replit and Apache-only restrictions matter for refresh policies. ADR-023+ scope.

### D5 — Full-text layer (Phase 3 — ParadeDB pg_search)

When BM25 ranking quality becomes the limiting factor on voice search (Sprint 13-14, projected), add ParadeDB's `pg_search` extension. Index:
- `Items.Description + Notes`
- `WorkOrder.Title + Description + Closeout notes`
- `AuditLog.AiCommandText`
- `KbArticle.Body` (when KB ships)
- `OcrText` columns on mill cert / packing slip / drawing attachments (when OCR ships)

Until then, Postgres-built-in `tsvector` covers basic cases.

### D6 — Multi-tenant via Row-Level Security (already in Sprint 12.5 plan)

Every table (including `Embeddings` per D2) has a `TenantId` column and an RLS policy keyed off `current_setting('app.tenant_id')`. The application sets this once per request from the authenticated user's `tenant_id` claim.

**No schema-per-tenant, no database-per-tenant** at v1. Single multi-tenant schema with RLS is the modern SaaS data architecture and what the rest of the industry has converged on (Supabase, Neon, PlanetScale, Stripe internal). Lower operational cost; backup/restore is one job not 1-per-customer.

Promotion to schema-per-tenant happens only if a regulated customer (ITAR, HIPAA-strict, FedRAMP) explicitly requires physical separation. Build for that on the day it's required; not before.

### D7 — Migration discipline

EF Core migrations are the single source of truth for ALL schema, including extension installations. The Phase 1 vector layer migration looks like:

```csharp
public partial class AddEmbeddingsAndPgvector : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
        mb.Sql("CREATE EXTENSION IF NOT EXISTS vectorscale;");
        mb.CreateTable("Embeddings", /* per D2 */);
        mb.Sql(@"CREATE INDEX embeddings_ann_idx ON ""Embeddings""
                 USING diskann (""Embedding"" halfvec_cosine_ops);");
        mb.Sql(@"ALTER TABLE ""Embeddings"" ENABLE ROW LEVEL SECURITY;");
        mb.Sql(@"CREATE POLICY embeddings_tenant_isolation ON ""Embeddings""
                 USING (""TenantId"" = current_setting('app.tenant_id', true)::int);");
    }
}
```

**Replit caveat.** Per [[feedback_timescaledb_apache_vs_tsl]], extensions must be in Postgres's `shared_preload_libraries` for some features. Confirm with Replit support pre-migration that `vector`, `vectorscale`, `age`, `pg_search` are loadable. If any aren't, that's a forcing function to move to Neon / self-managed faster than planned.

### D8 — Cost projections (sanity check, not commitment)

For three signed customers in Sprint 13 (ABS + EVS + FSC) at projected volumes:

| Cost driver | Phase 1 (vector) | Phase 2 (+graph) | Phase 4 (+full-text) |
| --- | --- | --- | --- |
| Postgres compute (Neon-class, hardened) | $200-400/mo | $300-500/mo | $400-600/mo |
| Embedding API (Voyage, projected) | $5-15/mo | $5-15/mo | $5-15/mo |
| LLM inference (voice intent + APS explain) | $50-200/mo | $100-300/mo | $100-300/mo |
| **Total estimated monthly infrastructure** | **~$300-600** | **~$450-800** | **~$550-900** |

Compare to multi-database alternative (Pinecone p1 + Neo4j AuraDB + ElasticSearch managed + Postgres):
- Pinecone p1 base: $70/mo
- Neo4j AuraDB Free runs out fast → Pro at $200+/mo
- ElasticSearch managed: $150-300/mo
- Postgres still: $200+/mo
- **Total: $620-770/mo PLUS the engineering cost of running 4 systems.**

Converged Postgres is at or below alternative cost while eliminating the operational complexity. The decision is dominant on both dimensions.

---

## Phase plan

| Phase | Sprint target | Scope | Blocking decision |
| --- | --- | --- | --- |
| **Phase 1 — Vector layer** | Right after Project Management cockpit for Shadi | pgvector + pgvectorscale + Embeddings table + embed pipeline (pgai or .NET worker) + embed ReceiptProfiles, Items, Vendors, WorkOrders | ADR-021 (embedding model + pipeline locked) |
| **Phase 2 — Graph layer** | BEFORE June 3 EVS pitch | Apache AGE + chain_of_custody + bom + aps_dependencies schemas. Real implementation of TraceChainOfCustodyAsync. AGE-backed "why is X late?" voice demo query. | ADR-022 (graph schemas locked) |
| **Phase 3 — Full-text layer** | Sprint 13-14 | ParadeDB pg_search + index Items + WorkOrder + AuditLog + (when ready) OcrText | None blocking |
| **Phase 4 — Multi-modal** | Sprint 14-15 | Voyage multimodal-3 embeddings on mill cert images + shop photos. Extend Embeddings table with image-source columns. | ADR-023 (multi-modal pipeline + storage) |
| **Phase 5 — Production hardening** | Sprint 15+ | Move off Replit to Neon / hardened-self-managed. Confirm extension availability. RLS multi-tenant verified live. Embedding-pipeline observability + cost dashboards. | ADR-024 (production infra) |

---

## Risks + mitigations

| Risk | Likelihood | Mitigation |
| --- | --- | --- |
| Replit doesn't support pgvectorscale / age / pg_search | Medium | Test on day 1 of Phase 1. If blocked, accelerate move to Neon (drop-in pg superset). |
| pgai's serial API-call throughput bottlenecks bulk embedding | Low | Mode B (.NET background worker) is the escape hatch; designed in from D2. |
| Apache AGE Cypher query performance regresses at scale | Low-Medium | View-backed graphs default; promote hot paths to trigger-backed only if measured. |
| Embedding model gets deprecated / pricing changes | Medium | `ModelVersion` column on Embeddings — re-embed pipeline is a single batch job. Vendor-portable design. |
| Single Postgres instance is a single point of failure | Medium | Standard Postgres HA at production (streaming replica + WAL archiving). Same risk as any single-DB architecture; not unique to this ADR. |
| jsonb attribute pass + vector ANN compete for same memory | Low | Index sizing review before Phase 1 ship. pgvectorscale's DiskANN is intentionally disk-resident to dodge this. |

---

## Why this is the right early-adopter bet

CherryAI's strategic positioning (`project_launch_scope_and_positioning`) is "modern, AI-native, vertically-aware operations platform that channels into nothing." The DB architecture is downstream of that positioning. Three things competitors literally cannot do without this architecture:

1. **Voice ask → SQL+vector+graph join in <300ms.** SAP / Plex / NetSuite need cross-store federation; we ship one query.
2. **"Explain why X happened" narratable by the LLM with full graph context.** Competitors either lack graph storage or have it siloed from the operational data. We have it in the same database as the transactions the LLM is reasoning about.
3. **Per-vertical semantic search via profile embeddings.** ABS asks in steel terminology, FSC asks in food-safety terminology, EVS asks in ITAR terminology — same SQL, different embedded vocabulary spaces. SAP can't add a profile without a 6-month implementation.

The competitive moat compounds: every embed + every graph edge written is a feature that takes a Tier-1 ERP a multi-quarter project to match.

---

## Decision

**Approved by:** _pending Dean sign-off_
**Phase 1 kicks off:** after Project Management cockpit for Shadi
**Phase 2 deadline:** before June 3 EVS pitch (demo data acceptable for v1)

---

## Cross-references

- [[project_database_direction_2026_05_19]] — the seeding research memo
- [[project_voice_ai_copilot_vision]] — the strategic vision this enables
- [[project_voice_mvp_shipped]] — what just shipped today; Phase 1 vector layer is the natural next AI step
- [[project_launch_scope_and_positioning]] — vertical-aware ERP positioning that needs semantic profile matching
- [[project_command_center_pattern]] — every Control Center wants hybrid search inside it
- [[feedback_timescaledb_apache_vs_tsl]] — Replit ships Apache; plan accordingly
- [[feedback_ef_fromsql_xmin_required]] — gotcha that affected the voice MVP; relevant when adding raw SQL graph queries
- ADR-011 — Industrial Sensor Data Architecture (TimescaleDB)
- ADR-014 — Phase F UI + Voice-AI Readiness
- ADR-015 — Industry-Agnostic Receipt Schema (jsonb)
- ADR-016 — Receiving Control Center voice tools

---

**Open questions for Dean before sign-off:**
1. **Migration timing.** Phase 1 right after Project Management cockpit (~1-2 weeks out), or accelerate to BEFORE the cockpit so voice intent classification graduates from keyword → vector immediately?
2. **Replit vs Neon decision date.** Confirm extension support on Replit by end of Phase 1; if any extension is blocked, the move to Neon happens as part of Phase 1 (not deferred to Phase 5).
3. **Embedding vendor.** Voyage (recommended) vs OpenAI vs Cohere vs Ollama-self-hosted. ADR-021 will lock; Dean may have a preference based on cost or vendor relationship.
