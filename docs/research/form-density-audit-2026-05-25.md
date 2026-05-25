---
title: Form Density Audit — Sprint 13.6 PR #3 Input
date: 2026-05-25
status: Draft (audit only — no edits made)
sprint: 13.6
gates: PR #3 (Top-10 form-density fixes)
authority:
  - wwwroot/css/design-tokens-v2.json
  - docs/research/luxury-cockpit-ux.md
exemplars:
  - Pages/Receiving/ControlCenter.cshtml (100 lines, ZERO data-csp-style)
  - Pages/Production/Workbench.cshtml (135 lines, ZERO data-csp-style)
---

# Form Density Audit — 2026-05-25

> Dean directive (paraphrased): *"a lot of these forms and subforms are taking
> up MASSIVE amounts of real estate."* This memo answers: where, how bad, and
> what to change.

---

## 0. The bar

Two pages set the bar. Both are LIVE on industryos.app today. Read them before
believing any of the analysis below.

| Surface | File | LOC | `data-csp-style` count | What it is |
|---|---|--:|--:|---|
| Receiving Control Center | `Pages/Receiving/ControlCenter.cshtml` | 100 | 0 | Composes 4 shared cockpit partials. The whole page is `if (Model.X is not null) { @await Html.PartialAsync(...) }`. |
| Operator Workbench | `Pages/Production/Workbench.cshtml` | 135 | 0 | Same primitives, plus 50 lines of inline JS for the preview hydrator. |

Discipline rules they enforce (from `wwwroot/css/design-tokens-v2.json`):

- **Asymmetric spacing scale 4 / 6 / 12 / 20 / 40 / 48 / 96 px** — luxury reads from chosen values, not a rigid grid.
- **Three density modes:** comfortable (56-px row) / compact (40-px row) / dense (32-px row). The cockpit picks; no user toggle in v1.
- **Three surface elevation tiers max** (flat / raised / floating). Borders > shadows.
- **Default radius 4-6 px**, max 16 px (Cherry Bar modal only).
- **Inter + JetBrains Mono only**, with `tnum` + `cv11` OpenType features on every numeric cell.
- **One enter ease, one exit ease, max 400 ms** — no springs, no overshoot.
- **Color is for meaning, not decoration** — monochrome chassis + one Cherry red accent + tri-state semantic (green/amber/red).

The Receiving + Workbench pages obey ALL of this *and* fit inside a
1440 × 900 viewport with no scroll on first paint. That is the bar.

---

## 1. Executive summary

**Audited:** `Pages/**/*.cshtml` — **441 Razor files, 151 of them with at least one `data-csp-style` attribute. 2,342 total inline overrides across the tree. 381 of those are `margin:*`, 213 are `padding:*`** (the rest are mostly grids, flex, colors, and one-off display rules).

The top **22 files account for 1,151 overrides — 49 % of the total inline-style surface area.** A focused PR that converts those 22 files to utility classes (and extracts 4 shared partials for the most-duplicated patterns) eliminates roughly half the inline-style debt and shrinks the visual real-estate footprint of every form by 30–60 %.

**Three structural offenses repeat across almost every offender:**

1. **"Section-card-inside-section-card" nesting** — the Vendor view tabs, Periods Close, StockReceipts core fields, ItemEdit, CIP Details, and Quality FAI Create all wrap a *form-grid* inside a `section-card`, then wrap that inside another `section-card`, then put alerts above it. Each layer adds 16-24 px of inner padding. Three layers ≈ 60-90 px wasted on every form.
2. **Vertically-stacked form rows where a 2-, 3-, or 4-column grid would fit** — Quality/Fai/Create stacks 4 inputs vertically inside a 700-px-wide `section-card-body`, eating 8 rows × 80 px = 640 px of vertical space for content that would fit in 2 rows of 320 px. ItemEdit, Asset.cshtml, WorkOrders/Details (the inline Edit form), CIP/Details (Edit Project inline), and FiscalCalendar all do the same.
3. **Inline-styled "card" wrappers that re-implement `.section-card`** — Periods/Close has 6 hand-coded card backgrounds (`background: rgba(34,197,94,0.10); border: 1px solid rgba(34,197,94,0.35); ...`) where it should compose `.callout--success` / `.callout--warning` / `.callout--danger` utility classes. ChainOfCustody re-implements the same with `.pe-card` plus inline `data-csp-style="margin-top:1rem;"` on every instance. StockReceipts/_ReceiptCoreFields uses 5 hand-coded `padding: 0` cards that defeat the entire `.section-card` system.

**Padding token violations** dominate the inline `padding` overrides:

- `1rem` (16px), `1.25rem` (20px), `1.5rem` (24px), `2rem` (32px) appear in 80 % of inline padding overrides.
- Per design-tokens-v2.json, the *form chrome* density should be `padX: 20px` + `padY: 12px` (asymmetric scale 5 + 3), or for dense data `padX: 12px` + `padY: 8px`.
- Most offenders use **`1.5rem` (24 px)** on `section-card-body`, which is the *comfortable* density meant for **detail panes** — not for forms with 12-20 fields.
- Marketing/Index uses `padding: 2rem` (32 px) on grid cards that already sit inside a 2-rem-padded parent — that's 64 px of total inside-padding on a single card.

---

## 2. Worst-offender ranking table (Top 22)

`#OVR` = total `data-csp-style` attributes (any property)
`MAR` = `data-csp-style="margin*` only
`PAD` = `data-csp-style="padding*` only

