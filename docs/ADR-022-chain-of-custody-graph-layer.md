# ADR-022 — Chain-of-Custody Graph Layer (Sprint 12D)

**Status:** Accepted 2026-05-22
**Decided by:** Dean (CEO/Visionary) + Claude
**Driver:** Joe's **June 3 2026 EVS pitch** — needs a working "explain why X is late / who touched it / how does it trace back to mill cert" graph-narrated demo.
**Supersedes / amends:** ADR-020 Phase 2 timing (the AGE migration was originally scoped inside Sprint 12D's window — this ADR pushes the host migration to Q3 and ships the June 3 demo via Postgres-native graph emulation).

---

## Context

### What the EVS pitch needs

The June 3 demo's headline moment is a single voice utterance ("why is this receipt blocked?") returning a **narrated chain of evidence**: the receipt → its IQC checks → the mill cert → the heat number → the material master → upstream PO + vendor + carrier. Visually rendered as a graph. This is the difference between "our ERP looks like 1998" (SAP MIGO, NetSuite WMS, D365 F&O — table after table after table) and "our ERP looks like Linear / Stripe / Datadog" (relationships first-class, you can traverse them).

It's also the demo moment that proves the **audit-trail / chain-of-evidence v1 sales line** from the 2026-05-20 strategic absorption: every machine event walks to its general ledger entry through a typed, auditable path. We just shipped Sprint 12.9 (10 PRs) to make every write go through a typed service; Sprint 12D makes the resulting trail walkable.

### What ADR-020 committed to

ADR-020 (Postgres-as-AI-Native-OS, Accepted 2026-05-19) committed to **ONE Postgres instance** with five workloads: relational + vector (pgvector) + graph (Apache AGE) + time-series (TimescaleDB) + full-text (ParadeDB) + LLM tooling (pgai). Phase 1 (vector) shipped via Sprint 12C and closed 2026-05-22 with the worker race fix (PR #282). Phase 2 (graph) was scoped for Sprint 12D.

### What Replit's managed Postgres actually has

Verified live 2026-05-19 (memory `reference_replit_postgres_extensions`):

| Extension | Available on Replit? |
|---|---|
| `vector` 0.8.0 (pgvector) | ✅ |
| `timescaledb` 2.13 | ✅ Apache edition (TSL features blocked — see `feedback_timescaledb_apache_vs_tsl`) |
| `pgcrypto` | ✅ |
| `pg_stat_statements` | ✅ |
| `uuid-ossp` | ✅ |
| `pgvectorscale` | ❌ |
| **`age` (Apache AGE)** | **❌** |
| `pg_search` (ParadeDB) | ❌ |
| `pgai` | ❌ |

Of the five workloads ADR-020 committed, only vector + time-series (Apache edition) are running today. Graph is the next-up phase and it's blocked at the host.

### The window

- **Today:** 2026-05-22
- **June 3 EVS:** 2026-06-03 (T-12 days)
- **Q3 2026 start:** 2026-07-01 (T-40 days)

A full host migration of the entire main-app Postgres carries serious risk: new database connection strings, RLS policies re-verified, all 96 entities re-confirmed by schema sync, secrets rotated in Replit + CI, every existing migration's history-row re-stamped on the destination. The 2026-05-20 snapshot drift (memory `project_pr271_shipped`) and the Sprint 12C migration-discovery gotchas (memory `feedback_ef_migration_attribute_required`) both demonstrate how brittle that surface is right now. **Doing it inside a 12-day window before a Fortune-100-grade pitch is the wrong risk profile.**

---

## Decision

**The chain-of-custody graph for the June 3 EVS demo ships as virtual AGE: Postgres-native recursive CTEs + adjacency-list edge tables + cytoscape.js front-end visualization. Real Apache AGE migration deferred to Q3 2026.**

The graph schema, query surface, and front-end pattern locked in this ADR are designed so the Q3 swap-in of real AGE is a backend-only refactor — the C# `IChainOfCustodyService` interface and the Razor partial that renders the cytoscape.js graph stay identical.

---

## Options considered

### Option A — Full host migration NOW (Replit → Azure / AWS RDS / Supabase / Neon-with-AGE)

**Pro:** ADR-020 single-Postgres goal honored end-to-end. No virtual-graph rework later.
**Con:** 2-3 weeks minimum to validate against the entire app surface (96 entities, 47 migrations, RLS policies, secrets rotation, monitoring rewire). T-12 days to EVS makes this the riskiest option. If anything goes wrong on the day of the pitch — connection pooling tuned wrong, RLS subtly different across hosts, latency regression — the audit-trail story we just shipped becomes a liability instead of an asset.
**Verdict:** ❌ rejected for Sprint 12D. **Q3 2026 priority.**

### Option B — Sidecar Postgres-with-AGE running parallel to the main Replit Postgres

**Pro:** Real AGE for the demo. Main app stays on Replit. Replication via FDW (foreign-data wrapper) or dblink keeps the graph in sync.
**Con:** Two Postgres instances violates ADR-020's "ONE Postgres" principle. The replication layer is its own moving part — every chain-of-custody node now depends on TWO databases being healthy. New IAM surface to manage. Adds operational complexity that this team doesn't have headcount to support pre-launch.
**Verdict:** ❌ rejected. The temporary nature ("just for the demo") becomes permanent the day the pitch lands a customer.

### Option C — Defer the graph layer entirely; demo without it

**Pro:** Zero risk to the timeline.
**Con:** The graph-narrated "why is X late" moment IS the demo's headline. Without it, the pitch reduces to "we have a nice receiving cockpit" — which Sprint 12A already delivered and which is matchable by Tulip / Cin7. The differentiation collapses.
**Verdict:** ❌ rejected. The whole point of accelerating Sprint 12D into the June 3 window was the demo dependency.

### Option D — Virtual AGE: Postgres recursive CTEs + adjacency-list edge tables + cytoscape.js viz (THIS ADR)

**Pro:**
- Stays on Replit. Zero host migration risk.
- Postgres recursive CTEs are well-understood, well-indexed, performant for graphs up to ~10K nodes (the demo's scope is a single chain — ~20 nodes max).
- cytoscape.js renders graphs of any topology with built-in layout algorithms (dagre, cose, breadthfirst). Already battle-tested in dozens of Fortune-100 IT visualizations.
- The C# `IChainOfCustodyService` interface that fronts the CTEs is identical to the interface that would front real AGE. Q3 swap is a backend-only refactor — no PageModel, no Razor partial, no test changes.
- New edge table (`ChainEdges`) is a regular Postgres table with composite index — no extension required, no migration trauma.

**Con:**
- Not "real" graph database. Loses AGE-specific features: opencypher query language, native graph indexes (much faster on million-node traversals), graph algorithms (shortest-path, betweenness, communities).
- BUT: the demo doesn't need any of those. The chain is ~20 nodes deep; recursive CTEs traverse it in <10ms.
- The "single Postgres" ADR-020 goal is honored in spirit (one database) even though one extension is unavailable.

**Verdict:** ✅ **chosen.** Q3 2026 swaps in real AGE behind the same interface.

---

## Decision sub-points

### D1 — Graph schema

Two tables, both regular Postgres (no extensions):

```sql
CREATE TABLE "ChainNodes" (
    "Id" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "NodeType" varchar(40) NOT NULL,        -- 'PurchaseOrder', 'Receipt', 'IQC', 'Cert', 'Heat', 'MaterialMaster', 'Vendor', 'Carrier', 'WorkOrder'
    "EntityId" bigint NOT NULL,             -- FK into the underlying table (polymorphic — like Embeddings)
    "TenantId" int NOT NULL,                -- RLS scope
    "Label" text NOT NULL,                  -- human-readable for the viz ("PO-2026-0042", "RCPT-2026-1234")
    "Metadata" jsonb NULL,                  -- node-type-specific attributes (status, dates, amounts, etc.)
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX "ix_chainnodes_entity" ON "ChainNodes" ("NodeType", "EntityId", "TenantId");
-- Same RLS policy template as Embeddings: TenantId=0 OR TenantId=app.tenant_id

CREATE TABLE "ChainEdges" (
    "Id" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "FromNodeId" bigint NOT NULL REFERENCES "ChainNodes" ("Id") ON DELETE CASCADE,
    "ToNodeId" bigint NOT NULL REFERENCES "ChainNodes" ("Id") ON DELETE CASCADE,
    "EdgeType" varchar(40) NOT NULL,        -- 'RECEIVED_AT', 'INSPECTED_BY', 'CERTIFIED_BY', 'MELTED_FROM', 'SUPPLIED_BY', 'CARRIED_BY', 'CONSUMED_BY', etc.
    "TenantId" int NOT NULL,
    "Metadata" jsonb NULL,                  -- edge-type-specific attributes (timestamp, quantity, status)
    "CreatedAt" timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX "ix_chainedges_from" ON "ChainEdges" ("FromNodeId", "EdgeType");
CREATE INDEX "ix_chainedges_to" ON "ChainEdges" ("ToNodeId", "EdgeType");
-- Same RLS policy.
```

Polymorphic shape mirrors `Embeddings` — `(NodeType, EntityId)` is the polymorphic key, so adding a new NodeType in a future sprint doesn't need a schema migration. Mill-cert, heat-number, AP invoice, GL entry, capital improvement — all slot in without rework.

### D2 — Traversal queries

Two-direction recursive CTE — same pattern that ships in cookbooks for hierarchical lookups, just generalized.

```sql
-- Upstream chain from a Receipt:  RECEIPT → IQC → CERT → HEAT → MM → VENDOR
WITH RECURSIVE chain AS (
    SELECT n."Id", n."NodeType", n."EntityId", n."Label", n."Metadata", 0 AS depth, ARRAY[n."Id"] AS path
    FROM "ChainNodes" n
    WHERE n."Id" = @startNodeId
    UNION ALL
    SELECT n."Id", n."NodeType", n."EntityId", n."Label", n."Metadata", c.depth + 1, c.path || n."Id"
    FROM chain c
    JOIN "ChainEdges" e ON e."ToNodeId" = c."Id"
    JOIN "ChainNodes" n ON n."Id" = e."FromNodeId"
    WHERE n."Id" <> ALL(c.path)              -- cycle break
      AND c.depth < @maxDepth
)
SELECT * FROM chain ORDER BY depth;
```

Two parameterized variants ship in PR #2 — `GetUpstreamChainAsync(startNodeId, maxDepth)` and `GetDownstreamChainAsync(...)`. Both return an `IReadOnlyList<ChainHop>` shaped to feed the cytoscape.js renderer directly.

### D3 — Service interface (the Q3-swap stability point)

```csharp
public interface IChainOfCustodyService
{
    // Used by the AI "explain why" tool — returns the chain in the shape cytoscape.js wants
    Task<Result<ChainOfCustodyGraph>> GetChainAsync(
        string nodeType,
        long entityId,
        int maxDepth = 6,
        CancellationToken ct = default);

    // Service-layer chain build — called by IPostingService implementations, by ReceivingPostingService,
    // by WorkOrderService.CapitalizeAsync — every typed service hooks the relevant edge writes here.
    Task<Result<ChainNode>> RecordNodeAsync(RecordNodeRequest request, CancellationToken ct = default);
    Task<Result<ChainEdge>> RecordEdgeAsync(RecordEdgeRequest request, CancellationToken ct = default);
}
```

PR #2 implements this on top of `ChainNodes` + `ChainEdges` with the recursive CTE. Q3 2026 ships PR #N that replaces the CTE with `MATCH (a)-[:EDGE_TYPE]->(b) RETURN ...` opencypher inside Apache AGE. **The interface contract is the contract; the storage is a swappable detail.**

### D4 — Front-end rendering

cytoscape.js v3.30+ (CDN), `dagre` layout (top-to-bottom for upstream chains), node coloring by `NodeType`. Bundled into a single `_ChainOfCustodyGraph.cshtml` partial that takes a JSON payload and renders a 800×400 SVG. The partial is reusable from any page (Receipt detail, WorkOrder detail, Asset detail).

Why cytoscape.js (not vis.js / D3.js / Sigma.js): it's the de-facto standard for biology + IT topology visualizations, has the most complete layout-algorithm library, and the JSON shape `{nodes: [...], edges: [...]}` matches the recursive-CTE result one-for-one.

### D5 — Service-layer integration (the wire-up moment)

Sprint 12.9's three new services already have the right shape for chain emission:

| Service (Sprint 12.9) | Chain emit point |
|---|---|
| `IWorkOrderService.CapitalizeAsync` | `WorkOrder → CapitalImprovement → Asset` edges |
| `IPurchasingService.ApproveAsync` | `PurchaseOrder → ApprovedBy(User)` edges |
| `IItemMasterService.UpdateItemAsync` | `Item → version_of(...)` edges (revision lineage) |
| `ReceivingPostingService.PostAsync` | `Receipt → PO + IQC + Cert + Heat + MaterialMaster` edges (the EVS demo chain) |
| `ApPostingService.PostAsync` | `Invoice → GL + Receipt + PO` edges |

Each of these adds 2-4 lines of `await _chainOfCustody.RecordEdgeAsync(...)` at the right moment. The CHERRY025 analyzer + the ratchet keep these writes typed.

### D6 — RLS

`ChainNodes` and `ChainEdges` both carry `TenantId int NOT NULL` and use the same RLS policy template as `Embeddings`:

```sql
CREATE POLICY chain_tenant_isolation ON "ChainNodes"
    USING (("TenantId" = 0) OR ("TenantId" = NULLIF(current_setting('app.tenant_id', true), '')::int));
```

The Sprint 12.9 PR #7 cross-tenant leakage tests (`tests/Abs.FixedAssets.Tests/CrossTenantLeakageTests.cs`) extend to cover `IChainOfCustodyService` in Sprint 12D PR #2.

### D7 — Q3 2026 host migration (when, not if)

Real Apache AGE arrives in Q3 2026 — sequenced to land AFTER:
1. Sprint 12D wraps with the chain-of-custody demo working.
2. EVS pitch + at least 2 customer LOIs in hand.
3. Backed-up snapshot of the production Replit Postgres + 7-day rollback window.

Q3 ADR-024 will document the host choice (likely Neon-with-extensions or Supabase, both of which now ship with AGE; possibly Azure Postgres Flexible Server if Joe's enterprise channel needs Microsoft-stack alignment). The migration is a single PR that:

1. Stand up the new host with the full extension stack.
2. `pg_dump` + `pg_restore` from Replit.
3. Switch the connection string in Replit secrets.
4. Replace the CTE-backed `ChainOfCustodyService` implementation with the AGE-backed one (same interface).
5. Verify the chain-of-custody demo still works on `/Receiving/Details/...`.

---

## Consequences

### Positive

- **June 3 EVS demo unblocked.** Chain-of-custody graph ships on the schedule needed for the pitch.
- **No host migration risk** in the 12-day window. Zero new variables introduced for the demo day.
- **Q3 swap is a backend refactor.** Interface stays. PageModel stays. Tests stay. cytoscape.js partial stays.
- **Audit-trail story holds together.** Sprint 12.9 typed every write; Sprint 12D makes those writes walkable. The "Machine Event → General Ledger" v1 sales line is now demonstrable end-to-end.

### Negative

- We are publicly committing to a Q3 host migration. This is a permanent debt entry on the architecture board — accepted because the demo dependency overwhelms the architecture-purity argument for the June 3 window.
- The CTE-backed implementation cannot run graph algorithms (shortest-path / betweenness / community detection) that real AGE would offer. None of those are demo-day requirements. They're future v2 features that wait for the host swap.
- A bit of code is "throwaway" in the sense that the storage layer gets rewritten in Q3 — but the schema, the interface, the tests, and the UI partial all stay. The throwaway portion is ~200 lines of CTE SQL.

### Neutral / accepted

- Two extra tables (`ChainNodes`, `ChainEdges`) in the relational schema. They mirror the `Embeddings` polymorphic-key pattern, so the team already knows how to reason about them.
- The Sprint 12.9 ratchet sees `IChainOfCustodyService` as a new service to allowlist for direct `_db.ChainNodes` reads (per ADR-025 D1 — reads via AppDbContext are OK). Each new service caller gets `[ControlPlaneExempt]` or routes writes through `IChainOfCustodyService.RecordEdgeAsync`.

---

## Sprint 12D scope (8 PRs locked by this ADR)

1. ✅ **PR #1 — THIS ADR-022** (docs-only, ships as PR #283).
2. ⏳ **PR #2 — `ChainNodes` + `ChainEdges` migration + `IChainOfCustodyService` + recursive-CTE backend + cross-tenant test.**
3. ⏳ **PR #3 — Edge-emit wire-up in `ReceivingPostingService` + `IWorkOrderService.CapitalizeAsync` + `ApPostingService` + `IPurchasingService.ApproveAsync` + `IItemMasterService` revision lineage.**
4. ⏳ **PR #4 — `_ChainOfCustodyGraph.cshtml` partial + cytoscape.js bundle + Receipt detail page integration.**
5. ⏳ **PR #5 — Voice tool `explain_chain_of_custody` + `HybridIntentRouter` intent + narration template ("RCPT-2026-1234 traces back to heat H-12345, mill cert ABC, vendor Acme, PO PO-555, ordered 2026-05-12").**
6. ⏳ **PR #6 — Audit-trail acceptance demo: full "Machine Event → General Ledger" walk for a sample receipt; one-click trace through the live graph rendered on `/Receipt/Details/{id}`.**
7. ⏳ **PR #7 — Performance harness: 1000-node chain traversal benchmark, p95 < 200ms (CTE) target; failover playbook in case of edge-table corruption.**
8. ⏳ **PR #8 — Q3 host migration ADR-024 stub + dry-run script.** Lays the file at the right path so the Q3 sprint just fills in the host details.

---

## Cross-refs

- ADR-020 — Postgres-as-AI-Native-OS (5-phase plan; AGE was Phase 2)
- ADR-021 — Embedding model + pipeline (Sprint 12C — closed by PR #282)
- ADR-025 — Service Layer Standard (Sprint 12.9 — closed by PR #281)
- Memory `project_database_direction_2026_05_19` — original 5-phase commitment
- Memory `project_roadmap_reshuffle_2026_05_19` — June 3 deadline lock
- Memory `reference_replit_postgres_extensions` — what's actually available
- Memory `project_sprint_12_9_complete` — audit-completeness substrate
- Memory `project_pr282_sprint_12c_closeout` — Sprint 12C closeout (sets up the audit trail this graph walks)
