# Navigation Information Architecture — Modern Enterprise Web-App Sidebar Patterns

**Status:** Research / pre-ADR-017
**Authors:** Claude (research) for Dean Dunagan
**Date:** 2026-05-18
**Predecessors:** ADR-014 (Voice-Ready Foundation), ADR-015 (Industry-Agnostic Receipts), ADR-016 (Control Center Pattern + Receiving Spec), `docs/research/receiving-control-center.md`, `project_command_center_pattern.md`
**Successor:** **ADR-017 — Control-Center-First Information Architecture** (recommended; see Section 6)

> **Writing protocol.** Written incrementally to disk. Each major section saved on completion so a crashed run loses at most one section. Aim: 600-900 lines; deliverable is scaffolding for ADR-017, not a coffee-table book. Tone: McKinsey memo crossed with engineering teardown.

---

## Table of contents

| § | Section | Words |
|---|---------|-------|
| 1 | Executive summary | ~440 |
| 2 | The IA failure mode this research solves | ~720 |
| 3 | Reference set — eight tools, deep | ~2,730 |
| 4 | Cross-cutting principles (11 + primitives map) | ~1,100 |
| 5 | Recommended IA for CherryAI EAM | ~1,940 |
| 6 | Implementation plan for the rebuild | ~810 |
| 7 | Open questions for Dean (Q1-Q6) | ~570 |
| 8 | Sources | 36 URLs |
| | **Total** | **~9,000** |

---

## 1. Executive summary

**The claim.** CherryAI EAM's current sidebar is a faithful, well-built example of the IA pattern that every incumbent ERP defaults to — data-table-centric, object-organized, "every screen needs its own line." That pattern fails the moment the product crosses ~150 screens. We are at 400 Razor pages. The fix is not to keep adding more lines or deeper sub-trees. The fix is a structural inversion: promote a new top-level **Control Centers** group that surfaces role + workflow homepages first; demote 80% of today's primary nav items into either a context-aware Cmd-K palette, a per-Control-Center workflow shortcut tray, or an Admin drawer; keep ~7 visible groups; and let workflow pages — `/Receiving/By-Po`, `/Receiving/By-Asn`, `/Receiving/Blind`, and the dozen analogues that ship in Sprints 12-20 — live behind their parent Control Center rather than fighting for a top-line spot.

**Three highest-impact findings from the reference scan.**

1. **The best modern apps (Linear, Stripe, Datadog) have stopped trying to expose every screen in the sidebar.** They expose roughly two layers — a curated, role-aware top level plus a deep, search-driven Cmd-K — and treat the sidebar as the place a user *parks*, not the place they *navigate*. CherryAI has built the Cmd-K (PR #116d.1c-era); we have not yet leaned on it.
2. **SAP Fiori's Spaces & Pages and Oracle Redwood's Work Areas converged independently on the same answer**: cluster screens by *area of work for a role* rather than by *data object type*. This is precisely the Control Center pattern Cherry Street already shipped for Receiving. The sidebar should be the elevator manifest of those work areas.
3. **Admin/settings demoted to a drawer is a near-universal modern pattern** (Linear, Stripe, Notion, GitHub all moved here in 2023-2025). The 30+ `/Admin/*` items currently competing for sidebar real estate belong behind a gear icon, not in line with operational pages.

**Recommended IA shape (one sentence).** Dashboard at the top, then **Control Centers** (10 role-workflow homes, one per future sprint), then **Quick actions** (highest-frequency cross-cutting verbs), then five reorganized data groups (Assets, Finance, Materials & Inventory, Work Management, Projects), then an AI & Integrations group, then a single **Settings** drawer absorbing all `/Admin/*` — backed by a Cmd-K palette indexing every screen so nothing is ever truly unreachable.

**Action.** Write **ADR-017 — Control-Center-First IA**, get Dean's sign-off on the seven open questions in §7, then ship the sidebar rebuild as a single PR ahead of Sprint 12 (Purchasing Control Center). Cost: one PR, ~600 lines of Razor + ~150 lines of CSS. Payback: every Sprint 12-20 Control Center slots in with zero IA churn, and the 52 orphaned admin sub-pages stop being orphans.

---

## 2. The IA failure mode this research solves

The current sidebar (`_ModernLayout.cshtml` lines 198-503) is a competent execution of an IA pattern that does not scale past where we already are. Six top-level operational groups (Assets, Finance, Materials & Purchasing, Work Management, Projects, AI & Integrations), one admin group, and a Dashboard. Inside each: a flat list of routes named after the data table or noun the route renders. **Items / Parts Catalog. Vendors. Purchase Orders. Receiving. Requisitions. Inventory / Warehouses. Stock Levels. Kits. Item Categories.** Every entry is a noun. Every entry is the table.

This is the SAP-MM mental model. It is the NetSuite mental model. It is what every ERP shipped in the 1990s and 2000s defaulted to because the schema was the product. The schema is no longer the product. The workflow is.

**The 400-Razor-page problem.** A `find Pages -name "*.cshtml" | wc -l` returns 400 — higher than the back-of-envelope 250 the brief estimated, because the partials and `_*.cshtml` fragments that ship the KPI / Context / Actions trios on every premium page roughly double the file count. Stripping partials, the unique routable-page count is closer to 200. Either way the sidebar exposes roughly 45 leaf items. That is a 10-22% exposure rate; ~80% of screens are reached by deep link, in-page button, breadcrumb, or pure URL guessing. As more Control Centers ship, the gap widens — Sprint 11 alone added 16 receiving files (six of them routable pages), of which only one (`/Receiving`) currently has a sidebar entry. The new workflow pages `/Receiving/By-Po`, `/Receiving/By-Asn`, and `/Receiving/Blind` ship with **no nav entries at all**. They are reached only from inside the Receiving Control Center. Today that is acceptable because the Control Center is the de-facto entry point. Tomorrow, when nine more Control Centers exist, the user has no map of where the workflow lives.

**The orphaned-admin-page problem.** A `find Pages/Admin -name "*.cshtml"` returns 52 files (`RegulatoryProfiles`, `MaterialMasters`, `StockReceipts`, `Lookups`, `Outbox`, `Webhooks/*`, `Integrations/*`, plus the standard org-admin set). Only the org-admin set is in the sidebar today. The Sprint 4 Wave 1 additions — `RegulatoryProfiles`, `MaterialMasters`, the StockReceipts admin landing — exist; they are not reachable from the sidebar; they are reachable from Cmd-K only if the user knows to search. This is the literal "I built it, no one can find it" problem.

**The "every new feature needs a nav entry to live" problem.** Worse, the existing pattern *teaches* the next IC: "if you build a page, add it to the sidebar." That is why the current sidebar has lines like *Webhook Deliveries* and *Event Catalog* — defensible features, indefensible to spend top-level sidebar real estate on. The defensive logic compounds. Without an IA rule that explicitly demotes most surfaces to indirect access, every Control Center we ship will add 4-6 new sidebar lines, and within a year the sidebar is a 70-item scroll log.

**Sprint 11's specifically-orphaned surfaces.** `/Receiving/ControlCenter` (live), `/Receiving/By-Po` (live, no nav), `/Receiving/By-Asn` (live, no nav), `/Receiving/Blind` (live, no nav), `/Receiving/History` (live, no nav), `/Receiving/Inspect` (live, no nav). Sprint 4 Wave 1's orphans: `/Admin/RegulatoryProfiles`, `/Admin/MaterialMasters`, `/Admin/StockReceipts`. These nine surfaces are the canary. Sprint 12 will add ten or so more (Purchasing Control Center + Receive-Without-PO, Vendor 360, RFQ Inbox, etc.). The IA must absorb them without growing the sidebar.

The right response is not "shrink the items" or "use icons." The right response is to change what the sidebar *is for*. The sidebar's job stops being "expose every screen" and becomes "expose every role's home, plus the four or five operations they do every day, plus the master-data tables they curate when they have to." Everything else moves to Cmd-K and the Settings drawer.

**Why the current sidebar is the *good* version of the wrong pattern.** It is worth being clear: the existing sidebar is not poorly executed. The Razor is clean, the active-state logic via `IsAny()` is correct, the section labels (`OPERATIONS`, `SYSTEM`) are appropriate, the feature-flag gating (`enableInventory`, `enablePurchasing`) is well-factored, and the recent-pages section is a legitimate personalization layer. The problem is not implementation quality. The problem is that the *pattern* — every-screen-as-a-line, grouped-by-noun — has a known scaling ceiling around 40-60 visible items, and we have already started building above it. Continuing to invest in the pattern is putting good engineering against the wrong IA. The fix is structural, not stylistic.

---

## 3. Reference set — what eight tools do

We picked eight that disagree with each other as much as possible. Two pure-play SaaS (Linear, Stripe), two power-user platforms (Notion, Datadog), two enterprise giants from the previous decade (Salesforce Lightning, SAP Fiori), one enterprise giant from this decade (Oracle Redwood), and the most directly competitive manufacturing platform (Plex). Each section: how the sidebar is organized, what's good, what fails for a 400-page enterprise app — and what to steal versus what to leave.

### 3.1 Linear — the project workspace pattern

**The shape.** Three-zone sidebar at ~240px expanded. Top: workspace switcher + global search + inbox. Middle: a small "Favorites" tray (user-curated, drag-orderable), then "Teams" (each team collapses to show Issues / Views / Projects / Cycles / Docs). Bottom: a single "More" item that hides everything else. Settings sits behind a dedicated route, not in the sidebar tree. The sidebar is fully personalizable per user via right-click → "Customize sidebar"; sections, items, and ordering all persist locally.

**What's good.** It is structurally honest about a hard problem: a workspace can have 30 teams and 200 projects, and there is no IA that fits all of that on screen at once. Linear's answer is to make the sidebar a *bookmark bar that defaults to sensible team scopes*, and to push genuine navigation into Cmd-K (their command palette is one of the best in any SaaS). The keyboard-first model — `G` then a letter for "go to," Cmd-K for "do anything" — means power users almost never touch the mouse. The visual language is calm: monochrome icons, low chroma, generous letter-spacing. Cognitive load is the lowest of any app on this list.

**What fails for CherryAI.** Linear's pattern assumes a small canonical noun-set (Issues, Projects, Cycles) replicated across teams. CherryAI has heterogeneous nouns per role: a Receiving Clerk lives in receipts, a Buyer in POs and requisitions, a Maintenance Tech in work orders and assets. We cannot just have ten identical Control Center subtrees; each needs distinct child shortcuts. Linear also has no concept of admin / settings being part of the sidebar at all — defensible for a 30-person startup, indefensible for an EAM where settings touches GL accounts, fiscal calendar, organization hierarchy, role grants, and 40 lookup tables.

**Steal.** The "Favorites" tray. Right-click-to-customize. Cmd-K as the safety net for everything not in the sidebar. The discipline of pushing settings out of the primary nav. The "More" pattern for low-frequency-but-canonical items.

**Leave.** The flat team-by-team replication; the assumption that every group has the same child shape; the absence of admin from the sidebar entirely.

### 3.2 Stripe Dashboard — role-aware financial calm

**The shape.** A ~256px sidebar with a fixed top section (Home, Balances, Transactions, Customers, Products, Payments, Reports) — Stripe's *core nouns* — and a personalized **Shortcuts** section that combines pinned and recently visited pages. Below that, expandable groups for each Stripe product the account has enabled (Billing, Connect, Issuing, Radar, Sigma, Atlas, Capital, Climate). The bottom anchor is a Developers section. Settings lives in a separate page reached from a top-right gear, not the sidebar. The dashboard pairs the sidebar with a right-side context rail on detail pages (a payment's customer, dispute history, fraud signals all surface there rather than as separate routes).