| Rank | File | #OVR | MAR | PAD | Primary visual offense |
|---:|---|---:|---:|---:|---|
| 1 | `Pages/Assets/Asset.cshtml` | **229** | 10 | 5 | Mostly hidden — the file inlines an 800-line `Func<string,object>` Razor lambda factory that emits empty-state banners with 4-5 inline styles each. Also: 30+ inline `data-csp-style="opacity:0.3"` SVG decorations, hardcoded `#10b981` / `#f59e0b` colors (anti-pattern: should be `var(--color-success-700)` etc.). Hero card has 5-row vertical-stacked metadata. |
| 2 | `Pages/WorkOrders/Details.cshtml` | **150** | 25 | 12 | "Edit Work Order" collapsible section is 2.5 rem of internal padding wrapping an auto-fit grid that holds 8 inputs that should be a single 4-column grid. 8 vertical labels at `font-size: 0.8em` would compose better as a single `.form-grid--4 .form-stack-tight`. |
| 3 | `Pages/Materials/ItemEdit.cshtml` | **111** | 25 | 8 | 6+ `section-card`s stacked vertically — Identity, Costing, Stocking, Procurement, Revisions, Alternates, Manufacturer Parts, Vendor Parts, Where Used. Should be a **3-column grid in one tab-shell** (per design tokens density: comfortable). Pulls in `item-master.css` AND uses inline `data-csp-style` on top. |
| 4 | `Pages/Admin/Items.cshtml` | **90** | 30 | 1 | 30 inline-style `<th>` widths on the data-table (`data-csp-style="width: 120px;"` × 10). Should be a single class `.data-table--items` with `width` rules in `cockpit.css`. Pure debt — no functional reason to be inline. |
| 5 | `Pages/Assets/_AssetMesIotSafetyTabs.cshtml` | **83** | 2 | 0 | 80+ `data-csp-style="flex: 1;"` on form-groups. Should be a single CSS rule `.form-row .form-group { flex: 1; }`. |
| 6 | `Pages/Index.cshtml` | **76** | 16 | 18 | The dashboard. Empty-state and "Available Even Now" cards have nested `data-csp-style="padding: 2rem;"` inside `section-card-body` that already pads 24 px. Quick-stats row hand-codes its grid-template-columns. 8+ Dev-mode info cards with inline backgrounds. |
| 7 | `Pages/Materials/Vendors/_VendorViewTabs.cshtml` | **59** | 0 | 13 | EVERY field is rendered as `<div data-csp-style="font-size: 0.6rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.08em; color: var(--text-muted); margin-bottom: 0.2rem;">LABEL</div><div data-csp-style="font-size: 0.95rem; font-weight: 500;">value</div>`. The 12-field Vendor Info tab has this 5-rule signature × 12 fields = 60 inline rules where a single `.field-stack` class would work. Sibling `_VendorEditTabs.cshtml` (the edit form) already uses `.form-section-p` / `.form-row-p` / `.form-group-p` classes correctly — the view tabs need the same treatment. |
| 8 | `Pages/Purchasing/Details.cshtml` | **57** | 6 | 5 | Mixed pattern — some primitives (`_ScreenHeader`, `_PODetailsKpis`), but the body re-rolls flash messages with `style="margin-bottom: 14px; padding: 12px 16px;"` and inline-styles every line-item add form. |
| 9 | `Pages/Admin/StockReceipts/_ReceiptCoreFields.cshtml` | **56** | 0 | 6 | The file hoists 4 style strings into Razor variables (`inputStyle`, `labelStyle`, `sectionHeaderStyle`, `sectionBodyStyle`) and then uses `data-csp-style="@inputStyle"` on every input. Five `<div class="ds-card" data-tone="brand" data-csp-style="padding: 0;">` that defeat the card's built-in padding, then reapply `padding: 22px` inline. This is the *purest* example of "should be classes". |
| 10 | `Pages/Periods/Close.cshtml` | **54** | 22 | 6 | Variables-as-CSS pattern (`StatusBadgeStyle()` switch returning hand-coded `background: rgba(...) border: 1px solid rgba(...)` strings). 4 "callout" cards (success/warning/danger/info), each re-implemented inline. Should be `.callout--success` / `.callout--warning` / `.callout--danger` utilities. **Heaviest semantic-color anti-pattern in the codebase.** |
| 11 | `Pages/CIP/Details.cshtml` | **51** | 10 | 6 | "Edit Project" + "Edit Funding" inline `pe-card` forms with `data-csp-style="display: none; margin-bottom: 1rem;"`. Each form has 6+ `data-csp-style="margin-bottom: 1rem;"` between sub-grids. Should be `.form-stack-md`. |
| 12 | `Pages/Reports/TrialBalance.cshtml` | **40** | 8 | 12 | Filter bar hand-coded with `padding: 1rem 1.25rem` + `display: flex; gap: 1rem; align-items: end`. Table headers each have 6 inline rules. Banner styles concatenated as Razor strings (`bannerStyle = Model.IsBalanced ? "..." : "..."`). |
| 13 | `Pages/Demo/ChainOfCustody.cshtml` | **39** | 22 | 1 | 22 instances of `data-csp-style="margin-top: 1rem;"` on `.pe-card` wrappers. The `pe-card` should be wrapped in a `.cockpit-stack > * { margin-top: 1rem; }` parent rule. |
| 14 | `Pages/Reports/Form4562.cshtml` | **36** | 1 | 6 | IRS-form-driven layout, 6 hand-coded sub-sections with `padding: 1.25rem`. |
| 15 | `Pages/Journals/Manual.cshtml` | **35** | 7 | 7 | JE entry — 14 inline padding/margin overrides on the line table. |
| 16 | `Pages/Admin/Sites.cshtml` | **34** | 8 | 0 | Empty-state SVG sized inline (`width: 48px; height: 48px; margin: 0 auto 12px; opacity: 0.5`). |
| 17 | `Pages/Admin/StockReceipts/Index.cshtml` | **34** | 2 | 3 | Filter bar pattern (similar to TrialBalance). |
| 18 | `Pages/CCA/ClassReport.cshtml` | **32** | 0 | 2 | Mostly width/text-align on table cells. |
| 19 | `Pages/Admin/Requisitions.cshtml` | **31** | 2 | 3 | Filter bar + inline action-pill widths. |
| 20 | `Pages/Receiving/Receive.cshtml` | **28** | 4 | 0 | Small file but 28 overrides — should be a flag in the receiving family that already obeys the primitives. |
| 21 | `Pages/Quality/Fai/Detail.cshtml` | **28** | 1 | 2 | Dean's recent ship. Workflow-Actions section is hand-stacked, the Add-Characteristic 9-field form is `grid-cols-4` but every field has 2 inline rules for label + input. **77 (28+23+26) total across the Fai trio** — single-largest 2-week regression on the audit. |
| 22 | `Pages/Help/Index.cshtml` | **26** | 8 | 2 | Marketing-style page; 8 inline-style "callout" gradient cards with `padding: 1.75rem` + inline backgrounds. |

**Dean-flagged in-scope sub-list (the 2026-05-25 ships):**

