---
title: Luxury Multi-Vertical Cockpit UX — Locked Baseline
date: 2026-05-23
status: Locked (Dean's decisions confirmed)
sprint: 13.5
gates: PR #4 (Manufacturing UI shell) + every UI PR after
companion: wwwroot/css/design-tokens-v2.json
---

# Luxury Multi-Vertical Cockpit UX — Locked Baseline

This memo is the design-language source-of-truth for every IndustryOS Control
Center cockpit, detail page, and Razor surface shipped from Sprint 13.5 PR #4
onwards. It accompanies the locked design tokens in
`wwwroot/css/design-tokens-v2.json` (321 lines of granular decisions) and the
Master Files audit at `docs/research/master-files-audit.md`.

**Multi-vertical reality.** IndustryOS serves machining (ABS Thursday demo),
ETO precision (EVS June 3), AND food sciences, pharma, cannabis, electronics,
aerospace, defense. Project Management is an OPTIONAL layer that any vertical
can turn on. The UX baseline below was designed against that constraint —
nothing in here forks for a vertical.

---

## 1. Locked decisions (Dean, 2026-05-23)

| # | Decision | Choice | Rationale |
|---|---|---|---|
| 1 | `Tenant.Mode` enum + DEMO band in PR #4 | **YES — include** | 2-line schema add. Lets sales drop a tenant into Demo for ABS / EVS pitches with a subtle but unmissable banner. Future-proofs Sandbox + Maintenance modes. |
| 2 | Cherry Bar (voice trigger) position | **Top-right header pill + cmd-K from anywhere** | Restrained. Reads "tool among tools," matches Hermès-of-enterprise direction. Stripe's top-center bar rejected as too loud. Cmd-K opens the bar from any page. |
| 3 | First-project Mode catalog depth | **HYBRID** — 3 buckets visible (Standard / EngineerToOrder / Aerospace) + "Show all 17 verticals" advanced link | Friction-free for the 90%, depth for power users. Vertical-specific chips materialize once an Item is added (driven by ItemMaster's ReceiptProfile). |
| 4 | `Company.IndustryVertical` enum scope | **17-value list** (locked) | `0=Unspecified, 1=Machining, 2=MetalFab, 3=PrecisionEto, 4=FoodSciences, 5=Pharma, 6=Cannabis, 7=Electronics, 8=Aerospace, 9=Defense, 10=MedDevice, 11=OilGas, 12=Automotive, 13=Chemicals, 14=Apparel, 15=Construction, 16=GeneralMfg` (17-31 reserved). |

---

## 2. The four dimensions (summary)

### Dimension 1 — Modern luxury UX language

**Tokens locked in `design-tokens-v2.json`:**

- **Type:** Inter + JetBrains Mono only. Geist-derived 14-size scale (display-72 … mono-13). Negative tracking (-0.04em at 32px+, -0.045em at 56-72px) is the single most-expensive tell.
- **Spacing:** 4-px base + asymmetric values (6, 12, 20, 40, 48, 96). Asymmetry IS the luxury read.
- **Motion:** durations 100/160/220/320 ms, cap 400. ONE enter ease `cubic-bezier(0.2,0,0,1)`, ONE exit `(0.4,0,1,1)`. M3 springs rejected as too playful.
- **Color discipline:** monochrome chassis + one Cherry red accent + tri-state semantic (green/amber/red). No info-blue, no purple-AI.
- **Elevation:** three tiers max (flat / raised / floating). Borders > shadows.
- **Radius:** 2/4/6/8/12/16. Default 4-6.
- **Focus ring:** 2-px solid accent, 2-px offset. NEVER Tailwind box-shadow.
- **DataTable:** 32-px row, tnum + cv11 OpenType features on every numeric. No striped rows.
- **Density:** comfortable (56-px row, detail) / compact (40-px, queue) / dense (32-px, DataTable). Cockpit picks; no user toggle in v1.

**Reference products studied:** Linear, Notion, Vercel, Stripe, Airtable, Figma,
Pitch, Things 3, Cron/Notion Calendar, Height, Acumatica AI Studio 2026 R1,
SAP Fiori 3, Microsoft Fluent 2, Material 3 Expressive (rejected).

### Dimension 2 — Project Management as universal optional layer

**Pattern locked:**

`/CustomerProjects` nav item appears when the tenant has ANY active project
OR the user has `projects.create` permission. Otherwise hidden — zero
"empty modules in your sidebar" friction.

First project creation = three-field card (Name, Customer, Mode) with NO
sample data. Mode picker = 3 buckets visible (Standard / EngineerToOrder /
Aerospace) + "Show all" link reveals full 17-value vertical catalog.

On every ProductionOrder / WorkOrder / Job page, a `Linked to project` chip
renders ONLY when `CustomerProjectId IS NOT NULL` — invisible otherwise,
never reserves whitespace.

**Reference:** Linear Initiatives opt-in, Notion AI Autofill database
properties, Stripe Mode: Test pattern, Asana empty-state PM onboarding.

### Dimension 3 — Vertical overlays via `<vertical-chip>` Tag Helper

**THE primitive:** a 22-px tall, 6-px radius, monochrome chip with a
single-letter mono glyph and an optional severity recolor (text + ring,
NEVER background). Resolves visibility from the ADR-015 ReceiptProfile
waterfall (Customer.IndustryVertical → Item.DefaultReceiptProfileId →
explicit Project.VerticalOverride).

**Glyphs (locked):**

| Glyph | Meaning | Verticals |
|---|---|---|
| F | FAI required | Aerospace, Defense |
| L | Lot tracking | Food, Pharma, Cannabis |
| N | NDC | Pharma |
| M | METRC tag | Cannabis |
| U | UDI | MedDevice |
| K | MSL level | Electronics |
| E | Export-controlled | Aero, Defense, Electronics |
| C | Cold-chain | Pharma, Food |
| A | Allergen | Food |
| R | RoHS / REACH | Electronics, Apparel |
| T | Test/Demo mode | All (Tenant.Mode banner) |
| X | Expiry-dated | Pharma, Food |

**Render locations:**
- Page header: max 3 chips + "+N more" overflow pill
- Queue row: max 2 chips inline
- DataTable cell: inline plain text (no chip frame)
- Preview-pane footer: full list, expandable

**Empty = absent, NEVER whitespace-reserved.** A commercial-only project
with no vertical signals shows zero chips. Aero/Def project shows F + E
chips, restrained, present-but-not-dominating.

### Dimension 4 — Voice-first overlay placement

**Cherry Bar** = top-right header pill (between vertical-chip slot and user
avatar). 36-px collapsed, 240-px on hover/focus.

- **Cmd-K** opens the bar from anywhere on the page
- **Cmd-Shift-V** opens with voice already active
- **Modal** appears centered top-of-viewport, 720-px max-width, 12-px radius, floating elevation
- **Interim transcript** rendered in `text-3` gray (Granola pattern)
- **Final transcript** in primary
- **Voice-active indicator** = low-amplitude header waveform + 2-px hairline pulse at top of viewport
- **NO screen darkening, NO modal lockout** — user can keep interacting while voice listens
- **Multi-turn** stays in the bar — no chat-window proliferation

**Rejected:** floating mic FAB (per prior feedback), sidebar voice pane
(Acumatica anti-pattern), centered-overlay modal that blocks interaction.

---

## 3. Anti-patterns — NEVER

1. **Striped table rows** — luxury reads as restraint; alternating-color rows scream "data dump"
2. **More than 3 type weights on one screen** — discipline > variety
3. **Box-shadow focus rings** (Tailwind default) — use solid border + offset
4. **Spring or bounce motion** — M3 Expressive rejected; we are not playful
5. **Vertical-chip background recolor** — recolor text + ring ONLY; background stays neutral
6. **Whitespace-reserved chip slots** — empty = absent (no "ghost chip" placeholders)
7. **Sidebar voice pane** — bar-in-header is THE pattern
8. **Floating FAB voice trigger** — already removed once; do not reintroduce
9. **Custom font in any cockpit surface** — Inter + JetBrains Mono only
10. **Modal lockout on voice listen** — voice never blocks user from continuing to interact

---

## 4. PR #4 / PR #5 specific application

### PR #4 — `/Production` UI shell

- Page header: page title (display-32, -0.04em tracking) + vertical-chip slot + Cherry Bar
- List view: 40-px row compact density, no stripes, mono-13 for PRO numbers
- Detail view: 56-px row comfortable density, 24-px gap rhythm, preview-pane on right (4-col grid)
- DEMO band: yellow ribbon, label-12 uppercase, top of viewport, dismissable per-session

### PR #5 — `/CustomerProjects/{id}` Customer Project Cockpit

- ADR-018 cockpit triad: queue-left (40-px row) + preview-right + KPI band-top
- KPI band: 4 hero tiles, display-40 numerals, label-12 labels uppercase
- "Next Up" priority preview: queue-row hover surfaces it
- "AI Suggests" panel: bottom-right, restrained, action-suggestion chips
- Vertical chips on every queue row (max 2) + on header (max 3 + overflow)
- Cherry Bar always visible top-right

---

## 5. Deferred to v2

- Per-user density preference (cockpit picks for v1)
- Multi-color RAG variants beyond green/amber/red
- Per-tenant brand color override (single accent locked at Cherry red for v1)
- Dark mode (light-mode-first for v1; tokens already support invert)
- Custom dashboard layouts (cockpit triad is THE pattern for v1)
- Cmd-K natural-language search (voice covers it via Cherry Bar)
- Per-vertical onboarding flows (3-bucket Mode picker covers it)

---

## 6. Cross-refs

- `wwwroot/css/design-tokens-v2.json` — the actual tokens (321 lines, granular)
- `docs/research/master-files-audit.md` — companion audit (Master Files for vertical chip data sourcing)
- `docs/research/customerproject-field-set.md` — PR #1.5 research (style template)
- `docs/research/fai-workflow-schema.md` — PR #1.75 research (style template)
- ADR-018 — Cockpit-First Pattern (live precedent on `/Receiving`)
- ADR-014 — Voice infrastructure (the substrate Cherry Bar runs on)
- ADR-015 — ReceiptProfile waterfall (drives vertical-chip visibility)

---

## 7. Note on recovery

The original 790-line research memo lived only in the memory directory which
was wiped on 2026-05-23. This workspace memo is the reconstructed source-of-
truth derived from (a) the original agent's executive summary captured in
session, (b) the surviving `design-tokens-v2.json` (321 lines of granular
detail), and (c) Dean's locked decisions on 2026-05-23. Future research
memos should land in `docs/research/` directly to survive memory wipes.