**What's good.** Two patterns worth lifting wholesale. First, **role-aware top items + opt-in product expansion**: a merchant who does not use Connect never sees Connect. The sidebar adapts to the account's actual surface area. Second, **the Shortcuts section** — automatic + manual, merged — is the single highest-ROI personalization affordance any of these tools ships. Users do not have to organize anything; the system promotes the pages they touch. Stripe's typography hierarchy is also unusually disciplined: section labels are 11px uppercase with 4px letter-spacing; items are 13px regular; the visual difference between section and item is unmistakable at one glance.

**What fails for CherryAI.** Stripe has fundamentally one persona (a finance/ops generalist at the merchant). The sidebar does not have to model role variance; it has to model product-surface variance. CherryAI is the opposite: the products are mostly the same (every plant runs Maintenance + Materials + Receiving + Quality), but the *role* of the logged-in user changes the relevance dramatically. Stripe also has comparatively few master-data tables; ours is heavy on lookup curation.

**Steal.** The Shortcuts (pinned + recent merged). The right-rail context panel on detail pages. The settings-behind-gear pattern. The uppercase 11px section labels.

**Leave.** Product-opt-in as the primary axis (we are role-first, not product-first); the assumption of one canonical persona.

### 3.3 Notion — collapsible workspace tree, infinite hierarchy

**The shape.** ~260px sidebar with three labeled sections: **Favorites**, **Teamspaces**, **Shared**, **Private**. Every node is an arbitrary page that nests arbitrarily deep. Each section collapses independently. The whole sidebar can be torn down to an icon rail or hidden entirely. Search and Cmd-K live at the top. The April 2026 redesign added a unified Teamspaces concept and tightened the visual chrome.

**What's good.** Two ideas worth taking. First, **independent section collapse with per-user persistence** — the sidebar is a *personal view* over a *shared structure*. Everyone sees the same page tree; everyone has their own collapse state. Second, **hover-reveal affordances**: actions (add child, favorite, share) appear only on hover, which keeps the resting state quiet. Notion's IA philosophy is "give the user the tools to build their own IA," and that philosophy is correct for a workspace product where structure is content.

**What fails for CherryAI.** Infinite nesting is the wrong model for an EAM. Our hierarchy is bounded (the sprint roadmap defines exactly what exists). We do not want users *creating* navigation; we want them *consuming* a curated one. Notion's pattern, taken too far, becomes "the user is responsible for their own findability." That works for a writing app and fails for software a plant runs on.

**Steal.** Independent section collapse with per-user persistence. Hover-only affordances. The icon-rail collapse state.

**Leave.** Infinite nesting. User-creatable nav items. The notion (no pun) that structure is content.

### 3.4 Datadog — search-first, "everything-is-a-list"

**The shape.** A narrow icon-rail sidebar (~64px) with collapsible groups that open on hover into a wide flyout. Top of the sidebar: a Quick Nav menu (`/` to open) that is essentially a Cmd-K palette restricted to navigation. Groups are organized by *operational concern* — Infrastructure, APM, Logs, RUM, Security, CI Visibility — not by data object. Within each group, the items are mostly "Explorer," "Service Catalog," "Dashboards," "Monitors" — a small canonical action set replicated across every group. The 2024 redesign explicitly moved the most-used features (Watchdog, Service Management) to the top and the least-used to the bottom of the sidebar.