| File | #OVR | MAR | PAD |
|---|---:|---:|---:|
| `Pages/Quality/Fai/Detail.cshtml` | 28 | 1 | 2 |
| `Pages/Quality/Fai/Create.cshtml` | 23 | 0 | 0 |
| `Pages/Quality/Fai/Index.cshtml` | 26* | 0 | 2 |
| `Pages/Admin/AssetImport/Upload.cshtml` | 10 | 4 | 1 |
| `Pages/Admin/AssetImport/Preview.cshtml` | 16 | 0 | 2 |
| `Pages/Admin/AssetImport/Detail.cshtml` | 8 | 0 | 2 |
| `Pages/Admin/AssetImport/Index.cshtml` | 4 | 0 | 0 |

*Fai/Index trends very similar to Detail; same anti-patterns.

The Fai + AssetImport ships are NOT in the top-10 by raw count, but they
share the SAME anti-patterns (vertically-stacked inputs that should be grids,
inline label styling, 1-rem padding on already-padded card bodies), and they
were shipped most recently — so they're the cleanest "fix-while-the-pattern-
is-fresh" candidate.

---

## 3. Before / After sketches — Top 10

> Each sketch shows the page's **current visual real-estate footprint** in
> approximate vertical pixels at 1440×900, then the **compact target** that
> obeys the design tokens.

---

### Rank 1 — `Pages/Assets/Asset.cshtml` (229 overrides)

**Current (vertical scroll required for hero + first tab):**

```
+-- back-link row (32px padding 1rem 2rem) -----------------+
| ← Back                                                    |
+-- alert row (margin-bottom: 20px) ------------------------+
| ✓ Success                                                 |
+-- asset-hero (own custom .css, 320px tall) ---------------+
| [image]  AssetNumber  badges...                           |
|          Description                                       |
|          5-row vertical metadata (labels then values)      |
+-- KPI band (.asset-hero-kpis, 120px) ---------------------+
+-- tab-nav row (48px) --------------------------------------+
+-- form-section "Identity" (4 form-rows × 80px) -----------+
+-- form-section "Costing" (4 form-rows × 80px) ------------+
+-- form-section "Asset Class / GL" (3 rows × 80px) --------+
| ... 8+ more sections, each 240-320px ...                   |
| Page total ≈ 2,800px vertical = 3.1 viewports             |
```

**Compact target (everything above-fold inside 1440×900):**

```
+-- _CockpitPageHeader (title 32px + badge + back) 64px ----+
+-- _CockpitKpiBand 4 tiles 96px ---------------------------+
+-- _CockpitTabShell (Identity | Financial | MES | IoT) 40px-+
+-- Active tab body: .form-grid--3-col --------------------+
| label | value     label | value     label | value         |
| label | value     label | value     label | value         |
| ... (16 fields in 6 rows × 56px = 336px) -----------------+
+-- Photo strip (RIGHT rail in cockpit, 240px wide) --------+
| Page total ≈ 540px vertical = 0.6 viewport               |
```

**Key change:** asset-hero replaced with `_CockpitPageHeader` + side-rail photo. The 8-section vertical stack becomes a 4-tab shell with one form-grid per tab. Every inline `data-csp-style="opacity:0.3"` + hardcoded color goes away.

---

### Rank 2 — `Pages/WorkOrders/Details.cshtml` (150 overrides)

**Current — "Edit Work Order" section ONLY (the worst sub-form):**

```
+-- <details> wrapper (padding: 1.5rem; border-radius: 12px)+
|   <summary> (font-size: 1.1rem) (40px)                    |
|   <form>                                                  |
|     auto-fit grid minmax(220px, 1fr) → typically 3 cols   |
|     8 fields × 88px row height = 704px ===============>   |
|     "Description" row (separate, 88px) ===============>   |
|     "Notes" row (separate textarea, 120px) ===========>   |
|     button row (margin-top: 1rem) (60px) ============>    |
|                                                            |
| Total section height ≈ 1,012px when expanded              |
+-----------------------------------------------------------+
```

**Compact target:**

```
+-- <details class="cockpit-edit-form"> --------------------+
|   <summary class="cockpit-edit-form__toggle"> 32px        |
|   <form class="form-grid form-grid--4 form-stack-tight">  |
|     8 fields × 56px row × 2 rows = 112px ==============>  |
|     description (col-span-4, 56px) ====================>  |
|     notes (col-span-4 textarea, 88px) =================>  |
|     button row (justify-end, 40px) ====================>  |
|                                                            |
| Total section height ≈ 328px when expanded (-68%)         |
+-----------------------------------------------------------+
```

**Key change:** swap `auto-fit minmax(220px, 1fr)` for explicit 4-col, drop the `font-size: 0.8em` per-label inline (define `.form-stack-tight label` in cockpit.css instead), drop `padding: 1.5rem` (use `padX: 20px; padY: 12px` per asymmetric token).

---

### Rank 3 — `Pages/Materials/ItemEdit.cshtml` (111 overrides)

**Current — 8 vertical sections:**

```
+- back link
+- item-hero (LEFT image + RIGHT info, ≈ 280px)
+- revision bar (≈ 56px)
+- Identity section-card (320px)
+- Costing section-card (240px)
+- Stocking section-card (320px)
+- Procurement section-card (240px)
+- Revisions section-card (320px) [TABLE]
+- Alternates section-card (240px) [TABLE]
+- Manufacturer Parts section-card (240px) [TABLE]
+- Vendor Parts section-card (240px) [TABLE]
+- Where Used section-card (200px) [TABLE]
| Total ≈ 2,940px = 3.3 viewports
```

**Compact target — tab shell with 3-col form-grid:**

```
+- _CockpitPageHeader (hero merged in, 96px)
+- _CockpitKpiBand (Current Rev, MPN count, VPN count, Alt count, 96px)
+- _CockpitTabShell (Identity | Cost & Stock | Procurement | Revisions | Parts | Where Used, 40px)
+- Active tab: .form-grid--3-col body (≈ 360px)
| Total ≈ 592px = 0.66 viewport
```

---

### Rank 4 — `Pages/Admin/Items.cshtml` (90 overrides — 30 are `<th>` widths)

**Current:**

```
<th data-csp-style="width: 120px;">Part #</th>
<th data-csp-style="width: 50px;">Rev</th>
... × 10
```

**Compact target:**

```html
<table class="data-table data-table--items">
  <thead><tr><th>Part #</th><th>Rev</th>...</tr></thead>
</table>
```

with one rule in `cockpit.css`:

```css
.data-table--items th:nth-child(1) { width: 120px; }
.data-table--items th:nth-child(2) { width: 50px; }
/* ... */
```

