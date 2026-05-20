# ADR-021 — Embedding Model + Pipeline

**Status:** Proposed — awaiting Dean sign-off
**Date:** 2026-05-20
**Author:** Architecture
**Supersedes:** N/A
**Builds on:** ADR-020 (Postgres-as-AI-Native-OS)
**Foreshadows:** ADR-022 (Apache AGE graph schemas), ADR-023 (multi-modal embeddings for mill certs)
**Project memory:** [[project_voice_mvp_shipped]] + [[project_database_direction_2026_05_19]] + [[project_roadmap_reshuffle_2026_05_19]]

---

## Question

ADR-020 D2 locked the vector layer architecture (single `Embeddings` table, halfvec column, RLS multi-tenant, two-mode pipeline). But three production-shape decisions were intentionally deferred to a sibling ADR:

1. **Which embedding model.** Voyage / OpenAI / Cohere / Ollama-self-hosted?
2. **Pipeline operational shape.** Default mode A (pgai in-DB) or mode B (.NET background worker)? Retry policy? Rate limit?
3. **Re-embed and migration policy.** When do we re-embed? How do we handle model-version transitions without breaking semantic search continuity?

Sprint 12C PR #1 implements the first concrete embed-write path. These decisions need to be locked before that code lands.

---

## State of practice (research-validated, May 2026)

**Embedding model landscape:**

| Model | Dims | $/M tokens | MTEB | Strengths | Caveats |
| --- | --- | --- | --- | --- | --- |
| Voyage `voyage-3-large` | 1024 | $0.06 | ~70.5 | Top-tier benchmark, cheapest, multimodal sibling | Smaller vendor; service maturity tradeoff vs OpenAI |
| Voyage `voyage-3` (lite) | 1024 | $0.02 | ~67.0 | Cost-optimal for high-volume | Lower benchmark; reserve for cold-tier embed |
| OpenAI `text-embedding-3-large` | 3072 → truncate 1024 | $0.13 | ~64.6 | Brand certainty, OpenAI ecosystem | 2× cost vs Voyage at equivalent quality |
| OpenAI `text-embedding-3-small` | 1536 → truncate 1024 | $0.02 | ~62.3 | Budget OpenAI tier | Quality gap |
| Cohere `embed-english-v3` | 1024 | $0.10 | ~64.5 | Pairs with Cohere Rerank for hybrid search | Vendor lock to Cohere's ecosystem |
| Cohere `embed-multilingual-v3` | 1024 | $0.10 | ~64.0 | Spanish/French Canadian support (Sprint 22 i18n target) | Same lock |
| Ollama `nomic-embed-text` | 768 | $0 | ~62.4 | Air-gapped capable (ITAR / on-prem) | You operate the GPU; lower quality |
| Ollama `mxbai-embed-large` | 1024 | $0 | ~64.7 | Free + 1024-dim matches Voyage | Same op burden |

**Pipeline patterns (validated):**