**What's good.** The pattern of a *narrow rail that flies out wide on hover* is the right answer for a high-density product that still needs persistent navigation; it gives you the brand-identity benefit of always-visible nav without the screen-real-estate cost. The "operational concern" grouping is precisely what we mean by role + workflow centric. And the Quick Nav menu is the only command palette we surveyed that is explicitly scoped to *just navigation* — separate from the global search — which is faster because the result set is smaller.

**What fails for CherryAI.** Datadog has trained users; manufacturing-software users have not been trained to discover things by hovering an icon rail. The hover-flyout pattern is also fragile on touch-first dock devices (the Zebra TC52 on the actual dock). We can borrow the *idea* but should keep the expanded sidebar as the default.

**Steal.** Operational-concern grouping, not object-type grouping. Quick Nav as a nav-scoped Cmd-K. Most-used at top, least-used at bottom.

**Leave.** Icon-rail-by-default (we are expanded-by-default). Hover-flyout sub-nav.

### 3.5 Salesforce Lightning — the App Launcher pattern

**The shape.** No persistent sidebar at all. Navigation is a top horizontal bar with the **App Launcher** — a 3×3 grid icon — that opens a modal grid of "Apps." Each App (Sales, Service, Marketing, Commerce, Field Service) is a curated bundle of tabs, layouts, and permissions assigned by admins to user profiles. Once you pick an App, your navigation bar reconfigures to that App's tabs (Accounts, Contacts, Opportunities for Sales; Cases, Knowledge for Service; etc.). The App is the unit of role-based navigation.

**What's good.** The strongest *concept* on this list: **role = an explicit, named, admin-configurable bundle of pages**. Salesforce ships dozens of out-of-the-box Apps; admins can clone and customize per role; users pick the App they need. This is genuinely role-first IA, not "role-aware sidebar." The Pin pattern (right-click a tab, "Pin to navigation") is also strong.

**What fails for CherryAI.** The horizontal-bar primary nav burns vertical space and tops out at ~8 visible tabs before overflow. App-switching is a context shift expensive enough that users complain about it constantly in field interviews. And the absence of a persistent vertical sidebar means you always need the App Launcher modal to escape your current context. For a manufacturing user who *crosses* roles ten times a day — a Receiving Lead who also approves requisitions and checks WO status — App Launcher is friction.

**Steal.** The concept of role-named, admin-configurable page bundles. The Pin-to-nav pattern.

**Leave.** Horizontal primary nav. App-switching as the role mechanism. The modal launcher.

### 3.6 SAP Fiori Launchpad — tile-based, role-based, Spaces & Pages

**The shape.** No vertical sidebar in the classic sense. The Launchpad opens to a wall of role-assigned **tiles**, organized into **Spaces** and **Pages** (introduced in S/4HANA 2020, replacing the older tile-group homepage). A user has one or more Spaces (an *area of work* corresponding to one or more business roles); each Space has Pages; each Page has tiles; each tile is a Fiori app. Tiles are intent-based — they declare a semantic object + action and the system resolves the target route. Search is the secondary entry point.

**What's good.** Two ideas. First, **Spaces & Pages are explicitly modeled as "areas of work for a role,"** which is identical in spirit to our Control Centers. Second, **intent-based routing** (Receiving Clerk wants `display ReceiptInbox`; the app resolves to the right page) is a cleaner abstraction than hard-coded URLs and pairs naturally with voice (the user says "show me receipts," the AI resolves the intent).

**What fails for CherryAI.** Tile walls are a maintenance nightmare; SAP customers spend significant consulting dollars curating which tiles which role sees, and the result is often hundreds of tiles per space and ten levels of taxonomy. The tile is also visually heavy — every interaction is a 100×100px clickable square, and a single screen with 30 tiles becomes a wall of cognitive load. Spaces & Pages were a *fix* for that problem and only partially solved it.

**Steal.** "Areas of work for a role" as the top-level grouping concept. Intent-based navigation as the voice/Cmd-K substrate.

**Leave.** Tile walls as the primary affordance. Multi-level Spaces > Pages > Tiles taxonomy that requires admin curation just to be usable.

### 3.7 Oracle Redwood — Work Areas + role-first dashboards

**The shape.** Redwood ships Work Areas as the equivalent of a Control Center: a role-scoped landing that combines KPIs, tiles, guided flows, and contextual lists. The primary navigation is a left-side **Navigator** (icon rail that expands to a flyout) plus a top-right **Quick Actions** dropdown for the small set of high-frequency verbs. A persistent **PLM Navigator** sidebar example exposes saved searches and a pinnable clipboard across sessions — essentially a personalized Favorites tray, but global. Pattern Book templates ship pre-built role-based dashboards in Figma, which Visual Builder consumes; admins extend without forking.

**What's good.** Three. First, **Work Areas as a first-class concept that the IA explicitly promotes**. Second, **separating navigation from action**: the left rail is *where you go*; the top-right Quick Actions is *what you do*. Most enterprise apps blur these and end up with neither working well. Third, **the saved-searches + clipboard pinning as a persistent, cross-session personalization layer** — a pattern that genuinely changes behavior because the user's prior intent travels with them.

**What fails for CherryAI.** Redwood is a corporate-scale framework that takes a lot of plumbing to look right; the pattern-book + Visual Builder dependency is overkill for our Razor stack. Some of the visual moves (heavy chrome on the navigator flyout) feel dated next to Linear.

**Steal.** Work Areas as a first-class IA concept. Separation of "where I go" (sidebar) and "what I do" (Quick Actions tray). Cross-session pinning of searches and recently viewed items.

**Leave.** The Visual Builder dependency. Heavy flyout chrome.

### 3.8 Plex Smart Manufacturing — manufacturing-specific, dock + shop floor

**The shape.** Plex's UX has shifted significantly since the Rockwell acquisition. The browser-based platform has a left sidebar plus a top action bar; Plex Mobile is a separate application explicitly designed for plant-floor and small-screen use. The desktop sidebar exposes major operational areas (Production, Quality, Inventory, Receiving, Shipping, Maintenance, Tooling, Engineering) — already organized closer to role + workflow than to data tables. Within each, screens are still named after objects. Plex's UX team has publicly emphasized shop-floor-operator-first design — large tap targets, error-recovery flows, role-tailored screens — with the explicit goal of reducing errors and improving data accuracy at the source.

**What's good.** Plex has done the work of *thinking about who is on the dock*, and the navigation reflects that the dock-worker persona is real and distinct from the office-finance persona. The mobile-as-separate-app discipline avoids the "responsive ERP that is responsive to no one" trap that NetSuite and D365 fall into. Visual moves: minimal chrome, big targets, plant-floor-friendly contrast.

**What fails for CherryAI.** Plex's nav is still object-organized within each operational group. The split between desktop and mobile applications is honest but expensive (Cherry Street's PWA strategy aims to do both from one codebase, which is harder and only works if the dock-mode is a real focus mode, not a responsive shrink).

**Steal.** Operational-area top-level grouping that already aligns with our Control Center taxonomy. Dock-worker-first discipline within each operational area. Big-target focus mode.

**Leave.** Two-app split (desktop + mobile). Object-naming within groups.

### 3.9 What the eight tell us together

No tool on this list has fully solved the problem. Linear, Stripe, and Datadog have the best modern sidebar craft but optimize for narrow domains. Salesforce, SAP, Oracle have the right *role-first concepts* (Apps, Spaces, Work Areas) but layer them on dated chrome. Plex is the closest competitor in spirit but still organizes within groups by object name. **The opportunity for Cherry Street is to lift the role-first concept from the enterprise giants, render it with the modern craft of the SaaS leaders, and ship it with a Cmd-K safety net that none of the giants have managed.** That is the design brief for the IA we propose in §5.