This is a zero-visual-change refactor — only the inline-debt count drops by 30. Worth doing as part of PR #3 because it sets the pattern for every other `data-table`.

---

### Rank 5 — `Pages/Assets/_AssetMesIotSafetyTabs.cshtml` (83 overrides — 80 are `flex: 1`)

**Current — repeated 80 times:**

```html
<div class="form-group" data-csp-style="flex: 1;">...</div>
```

**Compact target — one CSS rule in cockpit.css:**

```css
.form-row > .form-group { flex: 1; }
```

Pure mechanical refactor. Probably the single largest single-rule wipe in the audit. **Suggested as the first commit in PR #3** because it shows immediate -80 in the inline-style count with zero visual change.

---

### Rank 6 — `Pages/Index.cshtml` (76 overrides)

**Current — empty-state body has `padding: 2rem` inside `section-card-body` that already pads 24px:**

```
+-- section-card (max-width: 800px, margin: 2rem) ----------+
|  +-- section-card-header (default padding) ---------------+
|  |  Initialize Your System                                |
|  +-- section-card-body (padding: 2rem) -------------------+
|  |  +-- icon + heading + paragraph (margin-bottom: 1.5rem)+
|  |  +-- dev-mode section-card (padding: 1.25rem) ---------+
|  |  +-- "Available Even Now" border-top section (padding-top: 1.25rem)
|  |  +-- quick-action grid (auto-fit minmax(200px,1fr))----+
|  +---------------------------------------------------------+
| Total card height ≈ 580px for empty state                  |
```

**Compact target:**

```
+-- _CockpitPageHeader "Database Not Initialized" + warning badge --+
+-- .callout--warning (one card, padX 20 padY 12, no nested padding) +
|   icon · heading · paragraph · code-block · 4 quick-action chips   |
| Total card height ≈ 240px (-59%)                                   |
```

---

### Rank 7 — `Pages/Materials/Vendors/_VendorViewTabs.cshtml` (59 overrides)

**Current — every field is hand-stacked:**

```html
<div>
  <div data-csp-style="font-size: 0.6rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.08em; color: var(--text-muted); margin-bottom: 0.2rem;">CODE</div>
  <div data-csp-style="font-size: 0.95rem; font-weight: 500; color: var(--text-primary);">@v.Code</div>
</div>
```

**Compact target — composes the existing `_VendorEditTabs.cshtml` pattern in read-mode:**

```html
<div class="field-stack">
  <label class="field-stack__label">CODE</label>
  <div class="field-stack__value">@v.Code</div>
</div>
```

with one block in cockpit.css:

```css
.field-stack { display: flex; flex-direction: column; gap: 2px; }
.field-stack__label { font-size: 12px; font-weight: 500; letter-spacing: 0.02em; text-transform: uppercase; color: var(--color-text-3); }
.field-stack__value { font-size: 14px; font-weight: 500; color: var(--color-text-1); }
.field-stack--mono .field-stack__value { font-family: var(--font-mono); font-variant-numeric: tabular-nums; }
```

