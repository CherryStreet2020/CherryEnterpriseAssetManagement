# ADR-018 — Cockpit-First Pattern (amends ADR-016)

**Status:** ✅ Accepted 2026-05-18 by Dean (greenlit Sprint 12A → 12B → 12.5 sequence)
**Date:** 2026-05-18
**Author:** Architecture (Claude)
**Supersedes:** N/A (amends ADR-016 §D1 and ADR-016 §D2)
**Builds on:** ADR-014 (Voice-Ready Foundation), ADR-015 (Industry-Agnostic Receipt Schema), ADR-016 (Control Center Pattern + Receiving Pilot), ADR-017 (Control-Center-First Sidebar IA)
**Trigger:** Post-Sprint-11 live walkthrough of `/Receiving` (four-quadrant scaffold) versus the legacy `/Receiving/Cockpit-Legacy` (Pages/Receiving/Index.cshtml). Dean: *"I like the legacy receiving cockpit look and feel much better. It looks better and it's a much better workflow. It's not even close in fact. Let's put that functionality into the Receiving Control Center for PO receiving and then do something similar for ASN and to do orphan PO's. The Control Center is a great idea but the legacy look and nav is WAY BETTER."*

---

## Question

ADR-016 §D1 locked the four-quadrant scaffold — KPI strip + exception lane + detail drawer + activity feed — as the standard shape for every Control Center. The Receiving pilot (Sprint 11) shipped that shape end-to-end on the live URL.

When Dean walked the live `/Receiving` page against the legacy Receiving Cockpit at `/Receiving/Cockpit-Legacy`, the verdict was unambiguous: the legacy Cockpit is materially better for the daily-driver receive workflow. The four-quadrant scaffold treats the workspace as an exception-triage dashboard; receiving clerks need a queue-and-detail-pane workspace.

Two decisions are required:

1. **What is the right daily-driver shape** for a Control Center when the role's primary job is "work the queue, one item at a time" (Receiving, Maintenance dispatch, Planning exceptions, Scheduling, Inventory cycle-counts, Shipping cut-offs)?
2. **What does that imply for `/Receiving`** — locked tightly enough that the Sprint 12 rebuild PRs can start without further architectural debate?

---

## Why this is a real amendment, not a redo

The Sprint 11 build is not waste. The substrate it produced — `IReceivingControlCenterService`, `ReceiptStateMachine`, 10 `IReceiptVoiceTools`, the ADR-015 profile-driven drawer body, the DataWedge focus mode, the four-quadrant primitives (`_KpiStrip`, `_ExceptionLane`, `_ActivityFeed`, `_ControlCenterShell`), the Sprint 11.5 Control-Centers sidebar spine, the `ControlCenterRegistry`, and `VoiceReadyPageModel`-based wiring — is exactly the substrate the Cockpit shape needs. What we got wrong was the **default canvas**, not the architecture.