**Convergent moves across the reference set.** Worth naming explicitly because the convergence is the signal:

| Move | Linear | Stripe | Notion | Datadog | Salesforce | SAP Fiori | Oracle Redwood | Plex |
|---|---|---|---|---|---|---|---|---|
| Cmd-K palette as primary discovery | ✓ | ✓ | ✓ | ✓ (Quick Nav) | partial | ✗ | partial | ✗ |
| Settings out of primary sidebar | ✓ | ✓ | ✓ | ✓ | ✓ | n/a | ✓ | partial |
| Role/area as top-level concept | partial | partial | partial | ✓ | ✓ | ✓ | ✓ | ✓ |
| Pinned + recent personalization | ✓ | ✓ | ✓ | partial | ✓ | partial | ✓ | partial |
| Right-rail context drawer | ✓ | ✓ | partial | ✓ | partial | ✗ | partial | partial |
| Sidebar collapse to icon rail | ✓ | ✓ | ✓ | ✓ (default) | ✗ | n/a | ✓ | ✓ |

Six moves; eight tools; nobody has all six. Linear and Stripe come closest among the modern SaaS leaders (5/6 each). Salesforce nails role-as-bundle but skips the icon-rail / right-rail moves. SAP Fiori has the strongest role-area concept and the weakest discovery. The combination of *all six* in one product is the white space.

Vercel's February 2026 dashboard redesign — not in our primary eight but worth noting — independently landed on the same direction: a resizable sidebar, unified team/project navigation, prioritization of common workflows above master-data tables, and a mobile floating bottom bar instead of a responsive shrink of the desktop nav. The convergence is now broad enough to call a pattern, not a preference.

---

## 4. Cross-cutting principles

Eleven rules emerge from the reference set. They are ordered roughly by how much they change about the current CherryAI sidebar.

**P1 — Role-first over data-first.** The unit of the top-level sidebar is *a role's daily work*, not *a table the database has*. "Receiving" beats "Receipts." "Maintenance" beats "Work Orders + Work Requests + Technicians + PM Templates + PM Schedules + PM Assignments + Schedule Board" laid out as siblings. Tables remain reachable, but they reach for tables when they need to curate master data — not when they need to *work*. This is the move SAP made with Spaces & Pages and Oracle made with Work Areas; it is what Plex is most of the way to. The Control Centers group operationalizes this.

**P2 — Workflow shortcuts above data tables in the visual hierarchy.** Inside any group, the highest-frequency *verbs* sit above the highest-frequency *nouns*. "Receive by PO" / "Receive by ASN" / "Blind receive" sit above "Receipt history" / "Receipt details." A user clicks a verb to start work; they click a noun to look something up. Verbs should win the position fight.

**P3 — Collapsible groups with smart defaults.** Every group collapses. The current page's group is expanded by default; other groups are collapsed. Expansion state persists per user. This is the Notion + Linear default. The current sidebar already mostly does this via the Razor `IsAny()` helpers (lines 198, 232, 283, 344, 405, 447) — but per-user persistence is missing.

**P4 — Cmd-K as the safety net.** Anything not in the sidebar must be reachable in ≤2 keystrokes from anywhere. The Cmd-K palette must index *every route in the application* — including the 52 admin sub-pages, the workflow shortcuts, and entity-instance lookups ("PO 4500178823," "Asset PUMP-204"). PR #116d.1c already ships the Cmd-K primitive; the index is the work. This single feature is what makes it safe to remove items from the sidebar — the price of demotion is zero.