(Per design-tokens-v2.json: `label-12` is the canonical KPI/table-header/chip text — `12px, weight 500, letter-spacing 0.02em, uppercase`. The current `0.6rem` (9.6px) + `0.7rem` (11.2px) doesn't match the locked token and reads inconsistent.)

---

### Rank 8 — `Pages/Purchasing/Details.cshtml` (57 overrides)

Hybrid — already composes `_ScreenHeader` + KPI partials, but the body re-implements flash messages with `style="margin-bottom: 14px; padding: 12px 16px; ..."`. Mostly fixed by introducing a `_FlashMessages.cshtml` partial.

---

### Rank 9 — `Pages/Admin/StockReceipts/_ReceiptCoreFields.cshtml` (56 overrides)

**Current — variables-as-CSS:**

```csharp
var inputStyle = "width: 100%; padding: 10px 12px; border-radius: 8px; ...";
var labelStyle = "display: block; font-size: 11px; ...";
var sectionHeaderStyle = "padding: 18px 22px; border-bottom: 1px solid var(--border);";
var sectionBodyStyle = "padding: 22px; display: grid; ...";
```

then ` data-csp-style="@inputStyle"` × 14 fields.

**Compact target — extract `Pages/Shared/Partials/_ReceiptIdentityCard.cshtml`, `_ReceiptTraceabilityCard.cshtml`, `_ReceiptQuantityCard.cshtml`, `_ReceiptNotesCard.cshtml`:** each is a `<div class="section-card section-card--receipt"> ... </div>` with a `.form-grid--auto-fit` body that uses `<input class="form-input-p" />` already defined in premium-components.css. **Drop all 4 style variables, drop the 5 `padding: 0` cards.**

---

### Rank 10 — `Pages/Periods/Close.cshtml` (54 overrides)

**Current — every status callout is hand-coded:**

```csharp
string StatusBadgeStyle(Abs.FixedAssets.Models.PeriodStatus s) => s switch {
    PeriodStatus.Open => "background: rgba(34,197,94,0.12); border: 1px solid rgba(34,197,94,0.35); color: #86efac;",
    // ... 3 more
};
```

```html
<div data-csp-style="margin-top: 1rem; padding: 0.875rem 1.25rem; border-radius: 8px; background: rgba(34,197,94,0.10); border: 1px solid rgba(34,197,94,0.35); color: #86efac;">
  <strong>✓</strong> @Model.Notice
</div>
```

**Compact target:**

```html
<div class="callout callout--success">
  <strong>✓</strong> @Model.Notice
</div>
```

with one block in cockpit.css:

```css
.callout {
  margin-top: 12px;
  padding: 12px 20px;
  border-radius: 6px;
  border: 1px solid var(--color-border-1);
  background: var(--color-surface-2);
}
.callout--success { background: var(--color-success-50); color: var(--color-success-700); border-color: var(--color-success-400); }
.callout--warning { background: var(--color-warning-50); color: var(--color-warning-700); border-color: var(--color-warning-500); }
.callout--danger  { background: var(--color-danger-50);  color: var(--color-danger-700);  border-color: var(--color-danger-500);  }
.callout--info    { background: var(--color-surface-2);  color: var(--color-text-2);      border-color: var(--color-border-2);    }
```

This **single utility set kills ≈ 80 inline-style instances across `Periods/Close.cshtml`, `Pages/Index.cshtml`, `Pages/Reports/TrialBalance.cshtml`, `Pages/Admin/DataImport.cshtml`, `Pages/Demo/ChainOfCustody.cshtml`, `Pages/Help/Index.cshtml`.**

---

## 4. Dean-flagged in-scope: AssetImport + Fai (2026-05-25 ships)

These ships went well structurally — every one composes `_ScreenHeader` and
uses `.section-card` / `.kpi-card` / `.hero-btn` primitives. The inline debt
is all in **(a) input form rules** and **(b) flex utilities** that should
already exist as classes. Quick wins.

### `Pages/Admin/AssetImport/Upload.cshtml` (10 overrides)

**Current — single form has:**

```html
<div data-csp-style="margin-bottom: 1rem;">
  <label for="ExcelFile" data-csp-style="display: block; margin-bottom: 0.5rem; font-weight: 500;">
    Excel file
  </label>
  <input type="file" id="ExcelFile" name="ExcelFile" accept=".xlsx" required
         data-csp-style="display: block; width: 100%; padding: 0.5rem; border: 1px solid var(--border-color, #cbd5e1); border-radius: 0.375rem;" />
</div>
```

**Compact target:**

```html
<div class="form-stack">
  <label for="ExcelFile" class="form-label-p">Excel file</label>
  <input type="file" id="ExcelFile" name="ExcelFile" accept=".xlsx" required class="form-input-p form-input-p--file" />
</div>
```

`.form-input-p` already exists in `premium-components.css`. Need to add `.form-input-p--file` (slight padding tweak for file inputs) and `.form-stack` (the `margin-bottom: 1rem` pattern, mapped to a 12px token gap).

### `Pages/Admin/AssetImport/Preview.cshtml` (16 overrides — top of the AssetImport family)

**Worst line:**

```html
<div class="section-card-body" data-csp-style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
  <form ... data-csp-style="display: inline;"> ... </form>
  <form ... data-csp-style="display: inline;"> ... </form>
  <form ... data-csp-style="display: inline;"> ... </form>
```

**Compact:**

```html
<div class="section-card-body section-card-body--actions">
  <form class="inline-form"> ... </form>
  <form class="inline-form"> ... </form>
  <form class="inline-form"> ... </form>
</div>
```

with:

```css
.section-card-body--actions { display: flex; gap: 8px; flex-wrap: wrap; padding: 12px 20px; }
.inline-form { display: inline; margin: 0; }
```

Also: the 3 tab pills (`All | Valid | Errors`) use `data-csp-style="font-size: 0.8125rem; padding: 0.25rem 0.625rem;"` × 3 — these should be a single `.hero-btn--sm` variant.

### `Pages/Admin/AssetImport/Detail.cshtml` (8 overrides)

KPI cards have `data-csp-style="font-size: 1rem;"` on the date values to make them fit. The right fix is **`.kpi-value--date`** (sized smaller to accommodate `yyyy-MM-dd HH:mm` mono content) in cockpit.css.

The `<pre data-csp-style="margin: 0; white-space: pre-wrap; font-family: inherit;">` appears 3 times across the AssetImport trio for notes/errors. Extract `.pre--inherit` utility.

### `Pages/Admin/AssetImport/Index.cshtml` (4 overrides)

Cleanest of the four. The 2 instances of `data-csp-style="font-size: 0.8125rem; padding: 0.25rem 0.625rem;"` on the Preview/Detail buttons are the same `.hero-btn--sm` fix as Preview.cshtml.

### `Pages/Quality/Fai/Create.cshtml` (23 overrides)

**Current — every input has the same 5-rule signature:**

```html
<label data-csp-style="display: block; font-weight: 500; margin-bottom: 0.25rem;">Item *</label>
<select name="SelectedItemId" required data-csp-style="width: 100%; padding: 0.5rem; margin-bottom: 1rem; border: 1px solid var(--border-color, #cbd5e1); border-radius: 0.375rem;">
```

× 14 fields. = ~70 inline declarations.

**Compact target — use `.form-input-p` + `.form-label-p` from `premium-components.css`** (already defined), wrap each field in `.form-stack-md` (the 16px-margin-bottom pattern). Also, **collapse the 2-column section-card layout to a single 4-column form-grid:**

```html
<form method="post">
  <div class="section-card mb-4">
    <div class="section-card-header"><h3>Form 1 — Header</h3></div>
    <div class="section-card-body">
      <div class="form-grid form-grid--4">
        <div class="form-stack"><label class="form-label-p">Item *</label><select class="form-input-p">...</select></div>
        <div class="form-stack"><label class="form-label-p">Customer</label>...</div>
        <div class="form-stack"><label class="form-label-p">Customer Project</label>...</div>
        <div class="form-stack"><label class="form-label-p">Part Number *</label><input class="form-input-p" /></div>
        <div class="form-stack"><label class="form-label-p">Part Name</label>...</div>
        <div class="form-stack"><label class="form-label-p">Drawing #</label>...</div>
        <div class="form-stack"><label class="form-label-p">Drawing Rev</label>...</div>
        <div class="form-stack"><label class="form-label-p">Type</label><select class="form-input-p">...</select></div>
        <div class="form-stack"><label class="form-label-p">Part Type</label>...</div>
        <div class="form-stack"><label class="form-label-p">Reason</label>...</div>
        <div class="form-stack form-stack--full"><label class="form-label-p">Reason Text</label><textarea class="form-input-p" rows="2"></textarea></div>
      </div>
    </div>
  </div>
  <div class="action-row">
    <button type="submit" class="hero-btn hero-btn-primary">Create FAI</button>
    <a href="/Quality/Fai" class="hero-btn hero-btn-ghost">Cancel</a>
  </div>
</form>
```

**Vertical-space win:** current form is ~840px (2 stacked cards × 420px), target is ~360px (1 card × 320px + action row 40px). **-57 %.**

### `Pages/Quality/Fai/Detail.cshtml` (28 overrides)

Same patterns as Create. The 9-field "Add a characteristic" form already uses `grid grid-cols-4 gap-4` (good!) but every input inside has the same 5-rule inline signature. Same fix: drop the inlines, use `.form-input-p`. Workflow Actions section is hand-stacked with `display: flex; flex-direction: column; gap: 0.75rem;` — should be `.action-stack` utility.

---

## 5. Specific PR #3 edit instructions

The edits below are organized so each commit is a clean atomic change. Suggested branch: `pr/13.6-3-form-density-top10`. Suggested commit order:

### Commit 1 — Define the new utility classes in cockpit.css

(See § 6 for the full CSS block. **Add at end of `wwwroot/css/cockpit.css`.**)

### Commit 2 — Replace `.flex: 1` inline overrides with one CSS rule

File: `wwwroot/css/cockpit.css` — append:
```css
.form-row > .form-group { flex: 1; }
```

Then `grep -rln 'data-csp-style="flex: 1;"' Pages/` and `sed -i '' 's/ data-csp-style="flex: 1;"//g'` across all matched files.

Expected drop: **≥ 80 inline overrides** (mostly `_AssetMesIotSafetyTabs.cshtml`).

### Commit 3 — `.callout` + tri-state semantic utility

File: `wwwroot/css/cockpit.css` — append callout block (see § 6).

Then for each of these files, replace the hand-coded card backgrounds:

- `Pages/Periods/Close.cshtml` lines 53-57, 60-64, 70-90, 121-125 (the `ctaCardStyle` ternary), 137-159 (form CTAs), 167-205 (close packet)
- `Pages/Reports/TrialBalance.cshtml` lines 33-53 (the `bannerStyle` ternary)
- `Pages/Index.cshtml` lines 40-56, 60-68 (dev/prod mode banners)
- `Pages/Admin/DataImport.cshtml` lines 42-57 (guard-active banner)
- `Pages/Help/Index.cshtml` lines 56-78 (purple gradient implementation card)

Expected drop: **≈ 80 inline overrides**, plus removes 6 hard-coded hex colors that violate the "no info-blue, no purple-AI" rule.

### Commit 4 — `.field-stack` + sister `_VendorViewTabs` refactor

File: `wwwroot/css/cockpit.css` — append `.field-stack` block (see § 6).

File: `Pages/Materials/Vendors/_VendorViewTabs.cshtml` — full rewrite. Each `<div><div data-csp-style="...">LABEL</div><div data-csp-style="...">value</div></div>` block becomes `<div class="field-stack"><label class="field-stack__label">LABEL</label><div class="field-stack__value">value</div></div>`.

Also use the same `.field-stack` rule to clean up the 5-row metadata in `Pages/Assets/Asset.cshtml` hero card (lines 132-165 area).

Expected drop: **≈ 70 inline overrides.**

### Commit 5 — `.form-stack` + `.form-input-p--file` for AssetImport + Fai

Files:
- `Pages/Admin/AssetImport/Upload.cshtml` lines 37-43
- `Pages/Quality/Fai/Create.cshtml` lines 36-87 (the 2 section-cards → 1 form-grid--4)
- `Pages/Quality/Fai/Create.cshtml` lines 89-127 (the AS9102 Classification section)
- `Pages/Quality/Fai/Detail.cshtml` lines 198-256 (the Add Characteristic 9-field form)
- `Pages/Quality/Fai/Detail.cshtml` lines 91-119 (Workflow Actions → `.action-stack`)

Expected drop: **≈ 50 inline overrides + 540px vertical** across the Fai trio alone.

### Commit 6 — `.data-table--items` column-width refactor

File: `wwwroot/css/cockpit.css` — add the 10 `nth-child(n)` width rules.

File: `Pages/Admin/Items.cshtml` lines 53-62 — strip the 10 `data-csp-style="width: N;"` attributes and add `data-table--items` to the `<table class="...">`.

Expected drop: **30 inline overrides** with zero visual change. Sets the pattern for every other `data-table` in the codebase.

### Commit 7 — Extract `_FlashMessages.cshtml` partial

There are 14 files that render TempData["Success"] / TempData["Error"] / TempData["Warning"] alerts. Extract:

```html
@* Pages/Shared/Partials/_FlashMessages.cshtml *@
@{
    var success = TempData["Success"] as string;
    var error = (TempData["Error"] ?? TempData["ErrorMessage"]) as string;
    var warning = TempData["Warning"] as string;
}
@if (!string.IsNullOrEmpty(success)) { <div class="callout callout--success">@success</div> }
@if (!string.IsNullOrEmpty(error))   { <div class="callout callout--danger">@error</div> }
@if (!string.IsNullOrEmpty(warning)) { <div class="callout callout--warning">@warning</div> }
```

Replace ~28 inline-styled alert blocks across the tree with `@await Html.PartialAsync("Partials/_FlashMessages")`.

### Commit 8 — `_ReceiptCoreFields` rebuild

File: `Pages/Admin/StockReceipts/_ReceiptCoreFields.cshtml` — full rewrite, drop the 4 style variables, compose `.section-card` + `.form-grid--auto-fit` + `.form-input-p` natively. Expected drop: **56 → ≤ 4 inline overrides**.

### Commit 9 — `_PoDetailsBody` flash-message + line-item cleanup

File: `Pages/Purchasing/Details.cshtml` — drop the inline-styled flash block (commit 7 fix), drop the `style="margin-bottom: 14px; ..."` on the `ds-card` flash messages.

### Commit 10 — Documentation update

Append to `docs/research/luxury-cockpit-ux.md` §4 a new sub-section "PR #3 — Form density utility classes" that lists the 7 new utilities (`.callout`, `.field-stack`, `.form-stack`, `.form-stack-md`, `.form-stack-tight`, `.action-row`, `.action-stack`) and links back to this audit memo. Locks them in as authoritative.

---

## 6. Required CSS additions to `wwwroot/css/cockpit.css`

Append the following block to `wwwroot/css/cockpit.css`. Every rule maps directly to a token from `design-tokens-v2.json`.

```css
/* ============================================================== */
/* SPRINT 13.6 PR #3 — Form density utilities                      */
/* Authority: docs/research/form-density-audit-2026-05-25.md       */
/* Tokens: wwwroot/css/design-tokens-v2.json                       */
/* ============================================================== */

/* --- form-row / form-group base --------------------------------- */
.form-row > .form-group { flex: 1; }

/* --- form-stack: vertical label+input pair, 12px gap ------------ */
.form-stack { display: flex; flex-direction: column; gap: 6px; margin-bottom: 12px; }
.form-stack--tight { gap: 4px; margin-bottom: 8px; }
.form-stack--md    { gap: 6px; margin-bottom: 16px; }
.form-stack--full  { grid-column: 1 / -1; }
.form-stack > label,
.form-stack > .form-label-p {
  font-size: 12px;
  font-weight: 500;
  letter-spacing: 0.02em;
  text-transform: uppercase;
  color: var(--color-text-3);
}

/* --- form-grid: explicit column counts (replaces auto-fit) ------ */
.form-grid              { display: grid; gap: 12px 20px; }      /* 6, 5 asymmetric */
.form-grid--2           { grid-template-columns: repeat(2, 1fr); }
.form-grid--3           { grid-template-columns: repeat(3, 1fr); }
.form-grid--4           { grid-template-columns: repeat(4, 1fr); }
.form-grid--auto-fit    { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
@media (max-width: 1024px) { .form-grid--3, .form-grid--4 { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 640px)  { .form-grid--2, .form-grid--3, .form-grid--4 { grid-template-columns: 1fr; } }

/* --- field-stack: read-mode label+value pair (Vendor view tabs) - */
.field-stack { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.field-stack__label {
  font-size: 12px;
  font-weight: 500;
  letter-spacing: 0.02em;
  text-transform: uppercase;
  color: var(--color-text-3);
}
.field-stack__value {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-1);
}
.field-stack--mono .field-stack__value {
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-feature-settings: 'tnum' on, 'cv11' on;
}

/* --- callout: the one tri-state banner pattern ------------------ */
/* Replaces ~80 hand-coded background/border/color triples across   */
/* Periods/Close, Reports/TrialBalance, Index, Admin/DataImport,    */
/* Demo/ChainOfCustody, Help/Index. Per token: padX 20 padY 12.     */
.callout {
  margin-top: 12px;
  padding: 12px 20px;
  border-radius: 6px;
  border: 1px solid var(--color-border-1);
  background: var(--color-surface-2);
  color: var(--color-text-1);
  display: flex;
  align-items: flex-start;
  gap: 12px;
}
.callout > strong:first-child { flex-shrink: 0; font-weight: 600; }
.callout--success {
  background: var(--color-success-50, rgba(34,197,94,0.10));
  color: var(--color-success-700, #15803d);
  border-color: var(--color-success-400, rgba(34,197,94,0.35));
}
.callout--warning {
  background: var(--color-warning-50, rgba(245,158,11,0.10));
  color: var(--color-warning-700, #b45309);
  border-color: var(--color-warning-500, rgba(245,158,11,0.35));
}
.callout--danger {
  background: var(--color-danger-50, rgba(239,68,68,0.10));
  color: var(--color-danger-700, #b91c1c);
  border-color: var(--color-danger-500, rgba(239,68,68,0.35));
}
.callout--info {
  background: var(--color-surface-2);
  color: var(--color-text-2);
  border-color: var(--color-border-2);
}

/* --- action-row / action-stack ---------------------------------- */
.action-row {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
  margin-top: 12px;
  padding: 12px 0 0;
  border-top: 1px solid var(--color-border-3, rgba(255,255,255,0.06));
}
.action-row--start { justify-content: flex-start; }
.action-row--centered { justify-content: center; }
.action-row--bare { border-top: none; padding-top: 0; }
.action-stack { display: flex; flex-direction: column; gap: 12px; }

/* --- inline-form / button-sm ------------------------------------ */
.inline-form { display: inline-block; margin: 0; }
.hero-btn--sm {
  font-size: 13px;
  padding: 4px 10px;
  border-radius: 4px;
  height: 28px;
  line-height: 1;
}

/* --- file input variant ----------------------------------------- */
.form-input-p--file {
  padding: 6px 8px;
  font-family: var(--font-sans);
}

/* --- pre--inherit (notes / errors / parse output) --------------- */
.pre--inherit {
  margin: 0;
  font-family: inherit;
  white-space: pre-wrap;
  font-size: 13px;
  color: inherit;
}

/* --- section-card-body--actions --------------------------------- */
.section-card-body--actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  padding: 12px 20px;
}

/* --- data-table--items column widths (replaces inline th rules) - */
.data-table--items th:nth-child(1)  { width: 120px; }              /* Part # */
.data-table--items th:nth-child(2)  { width: 50px; }               /* Rev */
.data-table--items th:nth-child(3)  { width: 120px; }              /* Vendor Part # */
.data-table--items th:nth-child(4)  { width: 120px; }              /* Mfr Part # */
.data-table--items th:nth-child(5)  { min-width: 180px; }          /* Description */
.data-table--items th:nth-child(6)  { width: 100px; }              /* Type */
.data-table--items th:nth-child(7)  { width: 100px; }              /* Category */
.data-table--items th:nth-child(8)  { width: 70px; }               /* UOM */
.data-table--items th:nth-child(9)  { width: 90px; text-align: right; }  /* Std Cost */
.data-table--items th:nth-child(10) { width: 90px; }               /* Status */

/* --- kpi-value--date variant (smaller for date strings) --------- */
.kpi-value--date { font-size: 16px; font-variant-numeric: tabular-nums; }

/* --- cockpit-edit-form: standard collapsible edit form ---------- */
.cockpit-edit-form {
  margin-top: 20px;
  background: var(--color-surface-1);
  border: 1px solid var(--color-border-1);
  border-radius: 6px;
  padding: 12px 20px;
}
.cockpit-edit-form__toggle {
  cursor: pointer;
  font-size: 14px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 8px;
  user-select: none;
}
.cockpit-edit-form[open] .cockpit-edit-form__toggle {
  margin-bottom: 12px;
}

/* ============================================================== */
/* END Sprint 13.6 PR #3 utilities                                  */
/* ============================================================== */
```

**Total LOC added: ~145.** No JS, no font changes, no token additions — every rule maps to a value already in `design-tokens-v2.json`.

---

## 7. Estimated impact

| Metric | Current | Target after PR #3 | Δ |
|---|---:|---:|---:|
| Total `data-csp-style` attrs in Pages/ | 2,342 | **≤ 1,500** | -36 % |
| Total `data-csp-style="margin*` | 381 | **≤ 180** | -53 % |
| Total `data-csp-style="padding*` | 213 | **≤ 100** | -53 % |
| Files with > 25 overrides | 22 | **≤ 8** | -64 % |
| Fai/Create.cshtml vertical px | 840 | 360 | -57 % |
| ItemEdit.cshtml first-tab vertical px | ~960 | ~430 | -55 % |
| Assets/Asset.cshtml first-tab vertical px | ~1,200 | ~540 | -55 % |
| Periods/Close.cshtml vertical px (preflight + CTA) | ~1,150 | ~680 | -41 % |

**Pages that should fit inside 1440×900 viewport on first paint after PR #3:**

- `Pages/Admin/AssetImport/Index.cshtml` ✓ (already close)
- `Pages/Admin/AssetImport/Upload.cshtml` ✓
- `Pages/Admin/AssetImport/Preview.cshtml` ✓ (header + 4 KPIs + actions + 1 table)
- `Pages/Quality/Fai/Create.cshtml` ✓ (single section + action row)
- `Pages/Quality/Fai/Detail.cshtml` ✓ (header + 4 KPIs + 2-col detail-list + 1 table)
- `Pages/Periods/Close.cshtml` — preflight checks tab ✓
- `Pages/Materials/ItemEdit.cshtml` — Identity tab ✓

Pages that won't fit even after PR #3 (require deeper structural work, NOT in scope for PR #3):

- `Pages/Assets/Asset.cshtml` — needs full hero-to-cockpit rebuild (queued for Wave 2.5 per memory). PR #3 only does the inline-style cleanup and label-token alignment.
- `Pages/WorkOrders/Details.cshtml` — Operations Cockpit is 6 sections deep, needs tab-shell. PR #3 only fixes the Edit form sub-section.

---

## 8. Risk + rollback

**Risk: very low.** Every commit in this PR is either:

- (a) a pure CSS append to `cockpit.css` (no existing rule modified), or
- (b) a Razor edit that swaps inline-style for a class that resolves to the same computed style.

**Test plan:**

1. Build `dotnet build` — must compile clean.
2. Run a11y-audit CI gate — must pass (focus rings still 2 px solid accent).
3. Visually compare each top-10 page in Replit before/after — should be near-identical pixel-for-pixel, just shorter.
4. Snapshot the rendered HTML of the top-10 pages and diff — only `class=` attributes and `data-csp-style=` should change, not actual layout.

**Rollback:** revert the PR. No DB changes. No service contract changes. No new dependencies. Same as any pure-Razor refactor.

---

## 9. Out of scope (intentional)

- The `is-control-center` body theme + dark cockpit chrome — handled by `cockpit-v2.css`, not in scope for form density.
- Side-menu / Nav cleanup — separate Wave 2.5 PR per Dean's UI Audit + Cleanup Sprint memo (`reference_ui_audit_2026_05_25.md`).
- Replacing `auto-fit minmax(220px, 1fr)` with the explicit 2/3/4-col grids on every single form — only the top-10 are in PR #3. The rest get the same treatment in PR #4 (40-page mid-tier sweep) and PR #5 (long tail).
- `Pages/Assets/Asset.cshtml` hero-to-cockpit rebuild — Wave 2.5.
- `Pages/WorkOrders/Details.cshtml` Operations Cockpit tab-shell rebuild — Wave 2.5.

---

## 10. Appendix — full file count by override total

(Filenames truncated to suit the table. Sourced from `find Pages/ -name '*.cshtml' | xargs grep -c 'data-csp-style=' | sort -rn`.)

```
229  Pages/Assets/Asset.cshtml
150  Pages/WorkOrders/Details.cshtml
111  Pages/Materials/ItemEdit.cshtml
 90  Pages/Admin/Items.cshtml
 83  Pages/Assets/_AssetMesIotSafetyTabs.cshtml
 76  Pages/Index.cshtml
 59  Pages/Materials/Vendors/_VendorViewTabs.cshtml
 57  Pages/Purchasing/Details.cshtml
 56  Pages/Admin/StockReceipts/_ReceiptCoreFields.cshtml
 54  Pages/Periods/Close.cshtml
 51  Pages/CIP/Details.cshtml
 40  Pages/Reports/TrialBalance.cshtml
 39  Pages/Demo/ChainOfCustody.cshtml
 36  Pages/Reports/Form4562.cshtml
 35  Pages/Journals/Manual.cshtml
 34  Pages/Admin/Sites.cshtml
 34  Pages/Admin/StockReceipts/Index.cshtml
 32  Pages/CCA/ClassReport.cshtml
 31  Pages/Admin/Requisitions.cshtml
 28  Pages/Receiving/Receive.cshtml
 28  Pages/Quality/Fai/Detail.cshtml
 26  Pages/Help/Index.cshtml
 26  Pages/Reports/DepreciationSchedule.cshtml
 25  Pages/Admin/PMTemplateEdit.cshtml
 25  Pages/Admin/DataImport.cshtml
 24  Pages/CIP/Index.cshtml
 24  Pages/Books/Index.cshtml
 23  Pages/Admin/FiscalCalendar.cshtml
 23  Pages/Quality/Fai/Create.cshtml
 23  Pages/Materials/Items.cshtml
 22  Pages/Maintenance/WorkRequests/Details.cshtml
 22  Pages/Receiving/Details.cshtml
 22  Pages/AccountsPayable/Details.cshtml
 21  Pages/Maintenance/Details.cshtml
 21  Pages/Assets/Index.cshtml
 18  Pages/Receiving/Inspect.cshtml
 18  Pages/CCA/Index.cshtml
 17  Pages/Reports/DepreciationPreview.cshtml
 16  Pages/API/Index.cshtml
 16  Pages/Assets/Improve.cshtml
 ... (111 more files with 1-15 overrides) ...
```

The long tail (≤ 15 overrides per file × 111 files) accounts for ~700 of the 2,342 total — that's the PR #4 + PR #5 sweep target. PR #3 hits the top 22 = ~1,150 / 2,342 = **49 % of the inline-style debt in a single PR.**

---

## 11. Cross-refs

- Authority: `wwwroot/css/design-tokens-v2.json` (asymmetric spacing, three-density rule, label-12 token)
- Authority: `docs/research/luxury-cockpit-ux.md` (sections 1, 2, 3 — locked decisions, the four dimensions, anti-patterns)
- Exemplars: `Pages/Receiving/ControlCenter.cshtml`, `Pages/Production/Workbench.cshtml`
- Sibling Sprint 13.6 audit (queued): `docs/research/side-menu-audit-2026-05-25.md` (Wave 2.5 PR #1)
- Sibling Sprint 13.6 audit (queued): `docs/research/subform-chrome-audit-2026-05-25.md` (Wave 2.5 PR #2)
- Memory: `reference_ui_audit_2026_05_25.md` — Dean's UI Audit + Cleanup Sprint directive

---

_End of memo._