The four-quadrant scaffold is still right for **anomaly triage** (it's modelled on airline operations centers and NASA mission control, which *are* exception-management surfaces). It is wrong for **daily queue work**, which is what receiving clerks, maintenance techs, planners, schedulers, cycle-counters, and shipping clerks do most of the day.

The fix is to invert which sub-tab is the default, generalize the Cockpit pattern as a first-class primitive, and reuse 100% of the Sprint 11 service / voice / primitive layer underneath both shapes.

---

## State of practice (no new research; references prior docs)

ADR-016's research doc (`docs/research/receiving-control-center.md`, 1,152 lines) already surveyed SAP MIGO, Oracle Fusion Cloud Receipt Routing, NetSuite WMS Mobile, D365 F&O Arrival Overview + Quarantine Orders, Plex, Epicor Kinetic Mass Receipt, Acumatica Receiving Dashboard. The relevant slice for this ADR:

- The **operator-first** tools that consistently win UX awards — Linear (issue queue + detail pane), Front (inbox + conversation pane), Superhuman (inbox + reading pane), Bloomberg Terminal (watchlist + ticker pane), GitHub Notifications (subject queue + thread pane), Stripe Dashboard's right-rail drawer — all converge on the same shape: **a sortable / filtered queue on the left, a detail preview pane on the right, a context-aware KPI / status bar on top, and a context-aware action row near the preview header.**
- **None** of the surveyed ERP receiving screens use this shape as the default. SAP MIGO is a blank-form-driven transaction header. Oracle Cloud Receipts is a wizard. NetSuite WMS is a tabbed list. D365 Arrival Overview is a grid. Plex is a paper-form. Epicor Mass Receipt is a multi-select grid. Acumatica is a CRUD dashboard. **This pattern is uncontested in EAM/WMS as the default canvas.**
- The legacy Receiving Cockpit (Pages/Receiving/Index.cshtml, shipped pre-Sprint-11) already executes this pattern correctly in the CherryAI codebase: time-bucketed PO queue grouped by Overdue / Due Today / This Week / Upcoming, click-to-preview right pane, "Receive This PO" CTA in the preview header, four header KPI stats wired to `_ReceivingIndexKpis` partial. Performance was already optimized in PR #117.8 (median page load ~100ms).

---

## Decisions

### D1 — The Cockpit pattern is the daily-driver canvas for every v1 Control Center

The Cockpit is the **default tab** for every v1 Control Center (Receiving / Purchasing / Maintenance / Planning / Scheduling / Inventory / Shipping). It has five regions:

1. **Screen header** (existing `_ScreenHeader.cshtml`) — page title, context summary, KPI tile strip across the top, primary action row on the right. Three slots already supported: `ContextPartial`, `KpisPartial`, `ActionsPartial`.
2. **Left rail — queue** (~360px wide) — sortable / searchable / time-bucketed list of work items in role scope. Time-bucket grouping is the default lens; alternate lenses are role-specific (e.g., Maintenance defaults to priority × SLA; Inventory defaults to zone × count-age).
3. **Right pane — preview** — selected row's detail summary + line items + context-aware "primary action" button. Selecting a row does NOT navigate away; it populates the right pane in place. The primary action button (e.g., "Receive This PO") is what navigates into the full workflow page.
4. **Welcome state** — when no row is selected the right pane shows a welcome panel with bucket counts as large numbers (matches the legacy Cockpit). This is the page's hero state.
5. **Empty state** — when the queue is empty the left rail shows a "All caught up!" checkmark. This is a behavior-modification surface.

The four-quadrant scaffold from ADR-016 §D1 is retained but **demoted** to a sub-tab labeled **Exceptions**. It is still the right shape for anomaly triage.

### D2 — `/Receiving` becomes a four-tab shell

`/Receiving` (the route locked in ADR-016 §D2) now renders a four-tab shell. The shell is a new partial `_CockpitTabShell.cshtml` placed under `Pages/Shared/Primitives/`. Tabs in order:

| Tab key | Default? | Canvas | Source data |
|---|---|---|---|
| `po-queue` | ✅ default | Cockpit | `IReceivingControlCenterService.GetPoQueueAsync` (new method; backed by today's `PendingPOs` query) |
| `asn-queue` | | Cockpit | `IReceivingControlCenterService.GetAsnQueueAsync` (new method; backed by Phase F StockReceipts + ASN linkage) |
| `orphans` | | Cockpit | `IReceivingControlCenterService.GetOrphanReceiptsAsync` (new method; unmatched receipts with `MatchOrphanReceipt` AI suggestion column) |
| `exceptions` | | Four-quadrant (existing scaffold) | `IReceivingControlCenterService.GetExceptionLaneAsync` + `GetKpiStripAsync` + `GetActivityFeedAsync` (already shipped in Sprint 11) |

Tab state is part of the route: `/Receiving?tab=asn-queue`. Default `/Receiving` opens the PO Queue tab (matches today's legacy Cockpit behavior). Deep-link state is preserved across refresh and browser back/forward.

The existing wizard pages `/Receiving/By-Po/{poId}`, `/Receiving/By-Asn/{asnId}`, `/Receiving/Blind` remain reachable and unchanged. They are the destination when a user clicks the "Receive This PO" CTA in the right pane. A future PR may inline them as right-pane content; for now they stay as full pages and the Cockpit's right pane shows the preview, not the full receive wizard.

### D3 — Cockpit primitives are first-class and reusable

The legacy Receiving Cockpit's partials (`_ScreenHeader`, `_QueueCard`, `_ReceivingIndexContext`, `_ReceivingIndexKpis`, `_ReceivingIndexActions`, `_ReceiptDetailsContext`, `_ReceiptDetailsKpis`, `_ReceiptDetailsActions`) are extracted into reusable primitives under `Pages/Shared/Primitives/Cockpit/`:

- `_CockpitShell.cshtml` — grid container, calculates `100vh - 280px` so it fits under `_ScreenHeader` without scrolling the page.
- `_CockpitQueue.cshtml` — left rail. Inputs: title, count, search placeholder, group set, per-group row renderer.
- `_CockpitQueueGroup.cshtml` — one time-bucket (label + tone + count + collapsible rows).
- `_CockpitQueueCard.cshtml` — one row. Generic: takes `id`, `primary`, `secondary`, `metaTriples`, `tone`. Selectable; sets the right-pane preview via the existing JS pattern.
- `_CockpitPreview.cshtml` — right pane wrapper. Slots: header (PO# + status + primary action button), info row (5-7 labeled fields), line table.
- `_CockpitWelcome.cshtml` — no-row-selected state. Inputs: icon, title, subtitle, 4-tile stat strip.
- `_CockpitEmpty.cshtml` — empty-queue state. Inputs: icon, title.

Styling moves out of `Pages/Receiving/Index.cshtml`'s inline `<style>` block into `wwwroot/css/cockpit.css` keyed on `.cockpit__*` (matches existing class names — no rewrite needed). Design-system tokens (`--surface-card`, `--border-subtle`, `--cherry-red`, `--status-danger`) are already used.

The Cockpit primitives are **shape-only** and have no dependency on the Receiving domain. Subsequent v1 Control Centers (Sprints 13-18) compose them with their own queue adapter + their own preview adapter.

### D4 — Time-bucket lens is the default; role-specific lenses override

The default lens for every Cockpit is a four-bucket time grouping computed against the row's `RequiredAtUtc` / `DueAtUtc` / `EtaUtc` / equivalent:

| Bucket | Definition | Group tone |
|---|---|---|
| `overdue` | `Required < TodayLocal` | danger |
| `today` | `Required == TodayLocal` | warning |
| `this-week` | `TodayLocal < Required <= TodayLocal + 7d` | info |
| `later` | `Required > TodayLocal + 7d` OR `Required == null` | neutral |

Roles whose work is not time-driven (Maintenance priority × SLA, Inventory zone × count-age, Quality lot-status) override by providing a custom `ICockpitLens<TQueueRow>` implementation. The lens contract:

```csharp
public interface ICockpitLens<TQueueRow>
{
    string Code { get; }                 // "by-time", "by-priority", "by-zone"
    string Label { get; }                // "By Required Date"
    IReadOnlyList<CockpitGroup<TQueueRow>> Group(IReadOnlyList<TQueueRow> rows);
}

public record CockpitGroup<TQueueRow>(
    string Code,
    string Label,
    string Tone,                          // "danger" | "warning" | "info" | "neutral"
    string Icon,
    IReadOnlyList<TQueueRow> Rows);
```

A lens picker dropdown appears in the queue header when more than one lens is registered for the page. Default Receiving Cockpit ships with the time lens only; multi-lens land in Sprint 13+ where it's needed.

### D5 — Queue row contract: `ICockpitQueueRow`

Every Cockpit queue row implements a small interface so the primitives can render generically:

```csharp
public interface ICockpitQueueRow
{
    string Id { get; }                    // for selection + data-id attribute
    string Primary { get; }               // e.g., PO number, work order number
    string Secondary { get; }             // e.g., vendor name, asset name
    DateTime? RequiredAt { get; }         // drives the default time lens
    string Tone { get; }                  // "danger" | "warning" | "info" | "neutral"
    IReadOnlyList<MetaTriple> Meta { get; } // 1-3 small KV strips below the secondary line
}

public record MetaTriple(string Label, string Value, string? Tone = null);
```

Receiving's `PoQueueRow` adapts `PurchaseOrder` to this contract; ASN's `AsnQueueRow` adapts the ASN model; future Control Centers adapt their own root entity.

### D6 — Preview pane is JSON-hydrated, not server-rendered per row

The legacy Cockpit's hydration pattern (`<script id="__poDetails" type="application/json">` blob + `selectPO(id)` JS function) is the right choice and is preserved. Two reasons:

1. **Latency:** rendering the right pane on row click via an XHR/SignalR round-trip would add ~50-150ms per click. The legacy approach is essentially instant.
2. **Scale ceiling:** the JSON blob is fine up to ~500 queue rows. Beyond that we add server-side pagination + per-row XHR hydrate fallback. Sprint 12 ships the embedded-JSON path; the XHR fallback lands in the sprint that first exceeds the ceiling (Inventory cycle-counts, most likely).

A new helper `CockpitPreviewSerializer.Serialize<TPreview>(rows)` emits the JSON blob with `[JsonPropertyName]` snake_case + `null`-safe defaults. PII is opt-in via `[CockpitPreviewVisible]` — fields without the attribute are excluded.

### D7 — Voice posture: unchanged from ADR-016 §D3

Push-to-talk default, always-on opt-in, `<voice-action>` Tag Helper, voice-form-spec emit, `data-voice-key` attributes. The Cockpit preview pane participates in voice context — selecting a row updates the voice scope to that row's entity (matches Sprint 4 PR #1 D7 / D9). The voice tools shipped in Sprint 11 PR #4 (`IReceiptVoiceTools`) operate against the selected row's id when the Cockpit is active.

### D8 — Exception tab content: existing four-quadrant scaffold

The fourth tab `exceptions` renders the existing `_ControlCenterShell.cshtml`. No code changes to the shell itself. The Sprint 11 service methods (`GetExceptionLaneAsync`, `GetKpiStripAsync`, `GetActivityFeedAsync`) move under the Exceptions tab's view model. The screen-header KPI strip on the Cockpit tabs uses a **subset** of the same KPI tiles (the 4 most-actionable: Pending count, Today count, This Week count, Overdue count — exactly what the legacy Cockpit's `cockpit__welcome-stats` shows).

### D9 — Kill list addendum

ADR-016 §D6 already deprecates `/Admin/StockReceipts/Edit` Create. ADR-018 adds:

- `/Receiving/Cockpit-Legacy` is **kept for one sprint** (Sprint 12) as a fallback during the rebuild. After Sprint 12 closes, the legacy index file is deleted; its partials are already extracted to `Pages/Shared/Primitives/Cockpit/` per D3.
- The Sprint 11 four-quadrant scaffold page (`Pages/Receiving/ControlCenter.cshtml`) is **rewritten in place** as the four-tab shell. The `_ControlCenterShell` invocation moves under the Exceptions tab.
- `IReceivingControlCenterService` gets three new query methods (`GetPoQueueAsync`, `GetAsnQueueAsync`, `GetOrphanReceiptsAsync`). The existing methods stay. The state machine, idempotency, audit, and voice tooling stay. **Zero service contracts broken; only additions.**

### D10 — Sprint 12A + 12B + 12.5 sequence (locks the rebuild + depth + catch-up path)

**Revised 2026-05-18** per Dean's call to finish the Receiving pilot deeply before cloning the pattern. Purchasing CC moves out of Sprint 12 and becomes its own dedicated Sprint 13.

**Sprint 12A — Receiving Cockpit Rebuild (8 PRs, ~2 weeks).** Each PR independently mergeable + verifiable:

1. **PR #1 — ADR-018 ship.** This doc + the Cockpit-first pivot section in MASTER_PLAN.md. Docs-only.
2. **PR #2 — Cockpit primitives extract.** Move legacy Cockpit's partials + inline CSS into `Pages/Shared/Primitives/Cockpit/` + `wwwroot/css/cockpit.css`. Legacy `/Receiving/Cockpit-Legacy` still works (now using the extracted primitives). Compile-only PR; no behavior change.
3. **PR #3 — `ICockpitLens<TQueueRow>` + `ICockpitQueueRow` + `CockpitPreviewSerializer`.** Generic infrastructure under `Services/Navigation/Cockpit/`. Default `ByTimeLens<T>` implementation. Unit tests for bucket boundaries (overdue / today / week / later) including edge cases (midnight, timezone, null `RequiredAt`).
4. **PR #4 — `_CockpitTabShell.cshtml` + tab routing.** Generic four-tab shell partial. Tab state via `?tab=` query string. Lands `/Receiving` page as the consumer with PO Queue as default. Keyboard nav (J/K through queue, Enter focus preview, Esc back, arrow keys switch tabs).
5. **PR #5 — PO Queue tab on `IReceivingControlCenterService.GetPoQueueAsync`.** Time-bucketed `PendingPOs` adapted to `PoQueueRow : ICockpitQueueRow`. Preview pane port from legacy Cockpit. "Receive This PO" CTA → `/Receiving/By-Po/{id}`. Live-verified pixel-identical to legacy on Replit.
6. **PR #6 — ASN Queue tab.** New `GetAsnQueueAsync` returns `AsnQueueRow` keyed on ASN ETA windows. Preview pane shows ASN lines + carrier + ETA + expected items. CTA → `/Receiving/By-Asn/{id}`.
7. **PR #7 — Orphans tab.** New `GetOrphanReceiptsAsync` returns unmatched receipts with `MatchOrphanReceipt` AI-suggested PO column. CTA opens a "Match orphan" drawer.
8. **PR #8 — Exceptions tab cleanup + Cockpit-Legacy retirement.** Move existing `_ControlCenterShell` invocation under the Exceptions tab. `/Receiving/Cockpit-Legacy` deleted; redirect to `/Receiving?tab=po-queue`.

**Sprint 12B — Receiving DEPTH (6 PRs, ~2 weeks).** Finish the pilot before cloning the pattern to a second role.

1. **PR #1 — `MatchOrphanReceiptAsync` real implementation.** Replace Sprint 11 stub. AI-suggested PO matches from vendor + item + lead-time window. Logs to AuditLog with `ActorKind = Ai`, `AiConfidence`, `AiCommandText` (Purview pattern per ADR-014 D3).
2. **PR #2 — `OcrParseMillCertAsync` real implementation.** Replace Sprint 11 stub. Reuse Phase 2 OCR pipeline. Extracts heat number, mill, ASTM grade, chemistry, mechanicals from real mill-cert PDFs.
3. **PR #3 — `ReceiveByVoiceAsync` + `QuarantineByVoiceAsync` end-to-end.** Wire through `IdempotencyMediator` (ADR-014 D4) + state machine. Full voice receipt with zero screen touch. Quarantine by voice with reason + idempotency.
4. **PR #4 — DataWedge hardening on real Zebra TC52 / TC57.** Keystroke buffering for scan-burst, locale + numeric pad edge cases, CR/LF terminator robustness. GS1-128 + DataMatrix parsers tested against captured real-world barcodes.
5. **PR #5 — Mobile + Zebra handheld responsive optimization.** Cockpit primitives get responsive breakpoints. Touch targets, scan-first focus, single-column layout below ~768px. Designed for the dock worker's actual hardware.
6. **PR #6 — Receipts API endpoints (ERP integration thesis starts here).** `POST /api/v1/receipts` + `GET /api/v1/receipts/{id}` + outbound webhook on receipt-completed. SAP IDoc / Oracle REST / NetSuite SuiteTalk / D365 OData payload shapes documented in `docs/integrations/receipts-api.md`. The first step of the channels-into-customer-ERP positioning.

**Sprint 12.5 — Catch-Up Sprint (~2 weeks).** Five workstreams (foundation security, onboarding wizard, Admin v2 + a11y closeout, tech debt cleanup, i18n foundation). Full scope in `MASTER_PLAN.md` Priority 1.63.

**Sprint 13 — Full Purchasing Control Center (~8 PRs, ~3 weeks).** First non-Receiving Cockpit. Fully-built workspace, **not a pilot** — pattern validated by Receiving's production run. Folds in Sprint 3 backlog #129 (Vendor scorecards) + #132 (Blanket/Contract-PO). Full scope in `MASTER_PLAN.md` Priority 1.65.

---

## Consequences

**Win:**
- The daily-driver canvas matches how operators actually work (queue → detail). Receiving clerks can scan 30 POs/hour without modal churn.
- Cockpit primitives are reusable across all v1 Control Centers — Sprints 13-18 each become ~3-4 PRs (queue adapter + lens + preview adapter + flip registry row), not ~7 PRs.
- The four-quadrant scaffold stays useful (anomaly triage is real). It just isn't the front door anymore.
- The Sprint 11 service layer + voice tools + ADR-014 voice-ready infra are 100% reused. No rewrite waste.
- Performance ceiling is preserved (~100ms median page load, per PR #117.8) by keeping the JSON-blob hydration pattern.
- Pattern is best-in-class — no surveyed EAM/WMS competitor uses the queue + preview pane shape as their default receiving canvas.

**Cost:**
- The Sprint 11 page (`Pages/Receiving/ControlCenter.cshtml`) gets rewritten in place. The rewrite is ~150 lines and reuses the existing service + voice + primitives, but it's still a rewrite.
- The Sprint 11 four-quadrant scaffold demotes from "main canvas" to "Exceptions sub-tab." Discoverability of the exception lane drops — mitigated by the tab being labeled `Exceptions` with a count badge when non-empty.
- Sprint 12 absorbs the rebuild (~9-10 PRs). Sprints 13-18 each get a corresponding pattern-application PR.

**Risk:**
- The JSON-blob hydration scales to ~500 rows. Inventory cycle-counts and Shipping cut-offs may exceed this. **Mitigation:** XHR-hydrate fallback land in the sprint that first exceeds. Detection: page weight monitor + Lighthouse perf budget gate (~250KB max for the inline blob).
- The `ICockpitLens<TQueueRow>` abstraction is unproven across roles. **Mitigation:** PR #9 (Purchasing pilot) is explicitly a pattern-proof PR; refinements roll into ADR-018 §D11+ before Sprint 13 starts.
- The four-tab shell's keyboard nav (arrow keys to switch tabs, J/K within queue, Enter to focus preview, Esc to return to queue) is new behavior. **Mitigation:** PR #4 ships with explicit keyboard-nav tests (axe-core CI gate already in place for static checks; keyboard nav verified manually on live URL).

**Reversibility:**
- High. The Cockpit primitives are additive; the four-quadrant scaffold + service + voice tools are unchanged. If the Cockpit shape fails for a future role (Quality v2, AP/AR v2), that role's Control Center reverts to the four-quadrant scaffold as its default tab — config change, not architecture change. The two shapes coexist by design.

---

## Open questions (do not block sign-off)

1. **Multi-select on the queue?** Bulk-receive across 5 POs / bulk-quarantine across 3 receipts. Not in PR #5-#8 scope; consider for Sprint 13+ if user research surfaces the need.
2. **Saved filters / pinned rows on the Cockpit?** Pinned rows persist top-of-queue per user. ADR-017 §D2 already specced server-side Recent + Pinned (Nav-2). Roll into that PR.
3. **Right-pane → inline full-receive wizard?** Currently the "Receive This PO" CTA navigates to `/Receiving/By-Po/{id}`. Inlining the wizard as right-pane content (no navigation) is a UX win but doubles the page complexity. Defer to a Sprint 13+ AB test.
4. **Density toggle for the queue card?** The legacy Cockpit has one density; some operators prefer Bloomberg-density (more rows visible). Consider as a per-user setting under Settings → Display.
5. **Bucket boundaries per role.** The default time buckets (today / week / later) are right for Receiving; Maintenance may want today / 24h / 72h / week; Shipping may want carrier-cutoff-relative. PR #9 (Purchasing pilot) is the first divergence test.

---

## Sign-off

- [x] **Dean — 2026-05-18.** Greenlit the revised sequence after questioning whether two Control Centers in one sprint was the right call. Final lock:
  - **Sprint 12A** — Receiving rebuild (8 PRs)
  - **Sprint 12B** — Receiving DEPTH (6 PRs: wire 4 stubbed voice tools, harden DataWedge on Zebra, mobile/handheld optimization, Receipts API for ERP integration). *Originally drafted as "Full Purchasing CC"; Dean's call moved Purchasing out of Sprint 12 to finish the pilot deeply first.*
  - **Sprint 12.5** — Dedicated MASTER_PLAN catch-up (MFA + SSO + RLS + Onboarding wizard + Admin v2 + a11y form-labels + sparse-page redesigns + tech-debt cleanup + i18n foundation)
  - **Sprint 13** — Full Purchasing CC (8 PRs, folds in Sprint 3 modules #129 Vendor scorecards + #132 Blanket/Contract-PO)
  - **Sprints 14-18** — Maintenance / Planning / Scheduling / Inventory / Shipping (Sprint 3 premium modules folded by domain)

Specifically confirmed:
- The four-tab shell (PO Queue · ASN Queue · Orphans · Exceptions) matches the mental model.
- The Cockpit pattern (queue left + preview right + KPI tile strip header + welcome state) is the daily-driver canvas for all v1 Control Centers.
- The PO Queue tab is **pixel-identical** to the legacy `/Receiving/Cockpit-Legacy` — the rebuild is extraction-not-redesign.
- Sprint 12A's 8-PR sequence is the right rebuild path.
- The Sprint 11 build is reusable substrate, not waste.
- **One perfect ship before cloning.** Receiving pilot gets 4 weeks of dedicated polish (12A + 12B) before any second Control Center work.
- Foundation security (MFA/SSO/RLS) lands BEFORE Purchasing CC, in the dedicated Sprint 12.5 catch-up.
- Purchasing CC is its own dedicated Sprint 13 — fully-built, not a pilot, retires two Sprint 3 backlog modules in the process.

Sprint 12A PR #1 (this ADR + the MASTER_PLAN pivot block) ships immediately. Sprint 12A PR #2 (Cockpit primitives extract) starts next.