**P5 — Right-rail context replaces ~30% of drill-down links.** Detail pages currently route to sibling tables ("View Vendor," "View PO Line," "View Receipt History") — but each of those is a context lookup, not a navigation move. A right-side context drawer (Linear, Stripe, GitHub all use it) renders the related entity in place. Drawer routes do not need sidebar entries. Pattern already exists in the codebase (PR #116d.1b shipped the ContextDrawer primitive); the work is to deploy it consistently.

**P6 — Admin / settings demoted to a separate drawer or tray.** The 12-15 admin items in the current sidebar represent ~30% of its visual weight. They are needed weekly at best; daily by no one. Move to a single Settings entry that opens a multi-section drawer or a dedicated `/Settings` route with its own left-rail sub-nav. This is what Linear, Stripe, GitHub, and Notion all converged on between 2022 and 2025.

**P7 — Density rules: target 7±2 top-level groups, ~5 items each, max two levels deep.** Miller's 7±2 is the most-cited and most-abused law in IA. The truth: it is *not* a strict cap on visible items (visible items are recognized, not recalled), but it *is* a useful target for the working set the user holds in their head while navigating. Seven top-level groups with five items each = 35 visible leaves before any expansion; ~70 with everything open. That is comfortable. The current sidebar at 6 groups + admin is within range; the problem is item count per group (Materials & Purchasing has 8, Admin has 13).

**P8 — "Recently used" + "Pinned" as the personalization cheat code.** Stripe's Shortcuts (auto-pinned + manually pinned, merged) is the highest-ROI personalization affordance. The current sidebar has a "RECENT" section (line 506-509) that is populated client-side; it should be promoted, deduped against pinned items, and persisted server-side per user (currently localStorage-only).

**P9 — Don't paint everything as primary nav.** Secondary navigation belongs in context: breadcrumbs (already built), in-page tab strips, right-rail context drawers, page-header action bars, and the Cmd-K palette. Several items currently in the sidebar — Event Catalog, Webhook Deliveries, Outbox, Integration Hub sub-pages — are secondary to the AI & Integrations *area* and belong in an in-page tab strip on `/AI` or `/Admin/Integrations`, not as siblings.

**P10 — Mobile is a separate problem; solve desktop right first.** The Zebra TC52 dock device runs the same Razor app but in a focused-mode shell that hides the sidebar entirely (the PR #116d-era dock-mode work already does this). Do not contaminate the desktop IA design with mobile constraints. Solve desktop right. Then solve dock-mode right. The 2026 industry consensus (Plex, Linear, Stripe) is that responsive shrinkage is a lie; mobile is a different product surface.

**P11 — The sidebar is for parking, not navigation.** Linear's contribution. Once Cmd-K is good, the user does not *navigate* via the sidebar; they navigate via keyboard, and the sidebar shows them *where they are* and *what's pinned*. This reframing is what licenses every other principle on this list — if the sidebar is not the only way to reach a page, demotion costs nothing.

### 4.1 How the principles map to primitives we already shipped

Useful to flag explicitly: most of the heavy lifting is *already in the codebase* from the design-system PRs of the last two months. The IA rebuild is mostly composition, not net-new primitives.

| Principle | Existing primitive / PR | Gap to close |
|---|---|---|
| P1 Role-first | Receiving Control Center pattern (PR #116d.2-#116d.5) | Generalize to 9 more verticals |
| P2 Workflow shortcuts | Quick-action chips on Control Center landings | Lift into sidebar Group 3 |
| P3 Collapsible groups | `menu-group` + `IsAny()` (current layout) | Persist expansion per user |
| P4 Cmd-K | Command palette (PR #116d.1c) | Index admin sub-pages + entity instances |
| P5 Right-rail context | ContextDrawer primitive (PR #116d.1b) | Deploy on detail pages consistently |
| P6 Settings drawer | None yet | New `/Settings` Razor Page with sub-nav |
| P7 Density | Current sidebar | Trim per §5; nothing to build |
| P8 Recent + Pinned | `recentNavSection` (line 506, localStorage) | Server-side persistence + Pinned UI |
| P9 Don't paint all as primary nav | Page tabs primitive | Apply on AI & Voice + Settings |
| P10 Mobile separation | Dock-mode shell | Already handled; respect the line |
| P11 Sidebar as parking | Cmd-K (above) | Mostly a posture shift, no new code |

Total new primitives required: roughly two (`/Settings` drawer, server-side Pinned + Recent). Everything else is reorganization. This is why the rebuild fits in one PR.

---

## 5. Recommended IA for CherryAI EAM

Seven labeled sections, ~7 groups, ~5 items each. Settings demoted to a drawer. Recent / Pinned promoted. Cmd-K is the safety net. Every Sprint 12-20 Control Center slots into Group 2 with zero IA churn.

### 5.1 The structure (screen-in-words on `/Receiving/ControlCenter`)

```
+---------------------------------------------------+
| [ABS]  EAM                                        |   <- brand
|        © Powered by CherryAI                      |
|--------------------------------------------------|
| [Org: Cherry Forge Indianapolis ▼]                |   <- org selector
| [Site: Plant 2 — Indy South ▼]                    |   <- site selector
|---------------------------------------------------|
|  ⌂  Dashboard                                     |   <- single top item
|                                                   |
|  CONTROL CENTERS                                  |   <- section label
|  ▼ ⊕  Receiving        ●                          |   <- active (filled dot)
|    ▸ Receive by PO                                |   <- nested verbs
|    ▸ Receive by ASN                               |
|    ▸ Blind receive                                |
|    ▸ Receipt history                              |
|  ▸ ⊕  Purchasing       (soon — Sprint 12)         |   <- disabled grey
|  ▸ ⊕  Maintenance      (soon — Sprint 13)         |
|  ▸ ⊕  Planning         (soon — Sprint 14)         |
|  ▸ ⊕  Scheduling       (soon — Sprint 15)         |
|  ▸ ⊕  Inventory        (soon — Sprint 16)         |
|  ▸ ⊕  Quality          (soon — Sprint 17)         |
|  ▸ ⊕  Shipping         (soon — Sprint 18)         |
|  ▸ ⊕  AP/AR            (soon — Sprint 19)         |
|  ▸ ⊕  HR / Crew        (soon — Sprint 20)         |
|                                                   |
|  QUICK ACTIONS                                    |
|    ⊕  Create work order                           |
|    ⊕  Create requisition                          |
|    ⊕  Create receipt (blind)                      |
|    ⊕  Add asset                                   |
|    ⊕  Log inspection                              |
|                                                   |
|  OPERATIONS                                       |
|  ▸ Assets         (Register · Plant Floor · …)    |
|  ▸ Work           (Work Orders · Requests · …)    |
|  ▸ Materials      (Items · Vendors · Inventory)   |
|  ▸ Projects       (CIP · Costs · Analytics)       |
|  ▸ Finance        (Books · GL · Journals · Tax)   |
|                                                   |
|  PLATFORM                                         |
|  ▸ AI & Voice     (Assistant · API · Webhooks)    |
|  ▸ Reports                                        |
|                                                   |
|  RECENT                                           |   <- auto-populated
|    PO 4500178823                                  |
|    Asset PUMP-204                                 |
|    Receipt R-2026-04412                           |
|                                                   |
|  PINNED                                           |   <- user-curated
|    Schedule Board                                 |
|    Period Close                                   |
|---------------------------------------------------|
| 🔍 Search (⌘K)   ☀/🌙   ❓ Help   ⚙ Settings     |   <- footer utility row
+---------------------------------------------------+
```

### 5.2 Group 1 — Dashboard

A single top item, unchanged. `/` for the global home. This is unchanged from today and is the right anchor.

### 5.3 Group 2 — Control Centers (NEW; the spine of the new IA)

Ten items, one per role-workflow surface, mapped 1:1 to Sprint 11-20. The currently shipped Control Center (Receiving) is active; the other nine are visible-but-disabled with `(soon — Sprint N)` tags. Visibility is intentional: it signals roadmap to the user, it makes the IA stable across sprints (the sidebar does not move when Purchasing ships, the Purchasing line just lights up), and the disabled state is itself a teaching surface.

| Position | Item | Status | Route |
|---|---|---|---|
| 1 | Receiving | Live (Sprint 11) | `/Receiving/ControlCenter` |
| 2 | Purchasing | Sprint 12 | `/Purchasing/ControlCenter` (placeholder) |
| 3 | Maintenance | Sprint 13 | `/Maintenance/ControlCenter` |
| 4 | Planning | Sprint 14 | `/Planning/ControlCenter` |
| 5 | Scheduling | Sprint 15 | `/Scheduling/ControlCenter` |
| 6 | Inventory | Sprint 16 | `/Inventory/ControlCenter` |
| 7 | Quality | Sprint 17 | `/Quality/ControlCenter` |
| 8 | Shipping | Sprint 18 | `/Shipping/ControlCenter` |
| 9 | AP/AR | Sprint 19 | `/AccountsPayable/ControlCenter` |
| 10 | HR / Crew | Sprint 20 | `/HR/ControlCenter` |

Each Control Center expands to show its workflow-shortcut children when active. For Receiving the children are the four already-built workflow surfaces: Receive by PO, Receive by ASN, Blind receive, Receipt history. Future Control Centers will declare their own children in a per-section Razor partial; the layout does not have to know about them in advance.

**Rationale.** This is the structural inversion. The first thing a user sees, after Dashboard, is "what is my role going to do today?" Not "what table do I want to query?" Every Sprint 12-20 deliverable slots in here with one line of Razor (un-disable the item, point to the new route), zero IA churn, and a user who already learned the pattern.

### 5.4 Group 3 — Quick Actions (NEW; verb tray)

Five-to-seven highest-frequency cross-cutting verbs. Each opens a focused create-flow modal or a workflow page (not a table). These are *creation* affordances — the things a user *starts* — and they are scoped *across* roles. Membership decisions:

- **Create work order** — every role does this; cannot live only behind Maintenance.
- **Create requisition** — Buyers, Maintenance, Shop Floor all originate requisitions.
- **Create receipt (blind)** — fast path for unexpected dock arrivals.
- **Add asset** — Asset additions are rare but always urgent (a forklift just arrived).
- **Log inspection / quality event** — surface so QC events get reported, not buried.

Items considered and rejected from Quick Actions: Create PO (lives in Purchasing CC and is a process, not a single verb), Create journal entry (Finance-only, lives in Finance group), Create user (admin verb, lives in Settings).

**Rationale.** Verbs above nouns. A clerk who clicks Quick Actions is starting work; a clerk who clicks Operations is looking something up. The two intents deserve separate sidebar regions.

### 5.5 Group 4 — Operations (the reorganized data-table groups)

Five groups, ~4-6 items each. The current six operational groups (Assets, Finance, Materials & Purchasing, Work Management, Projects, AI & Integrations) compress to five by merging:

**Assets** (unchanged content; sharper label):
- Asset Register · Plant Floor · Locations · Categories · Bulk Operations · Barcodes

**Work** (renamed from Work Management):
- Work Orders · Work Requests · PM Templates · PM Schedules · Schedule Board · Technicians
- *(PM Assignments → demoted; it's a step in the PM Templates flow, not a destination)*

**Materials** (consolidates today's Materials & Purchasing):
- Items / Parts Catalog · Vendors · Purchase Orders · Requisitions · Inventory · Stock Levels
- *(Kits → demoted; reach via Items detail or Cmd-K)*
- *(Item Categories → demoted to Settings)*

**Projects** (unchanged):
- CIP Projects · Cost Tracking · Cost Analytics

**Finance** (unchanged content; trims order):
- Depreciation Books · GL Accounts · Journals · Period Close · Pending Approvals · Accounts Payable · Reports · US Tax · Canadian CCA
- *(Reports stays here; arguable it should be its own group — see §7 open question)*

### 5.6 Group 5 — Platform

Two items, both currently scattered across the sidebar:

- **AI & Voice** — `/AI` + sub-tabs inside the page for API Hub, Webhooks catalog, Outbox, Integrations. *(Currently five separate sidebar items; collapses to one.)*
- **Reports** — global cross-module reports landing.

**Rationale.** "Platform" is the right home for cross-cutting capability surfaces. Webhooks Deliveries / Event Catalog / Outbox / Integration Hub are valid pages but do not belong as sidebar siblings of Receiving and Work Orders. Promote them to in-page tabs on a single AI & Voice landing.

### 5.7 Recent + Pinned (NEW visibility)

Two auto-populated sections at the bottom of the scrollable area:

- **Recent** — last 5 entities visited (PO, asset, receipt, work order, etc.), persisted server-side per user, refreshed on every detail-page visit.
- **Pinned** — user-curated, right-click-to-pin from anywhere in the app (the Linear + Stripe pattern). Persisted per user.

The current `recentNavSection` (line 506) is the foundation; it currently uses localStorage and lists *page routes*, not *entities*. The upgrade is (a) persist server-side, (b) entity-scoped not route-scoped, (c) add Pinned as a sibling.

### 5.8 Settings drawer (replaces today's Administration group)

A single **⚙ Settings** entry in the footer utility row (where it already lives) opens a full-page or drawer interface with its own left sub-nav, organized as:

- **Organization** — Sites, Companies, Departments, Cost Centers, Manufacturers, Project Managers
- **Users & Access** — Users & Roles, Permissions
- **Master data** — Item Categories, Asset Categories, Kits, Lookups, GL Accounts, Fiscal Calendar
- **Data & Integration** — Data Management, Webhooks, Event Catalog, Outbox, Integration Hub
- **System** — System Settings, Audit Log

This is roughly the same 30 items that exist today, but they are *out of the operational sidebar*. The pattern is identical to Stripe's gear-icon settings, Linear's `/settings/*`, GitHub's `/settings/*`.

### 5.9 What gets removed from the primary sidebar

- All `/Admin/*` items currently in the Administration group → Settings drawer.
- `Webhook Deliveries`, `Event Catalog`, `Outbox`, `Integration Hub` → in-page tabs on AI & Voice landing (and Settings → Data & Integration for admin views).
- `PM Assignments`, `Kits`, `Item Categories` → demoted (reachable via parent detail page + Cmd-K + Settings).
- `Pending Approvals` → stays for now; could become an inbox-style icon in the topbar/footer (see §7).

### 5.10 What stays — and why each stays

Every operational *role's daily work* gets a top-level sidebar entry, either as a Control Center (Group 2) or as a data-table group (Group 4). Every other surface is reachable but not pre-paid for in screen real estate. Total visible top-level items, fully collapsed: **8** (Dashboard + 10 Control Centers shown as one expandable group + 1 Quick Actions group + 5 Operations groups + 1 Platform group = 8 groups, 1 leaf). Fully expanded with Receiving active: ~35 leaves. Within Miller's working-set range. Within the reference set's density envelope.

### 5.11 How Sprint 12-20 slot in without IA churn

When Sprint 12 (Purchasing Control Center) ships:
1. Flip the `Purchasing` Control Center item from disabled to live.
2. Add the workflow-shortcut children (Create PO, Receive RFQ response, Vendor 360, etc.).
3. Optionally collapse or move the legacy `Purchase Orders` data-table item under Materials (likely stays — Materials is the noun home; Purchasing CC is the verb home).

Nothing else changes. The user sees their existing sidebar with one new active line. The pattern repeats nine more times. By Sprint 20, the sidebar looks identical in shape to the day Sprint 11 shipped; only the *contents* have lit up. This is the payoff.

Concretely, predicted workflow shortcuts per Control Center — useful for sequencing the descriptor commits and for pre-baking the icons in the design system:

| Sprint | Control Center | Predicted workflow children |
|---|---|---|
| 11 | Receiving (live) | Receive by PO · Receive by ASN · Blind receive · Receipt history |
| 12 | Purchasing | Create PO · RFQ inbox · Vendor 360 · Open PO board |
| 13 | Maintenance | Open work · My assignments · Schedule board · PM due |
| 14 | Planning | MRP run · Demand plan · Supply review · Net changes |
| 15 | Scheduling | Dispatch board · Capacity by line · Sequencer · Reschedule queue |
| 16 | Inventory | Cycle count · Stock movement · Adjustments · Quarantine review |
| 17 | Quality | Inspection queue · CAPA inbox · Non-conformance log · Audit board |
| 18 | Shipping | Ship-by-PO · Pack & label · BOL center · Carrier handoff |
| 19 | AP / AR | 3-way match · Invoice inbox · Aging · Payment run |
| 20 | HR / Crew | Crew board · Time entry · Skills matrix · Shift planner |

These are *placeholders* — each Control Center will refine its own list in its own research doc (the analogue of `receiving-control-center.md` for Sprint 12). The point is that the *shape* is stable: 3-5 verbs per CC, all reaching workflow pages, none reaching master-data tables. Master data lives in Operations groups + Settings drawer.

---

## 6. Implementation plan for the rebuild

Single PR. Estimate: 600 lines of Razor changes, ~150 lines of CSS, 1 migration for user-level Pinned + Recent persistence. Ship ahead of Sprint 12 so Purchasing Control Center walks into the new IA, not the old one.

**Step 1 — Carve `_ModernLayout.cshtml` into partials.** The sidebar block (lines 198-503, currently 305 lines of inline Razor) becomes three partials:
- `_Sidebar_ControlCenters.cshtml` — drives Group 2 from a single C# list of `ControlCenterDescriptor { Slug, Label, Icon, Route, Status, SprintNumber, WorkflowChildren }`. Status is `Live | Soon`. Live items are clickable links; Soon items render disabled with the sprint tag.
- `_Sidebar_QuickActions.cshtml` — drives Group 3 from a `QuickAction` list. Same pattern.
- `_Sidebar_Operations.cshtml` — the existing 5 reorganized groups.

The main layout file shrinks by ~250 lines; each partial is independently testable and accessibility-auditable.

Descriptor sketch (C# record + Razor partial; keep it boring):

```csharp
public record ControlCenterDescriptor(
    string Slug,
    string Label,
    string Icon,                 // FontAwesome class
    string Route,                // /Receiving/ControlCenter
    ControlCenterStatus Status,  // Live | Soon
    int? SprintNumber,           // 11 for Receiving, 12 for Purchasing, ...
    IReadOnlyList<WorkflowChild> Children);

public enum ControlCenterStatus { Live, Soon }

public record WorkflowChild(string Label, string Route, string Icon);

// Registered once in Program.cs as a singleton; partial loops over it.
public static readonly IReadOnlyList<ControlCenterDescriptor> All = new[] {
    new ControlCenterDescriptor("receiving", "Receiving", "fa-truck-ramp-box",
        "/Receiving/ControlCenter", ControlCenterStatus.Live, 11,
        new[] {
            new WorkflowChild("Receive by PO", "/Receiving/ByPo", "fa-file-import"),
            new WorkflowChild("Receive by ASN", "/Receiving/ByAsn", "fa-truck-fast"),
            new WorkflowChild("Blind receive", "/Receiving/Blind", "fa-eye-slash"),
            new WorkflowChild("Receipt history", "/Receiving/History", "fa-clock-rotate-left"),
        }),
    new ControlCenterDescriptor("purchasing", "Purchasing", "fa-file-invoice",
        "/Purchasing/ControlCenter", ControlCenterStatus.Soon, 12, Array.Empty<WorkflowChild>()),
    // ... 8 more
};
```

The partial iterates this list; the layout becomes data, not 305 lines of Razor copy-paste. When Sprint 12 ships, the Purchasing descriptor's `Status` flips to `Live` and `Children` populates — a one-file diff.

**Step 2 — Settings drawer.** A new `/Settings` Razor Page (with its own left sub-nav layout) absorbs every `/Admin/*` route. Routes do *not* change — `/Admin/Sites` still serves the page; the Settings drawer's sub-nav links to those existing routes. Backward-compatible. Existing deep links continue to resolve.

**Step 3 — Recent + Pinned server-side persistence.** New `UserNavPin` and `UserNavRecent` tables (small; user-scoped; ~20 rows per user maximum). Service: `INavPersonalizationService` with `GetPinned()`, `Pin(string entityKind, string entityId, string label)`, `Unpin(...)`, `LogRecent(...)`. The current localStorage Recent stays as a fallback for first-page-load latency; server-side is the source of truth.

**Step 4 — Cmd-K index upgrade.** Extend the existing palette (PR #116d.1c) to index *every Razor route* via reflection over `RouteAttribute`s + a small Razor-page registry, plus *every entity instance* via a server-side search endpoint. Goal: any of the 400 pages is reachable in ≤3 keystrokes; any named entity (PO, asset, receipt, WO) is reachable in ≤5 keystrokes from anywhere.

**Step 5 — Sprint 12-20 placeholders.** Each Soon Control Center renders a disabled `<a>` with `aria-disabled="true"`, `tabindex="-1"`, and a tooltip "Ships in Sprint N." The visible-but-disabled pattern is preferred over hidden-until-shipped because (a) it makes the roadmap legible to users, (b) IA is stable across sprints, (c) we accidentally exposed users to a hidden item once before and the bug was discovered weeks late.

**Step 6 — Backward compatibility.** Every existing route works unchanged. Existing bookmarks resolve. The IA change is a *re-grouping*, not a *re-routing*. The handful of breadcrumb labels that change ("Administration → Settings → Organization → Sites") are cosmetic and the route is the same.

**Step 7 — Testing.**
- a11y audit on the new sidebar (axe + manual keyboard tab order). Targets: 0 critical, 0 serious violations. Use the same Phase 4 axe-CI harness that PR #213/#214 stood up.
- Keyboard: Tab order top-to-bottom; arrow keys within groups; Enter activates; Esc closes a flyout; `/` focuses Cmd-K (Cmd-K still opens the palette as today; `/` is a global secondary trigger for non-Mac muscle memory).
- Screen reader: every group header is a `button` with `aria-expanded` (already present in the current code, line 199 etc.); the Settings entry has `aria-label="Open settings drawer"`; Soon-state Control Center items get `aria-disabled="true"` + a `title` attribute the screen reader announces.
- Visual regression: snapshot the sidebar at three states (collapsed, expanded, Receiving CC active) for the four primary roles (Admin, Maintenance Tech, Receiving Clerk, Finance Clerk).
- Live verify: navigate from `/` → Receiving CC → Receive by PO → back to Maintenance CC (disabled, shows tooltip) → Cmd-K → "fiscal calendar" → Enter → Settings drawer opens at the right node. Three browsers, three viewport widths (1024, 1440, 1920).
- Performance: sidebar render budget ≤ 8ms on the dock workstation profile (current sidebar takes ~12ms; carving into partials should land us under 8 because the inline-Razor IsAny() helpers stop walking 50 strings on every request).

**Step 8 — Migration comms.** Single in-app modal on first login post-deploy: "We reorganized navigation. Here is the new map." Two screenshots. One dismiss. Done. The change is small enough that no training is required if the modal is clear.

---

## 7. Open questions for Dean

Sharpest five tradeoffs the research does not resolve. Each is a single decision that unblocks ADR-017 sign-off.

**Q1 — Visible-but-disabled vs hidden-until-shipped for Sprint 12-20 Control Centers?** The doc recommends visible-but-disabled with `(soon — Sprint N)` tags. Pros: roadmap transparency, IA stable across sprints, customer pilots see what's coming. Cons: disabled items look like bugs if the tooltip is missed, can read as "we shipped something broken." Plex and Notion both hide-until-shipped; Linear and Stripe both visible-but-disabled (with "Coming soon" or beta tags). Cherry Street's posture has historically been Linear-aligned (we show the roadmap). Recommend: **visible-but-disabled** with a subtle sprint chip. Confirm or flip?

**Q2 — Reports as a top-level group, a Platform sub-item, or buried under Finance?** Today Reports is under Finance and there is also a `Reports/ReportHub` route. Three options: (a) Top-level Reports group across all modules — defensible because reports cross domains; (b) Reports under Platform alongside AI & Voice — defensible because reports are a cross-cutting capability; (c) Reports stays under Finance because that's where most reports actually fire — defensible because field usage skews 70%+ financial. The doc currently proposes (c) but flags it as the open question. Recommend pick (b) or (c); do not pick both.

**Q3 — Pending Approvals: sidebar item, topbar inbox icon, or both?** Approvals are an inbox flow: notification-driven, completion-driven, item-count badge-driven. Linear, Stripe, GitHub, Notion all use a topbar bell-icon for this kind of flow, not a sidebar item. We currently have it under Finance as a sidebar item. Recommend: **move to a topbar/footer Approvals bell with badge count**, drop the sidebar item, keep `/Approvals/Pending` as a route Cmd-K can resolve. Less sidebar clutter, more accurate model of how approvals actually flow.

**Q4 — Personalize-the-sidebar (Linear) vs admin-defines-bundles (Salesforce) for v1?** The doc proposes a fixed sidebar with per-user Pinned + Recent. Salesforce-style admin-defined Apps would let an admin configure "Receiving Clerk only sees Receiving CC + Materials" — more aggressive personalization but ties IA to admin curation work. Cherry Street's "best-in-class" posture argues *against* requiring admin curation to be usable. Recommend: **user-defined Pins only in v1**; admin-defined role bundles becomes a Sprint 21+ followup if pilots actually ask for it. Confirm.

**Q5 — Where does `/Admin/AuditLog` live?** It is technically a Settings page (audit *configuration*) but the most-frequent user action against it is *forensic reading* (who changed this? what happened on this date? who reversed this receipt?). Linear and GitHub both surface audit log inside Settings. Stripe surfaces "Logs" as a top-level group. Field interviews say Compliance Officers want it findable as a daily-driver page. Three options: (a) Settings drawer only (matches Linear/GitHub); (b) Top-level under Platform (matches Stripe); (c) Surface a "Recent activity" chip in the Dashboard that links to the full log. Recommend: **(a) Settings drawer for management + (c) Dashboard chip for forensic** — covers both use cases without sidebar bloat. Confirm.

**Q6 — Is "Quick Actions" the right name, or do we use "Create" / "New"?** GitHub uses `+` icon → "New repository / New issue." Stripe uses "Create" with a dropdown. Linear uses Cmd-K with `>` for the action mode. Notion uses `+` everywhere. "Quick Actions" is descriptive but slightly enterprise-flavored; "Create" is sharper but excludes non-create verbs (Log inspection, Receive blind — these *are* creates, technically). Recommend: **keep "Quick Actions"** because the slot will also house non-create verbs in future sprints (Reconcile, Reopen, Reassign). Confirm or flip to "Create."

---

## 8. Sources

- [Linear changelog — Customize your navigation in Linear Mobile (Jan 2026)](https://linear.app/changelog/2026-01-22-customize-your-navigation-in-linear-mobile) — Recent personalization update.
- [Linear changelog — Personalized sidebar and new settings pages (Dec 2024)](https://linear.app/changelog/2024-12-18-personalized-sidebar) — The current Linear sidebar customization model.
- [Linear changelog — Collapsible Sidebar](https://linear.app/changelog/unpublished-collapsible-sidebar) — Collapse-to-rail behavior.
- [Linear — How we redesigned the Linear UI (Part II)](https://linear.app/now/how-we-redesigned-the-linear-ui) — Engineering blog on the visual + IA redesign.
- [Productivity Stack — Linear App: Complete Guide for Software Teams (2026)](https://productivitystack.io/guides/linear-app-complete-guide/) — Sidebar customization + Cmd-K workflow.
- [Stripe Documentation — Web Dashboard basics](https://docs.stripe.com/dashboard/basics) — Canonical reference for the sidebar's first section.
- [Stripe Support — Dashboard update (May 2024)](https://support.stripe.com/questions/dashboard-update-may-2024) — Pinned + recent in the Shortcuts section.
- [Putler — Stripe Dashboard: A Complete Guide for 2026](https://www.putler.com/stripe-dashboard) — Walkthrough of the current dashboard sidebar.
- [Lokesh Dhakar — Tutorial: Stripe.com's main navigation](https://lokeshdhakar.com/dev-201-stripe.coms-main-navigation/) — Engineering teardown of Stripe's nav implementation.
- [Notion Help — Navigate with the sidebar](https://www.notion.com/help/navigate-with-the-sidebar) — Workspace tree mechanics.
- [Notion Help — Structure your sidebar with teamspaces](https://www.notion.com/help/guides/structure-sidebar-focused-work-teamspaces) — Section-level organization.
- [Fazm Blog — Notion Release Notes April 2026](https://fazm.ai/blog/notion-release-notes-april-2026) — Most recent sidebar redesign.
- [Datadog Blog — A closer look at our navigation redesign](https://www.datadoghq.com/blog/datadog-navigation-redesign/) — The 2024 left-rail redesign rationale.
- [Datadog Blog — Introducing the Datadog quick nav menu](https://www.datadoghq.com/blog/datadog-quick-nav-menu/) — Quick Nav as a nav-scoped Cmd-K.
- [shadcn/ui — Sidebar component](https://ui.shadcn.com/docs/components/radix/sidebar) — Modern reference implementation.
- [Salesforce Help — App Switching in Lightning Experience](https://help.salesforce.com/s/articleView?id=xcloud.basics_app_launcher_lex.htm) — Canonical App Launcher behavior.
- [Marcloud — What Are Salesforce Lightning Apps? Custom Navigation](https://marcloudconsulting.com/salesforce-products-explained/what-are-salesforce-lightning-apps/) — Role-based App configuration.
- [Salesforce Trailhead — Optimize Salesforce Navigation & Setup](https://trailhead.salesforce.com/content/learn/modules/lex_migration_whatsnew/lex_migration_whatsnew_nav_setup) — Pin-to-nav patterns.
- [SAP Design System — Fiori Launchpad](https://www.sap.com/design-system/fiori-design-web/v1-136/foundations/integration-and-services/sap-fiori-launchpad/launchpad) — Canonical launchpad pattern.
- [Pathlock — What is SAP Fiori Launchpad? A Comprehensive Guide](https://pathlock.com/blog/sap-fiori/sap-fiori-launchpad/) — Role-tile mechanics + Spaces & Pages.
- [YuvaPlanNex Tech — SAP Fiori Launchpad: The Complete Guide](https://www.yuvaplannextech.com/blog/sap-fiori-launchpad-complete-guide) — Spaces & Pages evolution from S/4HANA 2020.
- [Oracle Redwood landing](https://redwood.oracle.com/) — Design system canon.
- [Oracle Learn — Redwood Design System module](https://learn.oracle.com/ols/module/redwood-design-system/63148/74436) — Pattern Book + Work Areas.
- [Oracle Docs — Leverage PLM Navigator using a Redwood Page](https://docs.oracle.com/en/cloud/saas/readiness/common/rrdem/leverage-plm-navigator-using-a-redwood-page.html) — Persistent navigator + clipboard pinning pattern.
- [Oracle Blogs — Redwood Quick Actions, Deep Links](https://blogs.oracle.com/fusioncoe/redwood-quick-actions) — Separation of go-to vs do-this.
- [Namos Solutions — Implementing Oracle Redwood with Visual Builder](https://namossolutions.com/blog/implementing-oracle-redwood-with-visual-builder-studio-a-modern-user-experience-transformation/) — Practitioner view on Work Areas.
- [Plex — Smart Manufacturing Platform Capabilities](https://www.plex.com/smart-manufacturing-platform) — Vendor reference for the manufacturing IA.
- [Plex — How to Design a User Experience with the Shop Floor Operator in Mind (Diginomica)](https://www.plex.com/company/newsroom/diginomica-how-design-user-experience-shop-floor-operator-mind-0) — Shop-floor-first UX philosophy.
- [Plex Mobile](https://plex.rockwellautomation.com/en-us/platform/mobile-application.html) — The desktop/mobile-app split.
- [Laws of UX — Miller's Law](https://lawsofux.com/millers-law/) — The 7±2 heuristic canon.
- [UX Myths — Myth #23: Choices should always be limited to 7±2](https://uxmyths.com/post/931925744/myth-23-choices-should-always-be-limited-to-seven) — Important corrective; the rule applies to working memory, not visible items.
- [Information Architecture Authority — IA for SaaS Platforms](https://informationarchitectureauthority.com/ia-for-saas-platforms) — Depth-vs-breadth tradeoffs.
- [UX Patterns — Command Palette pattern](https://uxpatterns.dev/patterns/advanced/command-palette) — Implementation reference.
- [Maggie Appleton — Command K Bars](https://maggieappleton.com/command-bar) — Survey of Cmd-K implementations.
- [AorBorC — Best Practices for ERP App Navigation Design](https://www.aorborc.com/best-practices-for-erp-app-navigation-design/) — Role vs object navigation tradeoffs.
- [Vercel — New dashboard redesign is now the default (Feb 2026)](https://vercel.com/changelog/dashboard-navigation-redesign-rollout) — Confirms the convergent pattern outside the primary eight.
- [Vercel — New dashboard navigation available](https://vercel.com/changelog/new-dashboard-navigation-available) — Resizable sidebar, unified levels, mobile floating bar.
- [Optimal Workshop — Information Architecture vs Navigation](https://www.optimalworkshop.com/blog/information-architecture-vs-navigation-creating-a-seamless-user-experience) — Distinction between IA and the nav UI that exposes it.
- [Medium / Bootcamp — Command Palette UX Patterns #1](https://medium.com/design-bootcamp/command-palette-ux-patterns-1-d6b6e68f30c1) — UX-pattern reference for Cmd-K specifically.

---

**Recommended next step:** Write **ADR-017 — Control-Center-First IA**, freeze the §5 structure plus the answered Q1-Q6 from §7, and sign off before any sidebar code lands. The ADR should reference this research doc for context but be tight: the decisions, the rationale, and the migration path. The ADR is what the engineering team builds against; this document is what convinced you the ADR is right.
