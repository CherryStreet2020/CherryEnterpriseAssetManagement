# ADR-016 — Control Center Pattern + Receiving Pilot Spec

**Status:** Proposed — awaiting Dean sign-off
**Date:** 2026-05-18
**Author:** Architecture (Claude)
**Supersedes:** N/A
**Builds on:** ADR-014 (Phase F Voice-Ready Foundation), ADR-015 (Industry-Agnostic Receipt Schema), Sprint 3.5 design-system primitives
**Reverses:** The `/Admin/StockReceipts/Edit` blank-form Create entry shipped in PR #219 (Wave 1 PR #5). Edit remains as admin fix-up only; the sidebar link is removed.
**Research:** [`docs/research/receiving-control-center.md`](research/receiving-control-center.md) (1,152 lines, 30 verified sources, May 2026 incumbent screens)

---

## Question

CherryAI EAM needs a UI shape that reflects how the manufacturer-customer's plant employees actually work — Receiving Clerk, Buyer, Maintenance Tech, Planner, Scheduler, QC Inspector, AP Clerk, Shipping Clerk, HR. Today every screen is either a CRUD admin table or a sparse detail page. The receiving job, the maintenance dispatch board, the AP three-way-match exception lane — none of those fit a CRUD shape.

**Two decisions are required:**

1. **What is the standard workspace shape** for a role-based experience that delivers incredible visibility, super-intuitive functionality, and AI-controlling-as-much-as-possible — repeatable across 10 roles without 10 bespoke rewrites?
2. **What is the spec for the first one** — Receiving Control Center as the PILOT — locked tightly enough that build can start without further architectural debate?

---

## State of practice (research-validated)

Full survey in [the research doc](research/receiving-control-center.md). Compressed findings:

- **No incumbent or modern challenger has a credible voice-AI receiving experience as of May 2026.** SAP MIGO, Oracle Fusion Cloud Receipt Routing, NetSuite WMS Mobile, D365 F&O Arrival Overview + Quarantine Orders, Plex receiving, Epicor Kinetic Mass Receipt, Acumatica Receiving Dashboard — all are blank-form-driven or paper-form-driven with bolt-on barcode. None expose a voice-form-spec layer. None have profile-driven attribute rendering. None have an AI tool surface backed by a properly-audited mediator. The category is greenfield.
- **The Control Center grammar comes from outside ERP.** Bloomberg Terminal (information density without chaos), Linear (calm prioritization, keyboard-first, Cmd-K), NASA mission control (role-based stations, single big board), Datadog and Grafana (sparkline KPI tiles, click-to-drill), airline operations centers (exception lanes with automated routing recommendations + human-in-the-loop confirmations), Stripe Dashboard (right-rail drawer for detail, financial calm). None of these moves is reachable for SAP / Oracle / NetSuite / D365 by incremental UI refactor — their architecture was set before voice, before AI, before profile-driven attributes were tractable.
- **The receiving workflow reality is 4-shaped + 7 long-tail.** ~80% of real receipts are PO-driven; ~10% ASN-driven; ~5% blind; ~5% partial. The other 7 (Over, Damaged, Quarantine, Returns, Cross-dock, Drop-ship, Consignment) are real but lower-frequency. Pilot ships the four high-frequency shapes; the others land in v2.
- **Dock workers run on Zebra TC52/TC57 handhelds running Android with DataWedge keystroke output.** That is the hardware target. The "responsive-down-from-desktop" trap is what made NetSuite WMS the most-replaced module in its category. Desktop and Zebra-handheld are two distinct surfaces sharing one service layer.
- **Measurement alone moves dock-to-stock 35-50% in 30 days.** The KPI strip is a behavior-modification surface before it is a reporting surface. Eight tiles is the right count (Bloomberg, Datadog, Linear converge on 6-10).

---

## Decisions

### D1 — Control Center pattern: four-quadrant scaffold

Every Control Center renders the same four quadrants on the design-system primitives:

1. **KPI Strip** (top, 8 tiles) — `KpiStripPrimitive`. Each tile: large numeric, sparkline, color-coded delta vs target, click-to-drill. Live-updates via SignalR.
2. **Exception Lane** (center-left, sortable list) — `ExceptionLanePrimitive`. The role's "what needs me right now" feed. Ranked by AI priority. Keyboard-navigable (J/K, Enter open, Esc close).
3. **Detail Drawer** (right rail, ~480px wide) — `DetailDrawerPrimitive`. Opens on row click without losing list context. Body is profile-aware via ADR-015 `DynamicFormViewComponent`.
4. **Activity Feed** (bottom, collapsible, ~120px tall) — `ActivityFeedPrimitive`. Real-time stream of receipts/exceptions/voice actions in the role's scope. Bloomberg-density, low-chrome.

A new shared layout `_ControlCenterLayout.cshtml` wraps all four. Each Control Center page (Receiving first) injects role-specific KPI definitions, exception ranking rules, drawer content, and activity-feed filters via a `ControlCenterPageModel : VoiceReadyPageModel` base class.

### D2 — Route lock: `/Receiving` is the Control Center landing

- `/Receiving` — main Control Center landing. **Default behavior:** opens the exception lane sorted by AI priority.
- `/Receiving/Receipt/{id}` — deep-link into a receipt detail. Renders as drawer over `/Receiving` for in-app navigation; renders as full page when accessed directly. Deep-link state is preserved (back button works).
- `/Receiving/By-Po/{poId}` — receive-against-PO wizard (the 80% workflow).
- `/Receiving/Blind` — blind-receive flow (the 5% workflow).
- `/Receiving/Exceptions/{kind}` — exception-lane filter views.

Sidebar nav swaps "Stock Receipts" → "Receiving" with the new icon.

### D3 — Voice posture: push-to-talk by default, always-on opt-in

Always-on listening is the right destination but the wrong default. Push-to-talk (spacebar hold, or microphone button click) is the safe default for Phase 1. Always-on is a per-user setting under "Receiving preferences," disabled by default. The voice substrate is ADR-014 D7 + D10 (`<voice-action>` Tag Helper, `voice-form-spec` JSON blob, `data-voice-key` attribute on every input).

### D4 — Profile-aware drawer body: reuse ADR-015 `DynamicFormViewComponent`

The right-rail detail drawer renders the receipt body via the same View Component already shipped for `/Admin/StockReceipts/Edit`. No new dynamic-form layer. Profile-driven validation already lives in `ReceiptAttributesValidator` (`JsonSchema.Net 7.3.0`) + `JsonPointerToModelKey.Translate` — both reused without modification.

This means: switching profiles in the drawer's profile-picker re-renders the entire body. Voice form spec re-emits. Validation rules update. Zero code changes per profile.

### D5 — Hardware focus mode: Zebra TC52/TC57 DataWedge-compatible

The scan field on `/Receiving` auto-focuses on page load and re-focuses after every successful scan (CR/LF-terminated keystroke output from DataWedge). Scan input is matched against a small finite state machine: PO number → ASN ID → Lot/Heat → Serial → Item code. The matched type drives the next action (open PO wizard / open ASN line / lookup by lot / etc.).

GS1-128 and DataMatrix parsing follows GS1 application-identifier syntax. Mobile devices use the same scan field; native iOS/Android camera scan is deferred to Sprint 6 mobile work.

### D6 — Kill list: `/Admin/StockReceipts/Edit` Create entry

- Sidebar link "Stock Receipts" replaced with "Receiving" pointing at `/Receiving`.
- `/Admin/StockReceipts` index page kept for admin browse, but the "+ New" button is removed.
- `/Admin/StockReceipts/Edit` GET with `?id=0` (Create mode) returns 302 to `/Receiving/By-Po` (sidebar nav cannot reach it).
- `/Admin/StockReceipts/Edit?id={existing}` (Edit mode) stays. Admin fix-up only. Locked under `Admin` policy. Documented as "admin override" in the help text.
- The PostgresException 22P02 JSON bug on the Edit page (the one that fired the Control Center pivot conversation) is NOT being fixed — the page being killed is the page being broken. Resources move forward.

### D7 — Service layer: `IReceivingControlCenterService`

New service interface in `Services/Receiving/`. Methods:

- `GetExceptionLaneAsync(string role, ExceptionFilter filter, CancellationToken ct) → Task<ExceptionLanePage>`
- `GetKpiStripAsync(string siteId, DateRange range, CancellationToken ct) → Task<KpiStripSnapshot>`
- `GetActivityFeedAsync(string role, int sinceSeq, CancellationToken ct) → Task<ActivityFeedDelta>`
- `ReceiveByPoAsync(ReceiveByPoCommand cmd, IdempotencyKey key, CancellationToken ct) → Task<Result<ReceiveResult>>`
- `ReceiveByAsnAsync(ReceiveByAsnCommand cmd, IdempotencyKey key, CancellationToken ct) → Task<Result<ReceiveResult>>`
- `BlindReceiveAsync(BlindReceiveCommand cmd, IdempotencyKey key, CancellationToken ct) → Task<Result<ReceiveResult>>`
- `QuarantineAsync(QuarantineCommand cmd, IdempotencyKey key, CancellationToken ct) → Task<Result<QuarantineResult>>`
- `MatchOrphanReceiptAsync(string receiptId, string poId, IdempotencyKey key, CancellationToken ct) → Task<Result<MatchResult>>`

All command methods go through `IdempotencyMediator` (ADR-014 D4). All mutations audit-log via `AuditService.LogAsync` with a flat DTO (never live EF entities — captured pitfall). `Result<T>` is the return contract per ADR-014 D2.

### D8 — Voice tool surface: 10 `IReceiptVoiceTools`

Four stubs already exist from ADR-015 D10:

1. `TraceChainOfCustody(string lotOrHeat, CancellationToken ct)`
2. `ListExpectedReceipts(string vendorId, DateRange range, CancellationToken ct)`
3. `QuarantineByFilter(QuarantineFilter filter, CancellationToken ct)`
4. `LookupReceipt(string receiptIdentifier, CancellationToken ct)`

Six new tools land with the Receiving Control Center:

5. `ListExpectedArrivalsAsync(string siteId, DateRange today, CancellationToken ct)` — combines open POs, ASNs, carrier-tracking ETAs, historical lead-time means.
6. `MatchOrphanReceiptAsync(OrphanReceiptHints hints, CancellationToken ct)` — AI guesses 3 candidate POs from vendor + item + window.
7. `ExplainExceptionAsync(string receiptId, CancellationToken ct)` — natural-language summary of why the receipt is on the exception lane.
8. `ReceiveByVoiceAsync(VoiceReceiveCommand cmd, CancellationToken ct)` — full receipt by voice, no screen touch.
9. `QuarantineByVoiceAsync(VoiceQuarantineCommand cmd, CancellationToken ct)` — voice-driven quarantine with reason.
10. `OcrParseMillCertAsync(byte[] pdfBytes, string profileCode, CancellationToken ct)` — extract heat number, mill, ASTM grade, chemistry, mechanicals from a mill-cert PDF. Reuses the Phase 2 OCR pipeline.

Each tool's invocation logs to `AuditLog` with `ActorKind = Ai`, `AiToolName = <tool>`, `AiConfidence`, `AiCommandText` (Purview pattern per ADR-014 D3). Each tool's permission check uses `IAuthorizationService.AuthorizeAsync` resource-based with the invoking user's identity — AI never gets its own identity (ADR-014 D6).

### D9 — KPI strip: 8 tiles, ship with these on day one

