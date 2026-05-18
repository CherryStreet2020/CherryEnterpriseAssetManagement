# ADR-015 — Industry-Agnostic Receipt Schema

**Status:** Proposed — awaiting Dean sign-off
**Date:** 2026-05-18
**Author:** Architecture (Claude)
**Supersedes:** N/A
**Builds on:** ADR-013 (Polymorphic Production Backbone), ADR-014 (Phase F Voice-Ready Foundation)
**Reverses:** The sheet-metal-centric `StockReceipt` columns shipped in PR #119.13b and PR #219.
**Research:** [`docs/research/industry-agnostic-receipt-schema.md`](research/industry-agnostic-receipt-schema.md) (~1,600 lines, 14-industry survey, 7-pattern comparison, ERP precedent review)

---

## Question

We just shipped a `StockReceipt` table + admin CRUD (PR #119.13b table, PR #219 UI). The schema is **correctly modeled for a structural-steel / aerospace machining shop and nothing else**. Its load-bearing columns (`HeatNumber`, `MillCertUrl`, `Mill`, `LengthMm/WidthMm/ThicknessMm`, `UsableLengthMm/UsableWidthMm`) bake the ASME / AWS / AS9100 worldview into the schema.

CherryAI EAM is targeting a multi-industry market. The schema is simultaneously **too specific** (none of those columns apply to pharma, food, electronics, cannabis, etc.) and **too narrow** (it cannot represent the equally-mandatory trace fields other regimes demand: DSCSA serials, FSMA traceability lot codes, EUDAMED UDI-PI/DI, METRC tags, IPC MSL clocks, IATF PPAP levels, REACH CAS numbers).

**Two related decisions are required:**

1. **What schema shape** can hold the receipt fields for 10+ industries without a code deploy per vertical, without subclass-explosion, without EAV's perf cliff, and without losing voice-AI introspectability?
2. **What is the primary receiving workflow** — blank "New Receipt" form (what we just shipped) or PO-driven receive-against-PO wizard (what real receiving operations look like)?

---

## State of practice (research-validated)

Full survey in [the research doc](research/industry-agnostic-receipt-schema.md). Summary:

- **SAP MM Batch Management** uses "batch class + characteristics" — a profile that defines which attributes apply per material. ~1986 design; still works.
- **Oracle Cloud Inventory** uses Extensible Flexfields (EFFs) / Descriptive Flexfields (DFFs) on lot + serial tables. Profile-driven at runtime.
- **Microsoft Dynamics 365 F&O** uses an "Inventory Dimensions" framework with Product / Storage / Tracking dimensions; lot and serial are **always universal**; vertical-specific attributes hang off the batch.
- **NetSuite** uses Custom Fields per item type — same hybrid pattern.
- **JSONB on Postgres** outperforms EAV by ~1000× on writes; expression-indexed JSONB lookups perform identically to native B-tree on the same field at million-row scale.
- **JSON Schema (Draft 2020-12)** is stable, has mature .NET tooling (`JsonSchema.Net`), and is well-suited to voice-AI prompt-context injection.

The receiving-workflow reality across every vertical surveyed is identical: **~80% of receipt fields are known at PO-issue time** (Item, Supplier, expected qty, default profile, default mill / DEA schedule / MSL level), **~10–15% arrive in the ASN** (lot #, serial range, heat #, expiry), **~10% are typed off supplier documents** (mill cert, COA, packing slip), and **~5% are the receiver's eyes + scanner** (actual qty vs expected, damage, temperature). Hand-typing all of it on a blank form is a workflow that nobody runs in production.

---

## Decisions

### D1 — Profile-driven typed-core + JSONB hybrid schema

Every `StockReceipt` row has:

- **A small, strictly-typed core** for fields every industry needs: `Id`, `ReceiptNumber`, `ItemId`, `ProfileId`, `LotNumber`, `SerialNumber`, `ReceivedAt`, `ReceivedByUserId`, `LocationId`, `QuantityReceived`, `QuantityRemaining`, `Uom`, `SourcePoNumber`, `SourcePoLineId`, `Status`, `QuarantineReason`, `Notes`, audit/concurrency cols.
- **A `ProfileId` FK** to a new `ReceiptProfiles` catalog table.
- **An `Attributes jsonb`** column that holds the industry-specific payload, validated against `ReceiptProfile.JsonSchema` at the service layer.

**Promoted to core (universal):** `LotNumber` and `SerialNumber`. These are universal tracking dimensions in every system surveyed (D365 F&O treats them as universal tracking dims; SAP treats both as first-class).

**Pushed into Attributes (industry-specific):** `HeatNumber`, `MillCertUrl`, `Mill`, `LengthMm`, `WidthMm`, `ThicknessMm`, `UsableLengthMm`, `UsableWidthMm`. These belong to the Steel profile, not the universal schema.

### D2 — `ReceiptProfiles` is a config-table-driven schema definition

A new `ReceiptProfiles` table (similar shape to existing `RegulatoryProfiles`) holds:

- `Code` — unique identifier (`STEEL`, `PHARMA`, `FOOD`, `CHEMICAL`, `ELECTRONICS`, `MEDICAL_DEVICE`, `AEROSPACE`, `CANNABIS`, `AUTOMOTIVE`, `APPAREL`, `CONSTRUCTION`, `OIL_GAS`)
- `Name`, `Description`
- `JsonSchema` (`jsonb`) — the schema for `StockReceipt.Attributes` when this profile is active
- `PromotedFacets` (`text[]`) — fields to expression-index (e.g. `["heatNumber", "mill"]` for Steel)
- `DefaultAttributes` (`jsonb`) — defaults seeded on new receipt of this profile
- `UiFormSpec` (`jsonb`) — field order, grouping, labels, voice hints — the Razor `Edit` page renders dynamically from this
- `RegulatoryProfileIds` (`int[]`) — which `RegulatoryProfile` gates fire on receipt of this type
- `IsActive` + audit cols

We ship 12 starter profiles. Customers/tenants can add their own (custom-fork the Apparel profile to add their cotton-grading fields, etc.).

### D3 — Expression-indexed JSONB for hot per-profile query fields

For the 5–10 fields per profile that drive 90% of queries, add Postgres expression indexes:

```sql
CREATE INDEX ix_receipt_attr_heat        ON "StockReceipts" ((attributes ->> 'heatNumber'));
CREATE INDEX ix_receipt_attr_exp_date    ON "StockReceipts" (((attributes ->> 'expirationDate')::date));
CREATE INDEX ix_receipt_attr_metrc       ON "StockReceipts" ((attributes ->> 'metrcTag'));
CREATE INDEX ix_receipt_attr_ndc         ON "StockReceipts" ((attributes ->> 'ndc'));
CREATE INDEX ix_receipt_attr_tlc         ON "StockReceipts" ((attributes ->> 'traceabilityLotCode'));
CREATE INDEX ix_receipt_attr_gin         ON "StockReceipts" USING gin (attributes jsonb_path_ops);
```

The GIN index handles the long-tail containment-query case (`attributes @> '{"key":"value"}'`). The per-field expression indexes handle the hot equality/range cases. Together, query performance matches native typed columns for the queries that matter and degrades gracefully (≤5 ms at million-row scale) for the long tail.

### D4 — PO-driven receiving is the primary workflow; `CreateAdHocAsync` is the escape hatch

`IStockReceiptService` evolves to expose two creation paths:

```csharp
// Primary (95%+ of receipts) — pre-populates from PO line + Item + Supplier + ASN
Task<Result<StockReceipt>> CreateFromPoLineAsync(
    long purchaseOrderLineId,
    ReceiveAgainstPoRequest request,
    int actorUserId, Guid? idempotencyKey, CancellationToken ct);

// Escape hatch (5% — returns, intercompany, customer-supplied, found stock, prototype)
Task<Result<StockReceipt>> CreateAdHocAsync(
    CreateStockReceiptRequest request,
    int actorUserId, Guid? idempotencyKey, CancellationToken ct);
```

The admin sidebar exposes three receiving paths:

- `/Receiving/Inbox` (NEW, primary) — open PO lines + ASNs expected this week
- `/Receiving/PO/{poId}` (NEW) — receive-against-PO wizard pre-populated with 80% of fields
- `/Admin/StockReceipts` (existing PR #219) — demoted to audit/lookup grid; "New Receipt" button routes to PO Inbox

The current Edit page is retained for the **edit-existing** case (correcting a typo'd lot # after the fact); we don't throw away the PR #219 work.

### D5 — Required adjacent extensions on `Item` and `Vendor`

The PO-driven workflow requires two small extensions to existing tables, landed in the **same migration** as D1–D3:

**`Item` table — additive columns:**
- `DefaultReceiptProfileId int? FK -> ReceiptProfiles` — which profile applies when this SKU is received
- `DefaultReceiptAttributes jsonb` — per-item defaults (e.g. DEA schedule for a controlled-substance SKU, MSL level for a moisture-sensitive electronic component)

**`Vendor` (Supplier) table — additive columns:**
- `DefaultReceiptAttributes jsonb` — per-supplier defaults (e.g. mill name, supplier's GFSI cert #)
- `SendsAsn bool` — does this supplier transmit ASNs?
- `AsnFormat varchar(32)` — `EDI856` / `EPCIS` / `CSV` / `NONE`

These are tiny, low-risk, additive. They make the PO-driven wizard work the day ADR-015 lands. The actual ASN ingestion tables (`AdvanceShipNotice`, `AsnLine`, `PoLineReceiptLink`) defer to a follow-up sprint.

### D6 — JSON Schema validation at the service layer + Postgres backstop

`StockReceiptService.CreateAsync` and `UpdateAsync` validate the `Attributes` payload against `ReceiptProfile.JsonSchema` via `JsonSchema.Net` (mature, pure-managed, sub-millisecond per receipt). A Postgres `CHECK (jsonb_typeof(attributes) = 'object')` constraint backstops against migration / direct-SQL drift.

A nightly `attributes`-conformance audit job (cron task) flags any rows whose Attributes don't validate against their profile's current schema. This catches schema-drift after a profile is updated.

### D7 — Per-profile reporting views

For BI tools that don't speak JSONB well (Tableau, Power BI), we ship per-profile views that flatten the JSONB into typed columns:

```sql
CREATE VIEW vw_receipts_steel AS
SELECT
    sr."Id", sr."ReceiptNumber", sr."ItemId", sr."LotNumber",
    sr."ReceivedAt", sr."QuantityReceived", sr."QuantityRemaining",
    sr."Status", sr."Notes",
    (sr."Attributes" ->> 'heatNumber')::varchar(64)      AS "HeatNumber",
    (sr."Attributes" ->> 'mill')::varchar(128)            AS "Mill",
    (sr."Attributes" ->> 'millCertUrl')::varchar(500)     AS "MillCertUrl",
    (sr."Attributes" ->> 'lengthMm')::numeric(10,2)       AS "LengthMm",
    -- ... etc
FROM "StockReceipts" sr
JOIN "ReceiptProfiles" p ON sr."ProfileId" = p."Id"
WHERE p."Code" = 'STEEL';
```

Generated automatically from `ReceiptProfile.JsonSchema` by a small code-gen step. Doubles as profile-schema documentation.

### D8 — Voice-AI gets the profile JSON Schema as prompt context

The voice-AI co-pilot (Sprint 5) receives, as prompt context for any receipt-related query: the active receipt's `ProfileId`, the resolved `JsonSchema`, and the `UiFormSpec` (which carries voice synonyms — e.g. `{"key":"heatNumber", "voice":["heat","heat number"]}`). This makes the LLM grammatically aware of which fields exist on which profiles and which natural-language phrases map to which JSON keys.

Voice query translation examples (full table in §4.4 of the research doc):

| Voice input | LLM-built query |
|---|---|
| "Find all receipts of heat H-12345" | `WHERE attributes ->> 'heatNumber' = 'H-12345'` |
| "Find all lots of lidocaine expiring within 30 days" | `JOIN Items WHERE Description ILIKE '%lidocaine%' AND (attributes ->> 'expirationDate')::date <= now() + interval '30 days'` |
| "Receive PO 12345" | Opens `/Receiving/PO/12345` wizard pre-populated |

### D9 — Migration sequence (3 PRs, additive-first)

**Migration PR #1 — Additive, zero breakage:**
1. Create `ReceiptProfiles` table; seed 12 starter profiles.
2. Add `ProfileId`, `Attributes`, `SerialNumber` to `StockReceipts` (all nullable initially).
3. Add `DefaultReceiptProfileId`, `DefaultReceiptAttributes` to `Items`.
4. Add `DefaultReceiptAttributes`, `SendsAsn`, `AsnFormat` to `Vendors`.
5. Add expression indexes on the hot facets for each profile.
6. Service stays on the old code path; old steel columns + new Attributes coexist.

**Migration PR #2 — Backfill:**
7. For every existing `StockReceipt` row, set `ProfileId = STEEL.Id` and copy the 8 sheet-metal columns into `Attributes` JSON.
8. Verify row counts + sample-test 20 rows manually.
9. Add `NOT NULL` constraint on `ProfileId`.
10. Dual-write period: service writes both the legacy columns AND `Attributes` for one sprint (safety net).

**Migration PR #3 — Switch primary path + drop legacy columns:**
11. Razor Edit page renders dynamically from `UiFormSpec` (one form for all profiles).
12. Service stops writing the legacy columns; reads come exclusively from `Attributes`.
13. Build `/Receiving/Inbox` + `/Receiving/PO/{poId}` wizard.
14. Drop `HeatNumber`, `MillCertUrl`, `Mill`, `LengthMm`, `WidthMm`, `ThicknessMm`, `UsableLengthMm`, `UsableWidthMm` columns from `StockReceipts`.
15. Drop per-profile reporting views regenerate.

### D10 — Voice-AI spike before Migration PR #1 lands

Validation #3 from the research doc is a hard prerequisite. Before PR #1 ships, we run a one-day spike: feed Steel + Pharma + Food profile JSON Schemas + example voice utterances to Claude in API context and verify it generates correct queries against the hybrid schema. If it consistently misses, capture what extra metadata the profile needs (synonyms, type hints, example queries) and add it to `UiFormSpec` before we ship.

**Spike result (2026-05-18):** **Conditional GO.** Full report at [`docs/research/voice-ai-spike-adr015-d10.md`](research/voice-ai-spike-adr015-d10.md). Summary: 5/9 ✅ correct, 1 ⚠️ partial, 2 ❌ wrong, 1 ❓ unsafe-mutation. All four failure modes are prompt-engineering / metadata gaps — **not schema problems.** The hybrid schema is validated. Migration PR #1 scope expands by ~1 hour to seed four new optional `UiFormSpec` field-spec keys (`scope`, `exampleQueries`, `disambiguation`, `semanticAction`), and the v1 voice-AI prompt template ships with five non-negotiable stanzas (MUTATION POLICY, AMBIGUITY HANDLING, SEMANTIC ACTIONS, DEFAULTS, TENANT GLOSSARY). Migration PR #1 also stubs four service tools (`traceChainOfCustody`, `listExpectedReceipts`, `quarantineByFilter`, `lookupReceipt`) to lock the tool catalog shape.

---

## Consequences

### Positive

- **Multi-industry positioning unlocked.** CherryAI EAM can credibly target pharma, food, cannabis, electronics, medical devices, etc. — not just steel/aerospace.
- **No code deploy per new vertical.** Adding a new industry is a `ReceiptProfile` insert + JSON Schema, not a sprint of work.
- **Voice-AI gets dramatically more capable.** The profile schema gives the LLM grammatical awareness of fields, types, synonyms, and validity. "Receive PO 12345" maps to a real wizard with 80% pre-filled.
- **Aligns with Sprint 7 (Item Master Expansion)** — Item's `DefaultReceiptProfileId` becomes the natural anchor for industry classification.
- **Aligns with Sprint 8 (Multi-Dim Inventory, D365 F&O-style)** — `LotNumber` and `SerialNumber` as universal tracking dimensions match D365's framework exactly.
- **Query performance ≥ native typed columns** for the queries that matter (via expression indexes); graceful degradation for the long tail (via GIN).
- **30+ years of ERP design experience validates the pattern** — SAP, Oracle, NetSuite, D365 all converged here independently.

### Negative

- **JSONB validation is service-layer, not DB-level CHECK constraint.** A bad migration or service bug can leak invalid JSON. Mitigated by JSON Schema validator + `jsonb_typeof` check + nightly conformance audit.
- **Tableau / Power BI don't speak JSONB natively.** Mitigated by per-profile flattening views.
- **The current PR #219 admin UI becomes secondary** (still useful for edit-existing). One sprint of work isn't thrown away, but its role changes.
- **3-PR migration sequence requires sprint-and-a-half of focused work.** Cheaper now than later — StockReceipts is not yet populated at scale.
- **Voice-AI prompt context grows with profile count.** ~2KB per profile × 12 starter profiles = ~25KB total. Comfortably fits modern LLM context windows.

### Neutral

- The 12 starter profiles ship in the `ReceiptProfiles` seed migration. Customers can fork them to add their own fields without touching code.
- The `RegulatoryProfile` table (PR #216) and the new `ReceiptProfile` table are conceptually parallel: one defines compliance gates, the other defines schema. They cross-reference via `ReceiptProfile.RegulatoryProfileIds`.

---

## Alternatives rejected

- **Pure polymorphism** (separate `PharmaReceipt`, `SteelReceipt`, `FoodReceipt` tables): subclass explosion; new vertical requires new table + service + UI + tests. Rejected.
- **Table-per-hierarchy** (single fat table with nullable industry-specific columns): unbounded column sprawl; we already burned two namespace-collision bugs this sprint adding enums. Rejected.
- **Pure EAV** (`ReceiptAttribute` table with `key`, `value`, `type`): Postgres benchmarks show JSONB beats EAV ~1000× on writes; query plans degrade with attribute count. Rejected.
- **Satellite-table pattern** (à la WorkOrder's CIP/Quality/Engineering/HSE): works for closed internal universes (4 fixed subtypes), wrong shape for open external universe (10+ industries + tenant variants). Rejected.
- **Pre-built fixed-set schema for each industry** (e.g. `PharmaReceiptAttributes` typed columns): every regulatory update requires a schema migration. Pharma's DSCSA fields alone changed 4 times between 2017 and 2025. Rejected.

---

## Validation before adoption

(Captured as TaskCreate #171.) Before Migration PR #1 ships, run these five validations from §5.2 of the research doc:

1. **Migration timing on a copy of prod** — verify the `HeatNumber` → `Attributes` backfill is sub-30-second on full data set. If it's slow, plan a maintenance window.
2. **Expression-index query plan stability** — synthesize a 10M-row test table and verify Postgres consistently picks the expression index for `attributes ->> 'heatNumber' = ?`.
3. **Voice-AI spike (D10)** — Claude correctly generates queries from the 5 example utterances for at least Steel + Pharma + Food.
4. **JSON Schema validator performance** — `JsonSchema.Net` benchmarks sub-millisecond on a 30-field receipt under load.
5. **Razor TagHelper polymorphism** — build the dynamic form for Steel + Pharma + Food and verify it's truly one component, not three.

---

## References

- Research doc: [`docs/research/industry-agnostic-receipt-schema.md`](research/industry-agnostic-receipt-schema.md)
- Predecessor ADR: ADR-013 (Polymorphic Production Backbone)
- Predecessor ADR: ADR-014 (Phase F Voice-Ready Foundation)
- Successor work: Migration PR #1 (TaskCreate #172), PR #2 (#173), PR #3 (#175); Voice-AI spike (#171); UI evolution (#174)
- SAP MM Batch Management docs
- D365 F&O Inventory Dimensions framework docs
- Oracle Cloud Inventory EFFs/DFFs docs
- Postgres JSONB performance whitepaper (Citus / EnterpriseDB)
- FDA DSCSA enforcement timeline (2024-11-27, 2025-08-27)
- FDA FSMA Section 204 Final Rule + 2025 compliance extension
- EU MDR / EUDAMED basic UDI-DI guidance
- METRC state cannabis tracking schema
- GS1 GTIN / SSCC / GLN identifier standards
- IPC J-STD-033 (MSL)
- IATF 16949 / AIAG PPAP
- IATA DGR (Dangerous Goods Regulations)