- **In-DB pipeline** (Timescale's pgai approach) — `SELECT ai.embed(...)` directly in SQL. Simple incremental writes; not great for batch.
- **External worker pipeline** (Anthropic/OpenAI cookbook pattern) — change-data-capture queue table + background worker. Decoupled, replayable, vendor-portable, parallelizable. The dominant pattern in production AI apps.
- **Hybrid** — external worker default; pgai for ad-hoc ops queries.

**Re-embed and migration policy patterns:**

- **Hash-based incremental** — SHA-256 of source text per row; skip re-embed if hash unchanged. Stripe's "idempotency by content" pattern adapted for embeddings.
- **Versioned column** — `ModelVersion` per row; gradual migration when changing models.
- **Dual-write window** — when migrating models, embed against both for N days, query against both, fade out old. Standard search-engine migration playbook.

---

## Decisions

### D1 — Voyage `voyage-3-large` is the default embedding model

Rationale ranked by impact:
1. **Top-tier MTEB benchmark at the lowest top-tier price** — best quality-per-dollar in the market as of May 2026.
2. **1024 native dimensions** — sized for HNSW index performance + reasonable storage (~2KB per row with halfvec).
3. **Multimodal upgrade path locked** — Voyage `multimodal-3` for mill cert images + shop photos shares the same vector space, the same API auth, and the same SDK shape. Phase 4 of ADR-020 (multi-modal) becomes a near-zero-friction extension.
4. **Vendor-portable column shape** — every other top vendor (OpenAI, Cohere, Mistral) offers 1024-dim variants. If Voyage fails or pricing changes, swap with `ModelVersion = "openai-3-large/v1"` and embed against the new model — same column, same index, same code.

**Specific config:**
- Model identifier in `Embeddings.ModelVersion`: `voyage-3-large/v1`
- Dimension: `halfvec(1024)`
- Input type: `document` for entity embeds, `query` for voice-query embeds (Voyage supports asymmetric embed types — improves retrieval ~3-5%)
- Truncation behavior: `truncate=true` (default) — silently truncates inputs over 32K tokens

### D2 — Column shape revision from ADR-020 §D2

ADR-020 specified `halfvec(1536)`. **Revised to `halfvec(1024)`** to match Voyage native dimension. Savings:
- 33% reduction in embedding storage
- Smaller HNSW index → faster query latency
- No truncation loss vs the previous "pad to 1536" plan

The `ModelVersion` column still does its job — future 3072-dim OpenAI models would land in a parallel column (`Embedding3072 halfvec(3072)`) or a sibling table; existing data is not affected.

### D3 — Pipeline: external .NET background worker (Mode B is default, Mode A deferred)

ADR-020 §D2 listed pgai (Mode A) as default with .NET worker (Mode B) as fallback. **Inverted: .NET worker is the default, pgai deferred to Phase 5.**

Why the flip:
- **pgai requires the Postgres host to enable the `ai` extension.** Sprint 12C is explicitly running on Replit-managed Postgres without a host migration (per Dean's 2026-05-19 evening call). Replit/Neon support for pgai is uncertain; the .NET worker has zero infrastructure dependencies and works today.
- **Bulk backfill is the harder problem.** Sprint 12C PR #1 needs to embed 12 ReceiptProfiles + ~5K items + ~500 vendors + ~10K work orders + audit log history. That's a batch job, which is the worker's strength.
- **Vendor portability.** The worker pattern works for any vendor (Voyage, OpenAI, Cohere, Ollama). pgai's vendor catalog is curated separately.
- **Observability.** Worker writes structured logs + metrics through the existing OpenTelemetry pipeline. pgai is opaque inside Postgres.

**Worker architecture:**

```csharp
public interface IEmbeddingBackfillService
{
    Task EnqueueAsync(string entityType, long entityId, string sourceText, CancellationToken ct);
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken ct);
}

// New table — Sprint 12C PR #1 migration
public class PendingEmbedding
{
    public long Id { get; set; }
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public int TenantId { get; set; }
    public string SourceText { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; } = 0;
    public DateTime? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
}
```

A hosted `IHostedService` polls the queue every 5 seconds, takes a batch (≤32 rows = Voyage's max batch), calls Voyage, upserts into `Embeddings`, deletes from queue.

**Incremental write trigger.** Services that mutate embeddable entities (ReceiptProfileService, ItemService, VendorService, WorkOrderService, voice endpoint) call `_embedQueue.EnqueueAsync(...)` after a successful save. Async fire-and-forget — write latency is unchanged for the user-facing operation.

**Bulk backfill.** A one-time admin endpoint `POST /_admin/embed/backfill` enqueues every existing row for embedded entity types. Runs to completion in the background.

### D4 — Retry + rate-limit policy

Voyage's rate limits (as of May 2026):
- Embed API: 2K requests/min, 30M tokens/min on free tier; 10× on paid.
- Per-request limit: 1K texts per call, 32K tokens per text.

**Worker policy:**
- Batch size: 32 texts per call (well under the 1K limit; keeps single-call latency under 500ms).
- Concurrent calls: 4 (16 calls/sec sustained; ~3.3% of the rate limit ceiling — leaves headroom for the voice endpoint).
- Retry on 429 (rate limit) or 5xx: exponential backoff 1s → 2s → 4s → 8s → 16s, max 5 attempts, then mark `LastError` and skip until manual retry.
- Retry on network errors (timeout, ConnectionRefused): same backoff.
- **Do not retry on 4xx (other than 429).** Surface to `LastError`, alert via OTel metric.

**Voice-path query embed:** Synchronous call to Voyage (no queue) with a 2-second timeout. If Voyage is down, voice falls back to keyword routing (graceful degradation — the existing intent classifier code still works).

### D5 — Hash-based re-embed policy

Every row in `Embeddings` carries `ContentHash` (SHA-256 of source text).

**Re-embed rules:**
- On entity save: compute hash of the source text. If different from `Embeddings.ContentHash`, enqueue re-embed. If same, no-op.
- On model swap (e.g. `voyage-3-large/v1` → `voyage-3-large/v2`): bulk backfill against the new model version; old version stays for a 7-day fade-out window.
- On entity soft-delete: leave the embedding row; never auto-delete (deleted entities may still need voice lookup for audit).

**What counts as "source text" per entity:** see ADR-021 Appendix A below. The function `BuildSourceText(entity)` is the single source of truth per entity type.

### D6 — Model-version migration playbook

When a new model is released or we change vendors:

1. Add the new `ModelVersion` to a recognized list (config-driven).
2. Run bulk backfill against the new model — writes new rows alongside old rows (different `ModelVersion` per row).
3. Switch voice query path to use the new model (single config flag).
4. After 7 days of stable query patterns, delete old rows: `DELETE FROM "Embeddings" WHERE "ModelVersion" = 'voyage-3-large/v1';`

Zero downtime, no search-quality cliff.

### D7 — Privacy + tenant isolation

- Source text is stored in `Embeddings.SourceText` for debug + cache invalidation. **No PII beyond what's already in the source entity.** ReceiptProfile descriptions, Item descriptions, Vendor names, WorkOrder titles — all already in the tenant's relational data.
- AuditLog.AiCommandText embeddings may contain voice utterances. These get the standard RLS tenant policy. **No cross-tenant query path** — `current_setting('app.tenant_id')` is the choke point per ADR-020 §D6.
- Voyage's data policy (verified May 2026): API inputs are NOT used for training, retained 30 days for abuse detection only. Compliant with our customer privacy promises.

### D8 — Cost projection (sanity check)

For Sprint 12C initial embed pass (3 signed customers, real volumes):

| Entity | Rows | Avg tokens | Total tokens |
| --- | ---: | ---: | ---: |
| ReceiptProfile | 12 | 200 | 2.4K |
| Item | ~5,000 | 100 | 500K |
| Vendor | ~500 | 80 | 40K |
| WorkOrder | ~10,000 | 250 | 2.5M |
| AuditLog.AiCommandText | ~20,000 | 50 | 1M |
| **Bulk backfill total** | | | **~4M tokens** |

At Voyage `voyage-3-large` $0.06 / M tokens = **~$0.24** for the entire initial embed. Steady-state incremental: <$5/mo for three customers.

Voice-query embeds at ~50 tokens per utterance, 1K utterances/day per customer = 50K tokens/day = $0.003/day per customer. Negligible.

---

## Appendix A — `BuildSourceText(entity)` per entity type

The function feeding the embedder. One source of truth; changes here require a re-embed (handled by hash).

### ReceiptProfile
```
{Code} | {Name}
{Description}
Industry: {ProfileType}
Required attributes: {SchemaFieldNames.JoinComma()}
Optional attributes: {OptionalFieldNames.JoinComma()}
```

### Item
```
{ItemNumber} | {Description}
{LongDescription ?? ""}
UoM: {Uom}
{ItemTypeFlags.JoinComma()}
Tags: {Tags.JoinComma()}
```

### Vendor
```
{Name}
{Description ?? ""}
Address: {City}, {StateOrProvince}, {Country}
{VendorTags.JoinComma()}
```

### WorkOrder
```
{WorkOrderNumber} | {Title}
{Description ?? ""}
Type: {WorkOrderClassification}
Status: {Status}
{ClassificationSatelliteSummary ?? ""}
```

### AuditLog.AiCommandText
```
{AiCommandText}
```
(Raw utterance — no enrichment.)

---

## Risks + mitigations

| Risk | Likelihood | Mitigation |
| --- | --- | --- |
| Voyage outage during peak voice usage | Low-Medium | Voice-path falls back to keyword routing (existing code still works); incremental embed queue catches up after recovery |
| Voyage pricing changes or model deprecation | Medium | ModelVersion column + 7-day dual-write playbook (D6) |
| Bulk backfill hits Voyage rate limit | Low | Worker concurrency capped at 4; well under ceiling |
| ContentHash collision (extremely unlikely with SHA-256) | Effectively zero | Standard cryptographic guarantee; no mitigation needed |
| Tenant data leaks across embeddings | Low (RLS enforced) | Per ADR-020 §D6 + this ADR §D7 |
| EVS ITAR customer rejects external embed API | Possible | Phase 5 / ADR-023 — Ollama self-hosted fallback for those customers |

---

## Decision

**Approved by:** _pending Dean sign-off_
**Implementation:** Sprint 12C PR #1 wires `IEmbeddingBackfillService` + the queue table + the Voyage client. First entity embedded end-to-end: ReceiptProfile.

---

## Cross-references

- ADR-020 — Postgres-as-AI-Native-OS Database Architecture (the foundation)
- [[project_voice_mvp_shipped]] — what the voice intent router will graduate from (keyword → vector)
- [[project_database_direction_2026_05_19]] — the parent vision
- [[project_roadmap_reshuffle_2026_05_19]] — Sprint 12C sequencing
