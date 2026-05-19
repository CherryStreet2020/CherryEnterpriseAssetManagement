# ADR-017 — Control-Center-First Information Architecture

**Status:** Proposed — awaiting Dean sign-off
**Date:** 2026-05-18
**Author:** Architecture (Claude)
**Supersedes:** N/A
**Builds on:** ADR-016 (Control Center pattern + Receiving spec)
**Reverses:** The data-table-centric sidebar shape that's been the de facto IA since Sprint 0.
**Research:** [`docs/research/navigation-information-architecture.md`](research/navigation-information-architecture.md) (557 lines, 36 sources)

---

## Question

After Sprint 11 we shipped the Receiving Control Center, the four-quadrant scaffold, the service layer, ten voice tools, three workflow pages, and the kill-list route swap — and **nothing in the sidebar reveals any of it.** `/Receiving/By-Po`, `/Receiving/By-Asn`, and `/Receiving/Blind` have zero nav entries. `/Admin/StockReceipts`, `/Admin/RegulatoryProfiles`, `/Admin/MaterialMasters` are similarly orphaned. The sidebar today exposes 7 groups and ~55 items organized around data tables (Items, Vendors, Purchase Orders, Receiving, Stock Levels, Kits) — pre-Control-Center thinking that does not survive ADR-016.

**Two questions need a decision before sidebar code lands:**

1. **What is the right top-level IA** for a manufacturing-operations product whose primary surfaces are now role-based Control Centers rather than data-table CRUD?
2. **Which Control Centers are in v1** (and visible in the sidebar) vs deferred to v2?

---

## State of practice (research-validated)

Full survey in [the research doc](research/navigation-information-architecture.md). Compressed findings:

- **Role-based primary nav beats data-table-based nav at scale.** Linear, Stripe, Salesforce Lightning, Oracle Redwood, and Plex all front-load roles / workspaces / control surfaces; data tables are second-class. The data-table-centric pattern (Notion, Datadog, parts of NetSuite) works under 50 pages and degrades hard above 150 — CherryAI has ~250 Razor pages today.
- **7±2 top-level groups, ~5 items each, max 2 levels deep** is the sustainable density. Every reference tool stays inside this envelope. Today's sidebar has 7 groups but ~55 items — too dense in the data-table groups and too sparse in the role groups.
- **Cmd-K command palette absorbs the long tail.** Already shipped in CherryAI (PR #116d.1c). Anything not in the sidebar is one keystroke away.
- **Settings / admin pages belong in a separate drawer, not the operational sidebar.** Stripe, Linear, GitHub, Notion all do this. Our 14 admin items in the sidebar are sidebar real estate stolen from operational surfaces.
- **Inbox flows belong in topbar bells, not sidebar items.** Approvals is an inbox flow; Linear / Stripe / GitHub / Notion all use a bell + badge count.
- **Recent + Pinned are cheat codes.** Linear's command palette + workspace tree pattern proves the model: auto-populated recents + user-curated pins outperform any fixed sidebar structure at predicting "what does this user want next."

---

## Decisions

### D1 — Control Centers are the spine of the sidebar

A new **"Control Centers"** group at the top of the sidebar (under Dashboard) lists every role-workflow surface. The currently-shipped Receiving Control Center is active and expandable. The five not-yet-shipped Control Centers in the v1 launch scope are visible-but-disabled with `(soon — Sprint N)` chips. This is the structural inversion ADR-016 §D11 set up: role-first nav, data-table nav demoted.

### D2 — v1 launch scope is seven Control Centers

Per Dean's 2026-05-18 scope lock, the v1 sidebar lists seven Control Centers in this order:

| # | Control Center | Status | Route | Notes |
|---|---|---|---|---|
| 1 | Receiving | Live (Sprint 11) | `/Receiving` | The pilot, already shipped |
| 2 | Purchasing | Sprint 12 | `/Purchasing/ControlCenter` (placeholder) | Open POs, supplier scorecards, expediting queue |
| 3 | Maintenance | Sprint 13 | `/Maintenance/ControlCenter` | WO queue, dispatch, asset health rollup |
| 4 | Planning | Sprint 14 | `/Planning/ControlCenter` | Demand→MPS→MRP exception lane |
| 5 | Scheduling | Sprint 15 | `/Scheduling/ControlCenter` | Finite-capacity Gantt, machine queue |
| 6 | Inventory | Sprint 16 | `/Inventory/ControlCenter` | Cycle-count queue, stock-location heatmap |
| 7 | Shipping | Sprint 18 | `/Shipping/ControlCenter` | Open SOs, carrier dock-time, BOL queue |

**Three Control Centers from ADR-016 §D11 defer to v2 (post-launch):** Quality, AP/AR, HR/Crew. They are not in the v1 sidebar. The MASTER_PLAN will mark Sprints 17, 19, 20 as "POST-LAUNCH v2."

### D3 — Positioning shift: EAM + Manufacturing Operations, not full ERP

The scope cut reflects a strategic positioning shift Dean locked alongside this ADR. CherryAI v1 is positioned as **EAM + Manufacturing Operations disruption** against IBM Maximo / Infor EAM / Fiix / UpKeep / Plex — not as a SAP / Oracle / NetSuite / D365 ERP replacement. CherryAI **channels into** the customer's existing financial ERP for finance, AP/AR, HR. The plant runs CherryAI for daily ops; CherryAI emits events that flow into the customer's ERP. This is the "best of both worlds" sale — no IT war about end-of-life dates, faster pilot-to-close.

Implication for the sidebar: the absence of Quality / AP/AR / HR Control Centers is intentional and on-thesis. Sales conversations should not present those as "missing"; they're "your existing ERP keeps doing what it does well."

### D4 — Visible-but-disabled placeholders for Sprints 12-18

The five not-yet-shipped Control Centers in v1 scope (Purchasing, Maintenance, Planning, Scheduling, Inventory, Shipping) are visible in the sidebar with a disabled state and a `(soon — Sprint N)` chip. Sprint 18 Shipping is included but gets a `(coming with Sprint 18)` chip since it ships last.

Rationale: roadmap transparency for prospect demos, sidebar shape stability across the next five sprints (no IA churn when Purchasing ships — its line just lights up), and the Linear / Stripe / Salesforce pattern of showing "coming soon" surfaces rather than hiding them.

### D5 — Quick Actions tray (NEW; verb shortcuts)

A second new group **"Quick Actions"** lists the five highest-frequency cross-cutting verbs:

- Create work order
- Create requisition
- Create receipt (blind)
- Add asset
- Log inspection

Each opens a focused create-flow page or modal — not a table. The slot houses verbs across roles; future sprints add Reconcile, Reopen, Reassign without renaming the group. "Quick Actions" beats "Create" as a label because not every future verb is a creation (Reassign is move; Reconcile is match).

### D6 — Operations group consolidates today's data-table groups

The five data-table-centric groups today (Assets, Finance, Materials & Purchasing, Work Management, Projects) compress to a single **"Operations"** group with five child groups inside it. Each child is one expandable section:

| Operations sub-group | Contents |
|---|---|
| Assets | Asset Register · Plant Floor · Locations · Categories · Bulk Operations · Barcodes |
| Work | Work Orders · Work Requests · PM Templates · PM Schedules · Schedule Board · Technicians |
| Materials | Items / Parts Catalog · Vendors · Purchase Orders · Requisitions · Inventory · Stock Levels |
| Projects | CIP Projects · Cost Tracking · Cost Analytics |
| Finance | Depreciation Books · GL Accounts · Journals · Period Close · Accounts Payable · Reports · US Tax · Canadian CCA |

Group-of-groups pattern (Linear's hierarchy + Stripe's collapsible sub-nav). Top-level density stays low; the operator who needs to dig still gets there in 2 clicks.

### D7 — Platform group consolidates AI + integrations

A single **"Platform"** group with **"AI & Voice"** as its primary entry — the existing AI Assistant page plus tabs inside for API Hub, Webhooks, Webhook Deliveries, Event Catalog, Outbox, Integration Hub (today's six separate sidebar items). This is the Stripe / Linear pattern of one landing surface with internal tabs rather than six sibling sidebar items.

### D8 — Approvals moves to a topbar bell

Pending Approvals leaves the sidebar entirely. A 🔔 bell icon in the footer/topbar utility row gets a numeric badge for the user's pending approvals count. Clicking opens an inbox-style overlay or navigates to `/Approvals/Pending`. Standard inbox-flow pattern across Linear / Stripe / GitHub / Notion.

### D9 — Settings drawer replaces today's Administration group

The current ~30 admin items in the sidebar move to a single **⚙ Settings** entry in the footer utility row. It opens a settings page with its own left sub-nav:

| Settings sub-group | Items |
|---|---|
| Organization | Sites · Companies · Departments · Cost Centers · Manufacturers · Project Managers |
| Users & Access | Users & Roles · Permissions |
| Master data | Item Categories · Asset Categories · Kits · Lookups · GL Accounts · Fiscal Calendar |
| Data & Integration | Data Management · Webhooks · Event Catalog · Outbox · Integration Hub · Audit Log |
| System | System Settings |

Mirrors Stripe's gear icon, Linear's `/settings/*`, GitHub's `/settings/*`. Cuts ~14 sidebar items.

### D10 — Recent + Pinned at the bottom of the sidebar

Two auto-populated sections below the static groups:

- **Recent** — last 5 entities visited (PO, asset, receipt, WO), persisted server-side per user, refreshed on every detail-page visit.
- **Pinned** — user-curated; right-click or star-button on any entity adds it. Persisted per user.

Today's `recentNavSection` (line 506 of `_ModernLayout.cshtml`) is the foundation — it currently uses localStorage and lists page routes. The upgrade is (a) server-side persist, (b) entity-scoped not route-scoped, (c) Pinned as a sibling.

### D11 — Cmd-K command palette is the long-tail safety net

Already shipped (PR #116d.1c). Anything removed from the sidebar (kits, audit log forensic, lookup tables) is one keystroke away. The sidebar can be ruthlessly pruned because the palette catches everything.

### D12 — Backward-compat: every existing deep-link still works

The sidebar rebuild reorganizes how things are *discovered*. It does not change route URLs. Every page in CherryAI today keeps its current route after the rebuild. Bookmarks, email links, voice-tool deep-links all continue to work.

---

## The new sidebar — screen-in-words

```
┌─────────────────────────────────────────────────────┐
│  [ABS] EAM                                          │
│        © Powered by CherryAI                        │
│  [Org: Cherry Forge Indianapolis ▼]                 │
│  [Site: Plant 2 — Indy South ▼]                     │
├─────────────────────────────────────────────────────┤
│  ⌂  Dashboard                                       │
│                                                     │
│  CONTROL CENTERS                                    │
│  ▼ ⊕  Receiving        ● live                       │
│    ▸ Receive by PO                                  │
│    ▸ Receive by ASN                                 │
│    ▸ Blind receive                                  │
│    ▸ Receipt history                                │
│  ▸ ⊕  Purchasing       (soon — Sprint 12)           │
│  ▸ ⊕  Maintenance      (soon — Sprint 13)           │
│  ▸ ⊕  Planning         (soon — Sprint 14)           │
│  ▸ ⊕  Scheduling       (soon — Sprint 15)           │
│  ▸ ⊕  Inventory        (soon — Sprint 16)           │
│  ▸ ⊕  Shipping         (soon — Sprint 18)           │
│                                                     │
│  QUICK ACTIONS                                      │
│    +  Create work order                             │
│    +  Create requisition                            │
│    +  Create receipt (blind)                        │
│    +  Add asset                                     │
│    +  Log inspection                                │
│                                                     │
│  OPERATIONS                                         │
│  ▸ Assets                                           │
│  ▸ Work                                             │
│  ▸ Materials                                        │
│  ▸ Projects                                         │
│  ▸ Finance                                          │
│                                                     │
│  PLATFORM                                           │
│  ▸ AI & Voice                                       │
│                                                     │
│  RECENT                                             │
│    PO 4500178823                                    │
│    Asset PUMP-204                                   │
│    Receipt R-2026-04412                             │
│                                                     │
│  PINNED                                             │
│    Schedule Board                                   │
│    Period Close                                     │
├─────────────────────────────────────────────────────┤
│  🔍 Search (⌘K)  ☀/🌙  🔔 Approvals  ❓  ⚙ Settings │
└─────────────────────────────────────────────────────┘
```

Top-level density: 4 groups (Control Centers, Quick Actions, Operations, Platform) + Dashboard + Recent + Pinned = 7 sections. Inside the envelope.

---

## Consequences

**Positive:**

- Every Sprint 11 surface becomes findable.
- The Control Center pattern is the FIRST thing a new user sees.
- Sprints 12-18 deliverables slot in with one line of Razor each — no IA churn.
- Sidebar item count drops from ~55 to ~30 (about 45% reduction).
- Settings drawer + Cmd-K absorb the long tail; no power-user is worse off.
- Approvals bell + Recent/Pinned upgrade the daily-driver UX for repeat users.
- The launch-scope cut signals "we know what we are" — sharper sales conversation against incumbents.

**Negative / cost-of-doing:**

- Every existing sidebar bookmark / muscle-memory click breaks. ~1 sprint of "where did X go?" support questions.
- Admin users who lived in the sidebar's Administration group have to learn to click ⚙ Settings instead. Documented in the help center.
- The visible-but-disabled placeholders look "broken" if the tooltip is missed. Mitigation: subtle sprint chip + tooltip on hover + cursor change.

**Risk:**

- Prospect demos see disabled Control Centers and ask "is this real?" Mitigation: the rollover tooltip + the consistent sprint chip make the roadmap obvious. Counter-pitch: "Yes, the roadmap is right here; here's what's live today."
- The Recent/Pinned upgrade requires a small DB migration (per-user JSON column on Users table or sibling table). Slot before or after the sidebar rebuild — not blocking.

---

## Migration plan

Three PRs:

| PR | Scope | Estimate |
|---|---|---|
| Nav-1 (this rebuild) | `_ModernLayout.cshtml` sidebar block fully rewritten. Razor partial `_ControlCentersGroup.cshtml` consumes a `ControlCenterDescriptor[]` registered in DI. Settings drawer page at `/Settings` with sub-nav. Approvals bell wired to existing `/Approvals/Pending`. Cmd-K already exists. | ~600 LOC Razor + 150 LOC CSS |
| Nav-2 | Recent + Pinned server-side persistence. New `UserNavState` table. API endpoints for pin/unpin. JS to wire star-buttons on detail pages. | ~300 LOC |
| Nav-3 | Per-sprint Control Center registration — as Sprints 12-18 ship, each adds one line to `ControlCenterDescriptors.cs` to flip its sidebar item from disabled to active. No re-deploys of the IA itself. | 1 line per sprint |

Nav-1 ships immediately after ADR-017 sign-off. Nav-2 slots whenever (low priority — Recent already works via localStorage). Nav-3 is per-future-sprint deliverable.

**Backward-compat:** every existing route stays the same. The sidebar is what changes; deep-links don't.

---

## Open questions for Dean

Five tradeoffs the research surfaced (per the research doc §7), with my recommendations:

1. **Visible-but-disabled vs hidden** for Sprint 12-18 placeholders — **Recommend visible-but-disabled.** Roadmap transparency, IA stability.
2. **Reports placement** — **Recommend under Finance** (70%+ of report usage is financial); status-quo.
3. **Approvals as bell vs sidebar item** — **Recommend bell.** Inbox flow.
4. **Personalization model** — **Recommend user-defined Pins + Recent only in v1.** Admin-defined role bundles is a Sprint 21+ followup if pilots ask.
5. **Audit Log placement** — **Recommend Settings + Dashboard chip.** Management lives in Settings; forensic via Dashboard chip.

All five are answerable yes/no; flipping any single one is a one-Razor-line edit before the rebuild ships.

---

## Decision log

| Date | Who | What |
|---|---|---|
| 2026-05-18 | Dean (scope) + Claude (proposal) | Control-Center-First IA proposed. v1 = 7 Control Centers. Quality / AP/AR / HR/Crew deferred to v2 ("EAM + Manufacturing Ops disruption, not full ERP"). Sprint 21+ = ERP integration connector workstream. Awaiting sign-off. |

---

## References

- [Research doc — Navigation Information Architecture](research/navigation-information-architecture.md) (557 lines)
- [ADR-016 — Control Center Pattern + Receiving Pilot Spec](ADR-016-control-center-pattern-and-receiving.md)
- [Project memory — Command Center pattern](../memory/project_command_center_pattern.md)
- [Project memory — Launch scope and positioning](../memory/project_launch_scope_and_positioning.md)