1. **Dock-to-stock time** — median minutes from truck-arrival event to inventory-available event. Sparkline 14d. Target: <90 min.
2. **Receiving accuracy** — % of receipt lines with `quantity_received == quantity_expected` within tolerance. Sparkline 14d. Target: 98%.
3. **Open exceptions** — count of receipts on the exception lane. Number with severity color. No sparkline.
4. **Doc completeness** — % of receipts with all `PromotedFacets` populated for their profile (already wired in ADR-015 Migration #3). Sparkline 14d. Target: 95%.
5. **Supplier on-time** — % of receipts arriving within scheduled window. Sparkline 30d. Target: 90%.
6. **Quarantine cycle time** — median hours from quarantine-set to quarantine-clear. Sparkline 14d. Target: <24h.
7. **ASN penetration** — % of receipts arriving with an ASN. Sparkline 30d. Target: 75%.
8. **Voice/scan adoption** — % of receipts created via voice or scan vs typed. Sparkline 14d. Target: 60%.

Tiles are configurable per site in v2 (Sprint 12+). v1 ships with these 8 as defaults.

### D10 — Desktop-first, iPad-friendly companion, Zebra/Android deferred

Pilot ships:

- Desktop (≥1280px viewport): four-quadrant Control Center scaffold, full keyboard shortcuts, push-to-talk voice, drawer detail.
- iPad / tablet (768px-1279px): four-quadrant collapses to a two-pane (exception lane + drawer); KPI strip becomes horizontal scroll; activity feed becomes a tap-expand sheet.
- Zebra TC52/TC57 (≤480px, Android Chrome, DataWedge scan input): single-pane scan-first flow at `/Receiving/Scan`. Not the full Control Center — a focused dock-worker companion that hands off to the desktop Control Center for everything beyond scan-receive.

Native iOS / Android receiver app is deferred to Sprint 6 (Mobile v1).

### D11 — Control Center scaffold is the template for Sprints 12-20

The four primitives shipped in PR #2 of Sprint 11 (`KpiStripPrimitive`, `ExceptionLanePrimitive`, `DetailDrawerPrimitive`, `ActivityFeedPrimitive`) plus the shared layout (`_ControlCenterLayout.cshtml`) plus the base page model (`ControlCenterPageModel`) become the scaffold. Subsequent Control Centers (Purchasing, Maintenance, Planning, Scheduling, Inventory, Quality, Shipping, AP/AR, HR) inherit the scaffold and provide:

- A role-specific KPI definition set
- A role-specific exception ranking function
- A role-specific detail-drawer content component
- A role-specific activity-feed filter
- A role-specific voice tool set
- A role-specific service interface

Each subsequent Control Center should land in ~3-4 PRs vs Receiving's ~7 PRs. This is the disruption thesis — once Cherry Street has the scaffold, we outpace SAP/Oracle/NetSuite/D365/Plex by an order of magnitude on per-role workspace coverage.

### D12 — Receiving workflows in v1: PO + ASN + Blind + Partial

Four shapes ship in the pilot:

1. **PO-driven** (`/Receiving/By-Po/{poId}`) — the 80% case. Operator picks PO → confirms expected quantity per line → scans pallet/lot/heat → confirms putaway suggestion → posts.
2. **ASN-driven** (`/Receiving/By-Asn/{asnId}`) — the 10% case. EDI 856 already in the system → operator scans ASN barcode → confirms or amends.
3. **Blind receive** (`/Receiving/Blind`) — the 5% case. No PO, no ASN. Operator records what's delivered → orphan receipt → `MatchOrphanReceiptAsync` AI tool offers 3 candidate POs.
4. **Partial receipt** — modifier on PO-driven and ASN-driven flows. Quantity-received < quantity-expected → balance held open on the PO line → exception lane tracks partial-open age.

Seven shapes deferred to v2 (Sprint 12+): Over-receipt, Damaged-on-arrival, Quarantine (drawer action exists, but the dedicated workflow is v2), Returns inbound (RMA), Cross-dock, Drop-ship logging, Consignment receipt. These are real but lower-frequency; pilot scope stays tight.

---

## Consequences

**Positive:**

- The Control Center scaffold is repeatable. Sprints 12-20 inherit ~70% of Sprint 11's code.
- Voice and AI are baked in from PR #1, not retrofit. ADR-014 + ADR-015 substrate is already paying off — no new infrastructure needed for the Receiving Control Center to be voice-ready.
- The kill of `/Admin/StockReceipts/Edit` Create entry removes a half-finished surface and resolves the open PostgresException 22P02 issue by deletion, not patch.
- Procurement-sales differentiator: "the only manufacturing ERP shipping a role-based Control Center per plant role, voice-first, profile-aware, WCAG 2.1 AA verified-in-CI" is a claim no competitor can match in 2026.

**Negative / cost-of-doing:**

- Sprint 4 Phase F Wave 1 PR #5 (StockReceipts admin) was shipped and is now superseded by Sprint 11 PR #1-#7. The work is not wasted — the service layer + voice infra + profile-aware rendering is all reused. But the page itself is being unlinked from sidebar nav 14 days after ship.
- Four shapes ship in v1; seven shapes deferred. Customers asking for cross-dock or consignment will be told "v2" until Sprint 12+ closes the gap.
- Push-to-talk default trades a "wow" demo moment (always-on) for safer real-world rollout. Acceptable — always-on is a flip-switch upgrade in v2.

**Risk:**

- Hardware integration depth is a known unknown. DataWedge keystroke output is mature, but specific GS1 application-identifier handling varies by scanner config. Mitigation: ship a hardware-config admin page in Sprint 11 PR #6.
- Voice latency on real plant networks could be 2-4 seconds. Push-to-talk default makes this acceptable; always-on would not.
- The 70%-configuration claim for Sprints 12-20 is a target, not a guarantee. The first follow-on Control Center (Sprint 12 Purchasing) will validate or invalidate the claim. If validation fails, the scaffold gets refactored before Sprint 13.

---

## Migration plan

**Sprint 11 ships in 7 PRs:**

| PR | Scope | Pre-reqs |
|---|---|---|
| #1 | This ADR-016 doc + research doc finalize | Research doc done; this doc pending sign-off |
| #2 | Four-quadrant scaffold primitives + `_ControlCenterLayout.cshtml` + `ControlCenterPageModel` base | Design-system primitives (shipped) |
| #3 | `IReceivingControlCenterService` + service implementation + state machine for the 4 workflows | ADR-014 idempotency mediator (shipped); ADR-015 schema (shipped) |
| #4 | 10 `IReceiptVoiceTools` (4 existing stubs implemented + 6 new) | Service layer (PR #3) |
| #5 | `/Receiving` landing page wiring all four quadrants + KPI computations | Primitives (PR #2), service (PR #3) |
| #6 | `/Receiving/By-Po` PO-driven wizard + `/Receiving/By-Asn` + `/Receiving/Blind` + hardware focus mode + GS1-128 parser | Service (PR #3) |
| #7 | Kill list: sidebar swap, `/Admin/StockReceipts/Edit` Create 302, admin-only badge, sparse-page unlinking | All previous PRs |

Each PR ships through the standard `cowork-github-replit-process` workflow: edit-on-Mac → `osascript` push → `gh pr create` → `gh pr checks` green → `gh pr merge --squash --delete-branch` → Replit Shell pull → Replit Agent restart → live-verify → ship comment.

**Build target:** 7 PRs in 5-7 calendar days. Each PR ≤500 LOC where possible. Bigger PRs (#3 service layer, #4 voice tools) may exceed but stay under 1000 LOC.

---

## Open questions for Dean

1. **Drawer vs full page for receipt detail.** Recommendation: drawer for in-app navigation; full page for deep-link / external access. Confirm?
2. **Voice always-on vs push-to-talk default.** Recommendation: push-to-talk default with per-user always-on opt-in setting. Confirm?
3. **ASN ingestion in pilot or deferred.** Recommendation: include in pilot — EDI 856 parser already on the Sprint 3 backlog and the substrate is straightforward. Confirm?
4. **BOL signature capture in pilot or deferred.** Recommendation: defer to v2. Pilot ships paper-signed BOL with photo capture; digital signature is a 2-PR add in Sprint 12+.
5. **Cross-dock, drop-ship, consignment in v1 or v2.** Recommendation: v2 (per D12 above). Pilot stays focused on the 95% case.
6. **Big-board / supervisor TV view in v1 or v2.** Recommendation: v2. The Control Center is built for individual contributors first; the TV view is a layout variant on top of the same data layer.
7. **Naming.** "Receiving Control Center" everywhere in code and routes (`/Receiving`)? Or do we want a different label? Confirm "Receiving" is the public-facing name.

---

## Decision log

| Date | Who | What |
|---|---|---|
| 2026-05-18 | Dean (vision) + Claude (proposal) | Control Center pattern locked; Receiving pilot scope locked. Awaiting sign-off. |

---

## References

- [Research doc — Receiving Control Center](research/receiving-control-center.md) (1,152 lines, May 2026)
- [ADR-014 — Phase F Voice-Ready Foundation](ADR-014-phase-f-ui-and-voice-readiness.md)
- [ADR-015 — Industry-Agnostic Receipt Schema](ADR-015-industry-agnostic-receipt-schema.md)
- [Research doc — Industry-agnostic receipt schema](research/industry-agnostic-receipt-schema.md)
- [Research doc — Voice-AI spike ADR-015 D10](research/voice-ai-spike-adr015-d10.md)
- [Memory — Control Center pattern](../memory/project_control_center_pattern.md)
- Sources for cross-industry pattern grounding listed in [receiving-control-center_sources.md](research/receiving-control-center_sources.md)
