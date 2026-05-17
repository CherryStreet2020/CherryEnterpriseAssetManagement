# CherryAI EAM — Design System

**Status**: Living document. Locked by PR #116d.1a (2026-05-17).
**Showcase**: `/Admin/DesignSystem` (admin-only).
**Direction**: Ultra-premium luxury. Reference cluster: Linear × Vercel × Arc Browser × Notion Calendar. Not Bloomberg, not generic B2B CRUD.

---

## The rules

1. **No hard-coded colors** outside `wwwroot/css/tokens.css`. If you need a new color, add it to tokens first.
2. **No off-scale spacing.** Use the `--space-*` and `--density-*` tokens. Don't write `padding: 17px`.
3. **No `font-weight: 600` or `700` in body copy.** Reserve them for display headlines and emphasis. Body is 400.
4. **Inter Tight for display, Inter for UI, JetBrains Mono for IDs and numerics.** Never mono in chrome (no Bloomberg fonts).
5. **Two color decisions per surface, max.** Brand red + one semantic tone. Avoid rainbow categorical encoding.
6. **Every numeric in a tile or KPI uses `font-feature-settings: "tnum" 1`.** Tabular numerals prevent shifting.
7. **Hover-lift, not hover-shadow.** Cards translate up; shadows deepen. Always `translateY` + `box-shadow`, never just one.
8. **Glass-blur is reserved for floating elements.** Badges, palette, drawers — not surface cards.
9. **Critical band gets a glow.** `--glow-critical` ring on hover for any Critical-tone card.
10. **Reduced-motion respected.** All animations have `@media (prefers-reduced-motion: reduce)` fallbacks.

---

## Primitives (10 shipped — #116d.1a + #116d.1b)

### `<DataCard>` · `Primitives/_DataCard.cshtml`

Universal bounded surface. Renders an optional eyebrow row, title, subtitle, AI-storyline narrative, and footer. Has an image-left variant (4:5 aspect imagery + content right) with floating glass-blur badges over the photo.

**When to use**: any time you'd reach for a "card" — asset summary, work-order row in card mode, dashboard panel, KPI rollup wrapper.

**Tones**: `neutral`, `brand`, `info`, `success`, `warning`, `critical`. The tone applies a soft ring on hover; Critical gets a brand-glow.

**Props (`DataCardModel`)**:
- `Eyebrow`, `Title`, `Subtitle` — text slots
- `Tone` — drives accent color
- `Interactive` + `Href` — turns the card into a link with hover-lift
- `ImageUrl` *or* `ImageBrandClass` — enables image-left variant
- `FloatingBadges` — list of glass-blur chips over the image
- `Storyline` — pinned narrative inside the card
- `Footer` — bottom strip

### `<KPITile>` · `Primitives/_KPITile.cshtml`

Number + label + delta + sparkline. The signature dashboard primitive.

**When to use**: any top-of-page summary row. Always show in groups of 3 or 4 — solo KPIs feel orphaned.

**Props (`KpiTileModel`)**:
- `Label` — caps text above the number
- `Value` + `Unit` — the big number and its unit ("94", "%")
- `DeltaText` + `DeltaDirection` — period-over-period comparison
- `SparkPoints` — typically 7-day series. The component normalizes.
- `SparkTone` — color the trend by meaning, not by direction
- `Href` — optional drill-through

**Motion**: Numbers auto-count-up from 0 → value over 1.2s the first time the tile enters the viewport. Skipped for `prefers-reduced-motion`.

### `<StatusPill>` · `Primitives/_StatusPill.cshtml`

Semantic tone pill. Compact, used inline.

**When to use**: a single piece of status — "Active", "Critical", "Overdue", "12 pending". Not for filtering — that's a different primitive (Phase #116d.1b will add `<FilterPill>`).

**Tones**: `neutral`, `active`, `success`, `warning`, `danger`, `critical`, `info`, `brand`, `muted`.

### `<Sparkline>` · `Primitives/_Sparkline.cshtml`

Inline SVG trend chart. Used standalone in cards or behind tile values. Auto-normalizes its data points.

**Props**:
- `Points` — array of numbers (left-to-right time series)
- `Tone` — stroke + fill color
- `ShowArea` — soft gradient fill under the line
- `Thin` — 1.0 stroke instead of 1.4 for dense placements

### `<DataTable>` · `Primitives/_DataTable.cshtml`

Sortable, sticky-header, density-aware data table. Sort handled client-side by `primitives.js`.

**When to use**: any tabular list — work orders, assets, journal lines. Up to ~500 rows; beyond that, pair with server-side pagination.

**Props (`DataTableModel`)**:
- `Eyebrow`, `Title`, `CountText` — header chrome
- `Columns` — list of `DataTableColumn` (`Label`, `Align`, `Width`, `Sortable`, `Mono`)
- `RowsHtml` — pre-rendered `<td>` HTML per row (gives you control over inline pills, links, icons)
- `StickyHeader` — defaults to true
- `EmptyMessage` — shown when `RowsHtml` is empty

### `<EmptyState>` v2 · `Primitives/_EmptyStateV2.cshtml`

Hero illustration slot + headline + body + CTA. Old `_EmptyState.cshtml` kept for backwards compat.

**When to use**: any list, table, or section with zero results. Always pair with a CTA that resolves the empty state (create something, reset filters, view help).

**Props (`EmptyStateModel`)**: `Title`, `Body`, `CtaLabel`/`CtaHref`, `SecondaryLabel`/`SecondaryHref`, `Icon` (one of: search, inbox, folder, document, calendar, wrench, sparkle, chart, lock), `Tone` (drives glow color on the icon halo).

### `<SkeletonLoader>` · `Primitives/_SkeletonLoader.cshtml`

Animated placeholder shapes — line / card / table / kpi. Pulses a soft sheen.

**When to use**: while data is loading. Never spinners — skeletons feel faster.

**Props (`SkeletonLoaderModel`)**: `Shape`, `LineCount`, `RowCount`, `TileCount`.

### `<ContextDrawer>` · `Primitives/_ContextDrawer.cshtml`

Slide-in right drawer for "detail without leaving the list."

**When to use**: clicking a row should open detail in-context, not navigate away. Use it for asset detail from Plant Floor, WO detail from a list, etc.

**Open**: `CherryDS.drawer.open(id)` or `<button data-drawer-open="id">`.
**Close**: ESC, backdrop click, `<button data-drawer-close="id">`, or `CherryDS.drawer.close(id)`.

**Props (`ContextDrawerModel`)**: `Id`, `Title`, `Subtitle`, `BodyHtml`, `FooterHtml`, `Width` (default 480px).

### `<ButtonGroup>` · `Primitives/_ButtonGroup.cshtml`

A row of related buttons. Use for form actions, table toolbars, drawer footers.

**Variants**: `primary` (brand gradient + glow), `secondary` (subtle bg + border), `ghost` (transparent until hover), `danger` (red).
**Sizes**: `sm` (28px), `md` (36px), `lg` (42px).

**Props (`ButtonGroupModel`)**: `Buttons` (list of `ButtonModel`), `Align` (start/center/end).

### `<BrandChip>` · `Primitives/_BrandChip.cshtml`

Per-OEM color-accent chip. Pass a manufacturer name; the chip auto-tints with that brand's signature color.

**When to use**: anywhere we surface manufacturer + model. Plant Floor cards, asset register rows, drawer headers.

**Recognized manufacturers** (with brand-tinted dot + soft background): Haas (red), Mazak (gold), Lincoln (red), KUKA (orange), FANUC (yellow), Trumpf (blue), DMG MORI (green), Schuler (navy), ABB (red), Yaskawa/Motoman (blue), Atlas Copco (amber), Fronius (red), Siemens (teal), Doosan (navy). Unknown → neutral.

---

## Color tokens — semantic, not decorative

| Token | Light | Dark | Use |
|---|---|---|---|
| `--color-accent` | `#cf3339` | same | Brand red. Primary CTAs, brand chip, active nav. |
| `--success` | `#10b981` | same | Healthy state, positive delta, available stock. |
| `--warning` | `#f59e0b` | same | Warning band, low stock, late. |
| `--danger` | `#ef4444` | same | Critical band, blocked, error. |
| `--info` | `#3b82f6` | same | Informational pill, neutral metric. |
| `--surface-primary` | `#ffffff` | `#111827` | Card background. |
| `--surface-secondary` | `#f1f5f9` | `#1e293b` | Page background tier. |
| `--text-primary` | `#0f172a` | `#f1f5f9` | Headlines, body. |
| `--text-secondary` | `#475569` | `#94a3b8` | Subtitle, supporting. |
| `--text-muted` | `#94a3b8` | `#64748b` | Captions, eyebrow caps. |

**Pairing rule**: text on a colored fill uses a darker stop from the same ramp (e.g. `--color-success-700`), never plain black or generic gray.

---

## Density

`html.density-compact`, default (Comfortable), `html.density-spacious`. Set via `CherryDS.density.set(mode)` or the toggle on `/Admin/DesignSystem`. Persists to `localStorage` as `cherryai.density`.

Affects: card padding, gap between cards, page padding, table row height, form input height. Does NOT affect typography — type stays consistent across modes.

---

## Motion grammar

- `--dur-quick` 160ms — hover state changes, color transitions
- `--dur-base` 240ms — card lifts, button presses, primary transitions
- `--dur-relaxed` 360ms — drawer slide-in, modal entry
- `--dur-cinematic` 520ms — page transitions, hero entries
- `--dur-count-up` 1200ms — KPI numeric count-up

Use `--ease-out-expo` for entry, `--ease-out-quart` for state changes, `--ease-emphasized` for cinematic moments. Never `ease`, never `linear`, never default. Apple-tier curves only.

---

## Files

- `wwwroot/css/tokens.css` — single source of truth for color, spacing, type, motion
- `wwwroot/css/primitives.css` — base styles for all primitives
- `wwwroot/js/primitives.js` — density toggle, count-up, noise canvas
- `Pages/Shared/Primitives/PrimitiveModels.cs` — typed view-models
- `Pages/Shared/Primitives/_*.cshtml` — Razor partials
- `Pages/Admin/DesignSystem.cshtml` — internal showcase

---

## What ships next

- **PR #116d.1b** — `<DataTable>` (sortable, sticky header, density-aware), `<EmptyState>` v2 (with hero illustration slot), `<SkeletonLoader>` (line/card/table/kpi shapes), `<ContextDrawer>` (slide-in right), `<ButtonGroup>` (primary/secondary/ghost/danger), `<BrandChip>` (OEM color accents), Cmd-K palette hardening with top-20 routes registered.
- **PR #116d.2** — Dashboard refactored as the reference exemplar, every section built only from primitives.
- **PR #116d.3-22** — Page-by-page rollout in demo-arc order.
- **PR #116d.23** — axe-core CI + WCAG 2.1 AA verification + final cross-page consistency audit.

This document updates as primitives ship. Don't write new primitive-style CSS outside this system — extend the tokens or add a new primitive instead.
