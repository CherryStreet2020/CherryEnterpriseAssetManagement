# Subform Redundancy Audit — 2026-05-25

**Audit lead:** UI Cleanup Sprint Wave 2.5 (Priority 1.62a)
**Authority:** `wwwroot/css/design-tokens-v2.json` + `docs/research/luxury-cockpit-ux.md`
**Feeds:** Sprint 13.6 PR #4 — Subform consolidation
**Trigger:** Dean Dunagan, 2026-05-25 — *"a lot of these forms and subforms are taking up MASSIVE amounts of real estate."*

---

## Executive summary

**Scope audited:** 159 top-level Razor pages + 282 partials under `Pages/`. Deep-read 14 pages (today's two ships + the highest-traffic Details surfaces + every page that combines `_ScreenHeader` with `page-header` patterns). Sampled the remaining ~145 via grep.

**Result:** **12 pages** carry an entirely parallel page-chrome stack (`page-header` + `breadcrumb-trail` + `page-title` + `page-subtitle` + manual Back button) instead of using `_ScreenHeader` — these are the worst offenders, all written before ADR-008 landed. **31+ pages** ship with `_ScreenHeader` AND then immediately re-render the same status/identity/metric data in 4-tile KPI grids, "Receipt Information" sections, or pe-card field strips. **6 pages** include a section-card whose only job is to wrap a CTA button or sentence (a "section that isn't a section"). **Today's PR #337 Preview.cshtml** triple-renders status. **Today's PR #338 Detail.cshtml** has the inline add-characteristic form with no contextual nesting under Form 3. **CustomerProjects/Details.cshtml** has 5 nearly-identical `<section class="detail-card detail-card--full">` children that beg for one shared partial.

**Top 5 redundancy patterns (by occurrence count):**

| # | Pattern | Pages affected | Severity |
|---|---------|----------------|----------|
| 1 | Repeated child-table section header (`<h3>Title <span class="muted">(@count)</span></h3>` + table) | 6 in Details.cshtml alone, 30+ across surfaces | High — biggest visual mass |
| 2 | KPI tile grid duplicates Subtitle/StatusText shown in `_ScreenHeader` above | 8 confirmed (Preview, Detail, Fai/Detail, AssetImport/Index, AssetImport/Detail, …) | High — vertical real estate waste |
| 3 | Parallel page-chrome stack (page-header + breadcrumb-trail) instead of `_ScreenHeader` | 12 | Critical — divergent UI per page |
| 4 | "Section card" wrapping ONE CTA button (`Start a new FAI`, `Start a new import`) | 6 | Medium — section-card chrome for ~3 lines of body |
| 5 | First detail-card on Details pages restates H1 identity (Receipt Number, Invoice Number, etc.) | 4 (Receiving, AccountsPayable, Purchasing, CIP) | Medium — H1 already says it |

**TL;DR** — The chrome stack is healthy; teams are just not trusting it. Every Details page renders the same primary identifier three times: `<title>`, `_ScreenHeader.HeaderTitle`, and then a "X Information" / "Header" card that restates Code + Description as the first row. Every Index page that has a KPI partial wired into `_ScreenHeader.KpisPartial` ALSO renders kpi-card grids below the header. Per Dean's lock — extract, consolidate, demand discipline.

---

## DUPLICATED CHROME table

Columns:
- **File** — host-relative path
- **Pattern code:**
  - `BREADCRUMB-X2` — manual `<nav class="breadcrumb-trail">` OR Context partial with `<nav class="breadcrumb">` AND `_ScreenHeader.Breadcrumbs`
  - `BACK-X2` — manual `<a class="btn-secondary">← Back</a>` AND `_ScreenHeader.ShowBackLink=true`
  - `H1-X2` — page renders `<h1 class="page-title">` AND uses `_ScreenHeader` (or `_ScreenHeader` HeaderTitle + first section restates identity)
  - `KPI-ECHO` — KPI tile grid restates Subtitle text or Status that `_ScreenHeader` already shows
  - `INNER-TITLE-ECHO` — child section card title repeats an ancestor's H1/H3 word-for-word
  - `FORM-NO-CONTEXT` — inline form with no parent section explaining what it's adding to
  - `SECTION-FOR-CTA` — full section-card chrome wrapping only a CTA button or 1-sentence body
  - `PARALLEL-STACK` — page uses `page-header` block instead of `_ScreenHeader` entirely (divergent IA)

| File | Pattern | Evidence |
|------|---------|----------|
| `Pages/Admin/AssetImport/Preview.cshtml` | KPI-ECHO + SECTION-FOR-CTA | L30 Subtitle says *"Parsed N rows · N valid · N with errors"*; L62-77 same 3 numbers in kpi-card grid. L80-108 "Actions" section-card wraps 3 buttons that belong in `_ScreenHeader.ActionsPartial`. |
| `Pages/Admin/AssetImport/Detail.cshtml` | KPI-ECHO | L20 Subtitle *"… N rows · N valid · N errors"*; L34-50 kpi-card grid restates RowCount, plus CreatedAt + CommittedAt that belong in Subtitle or Context. |
| `Pages/Admin/AssetImport/Index.cshtml` | SECTION-FOR-CTA | L50-74 entire "Start a new import" section card is one paragraph + 2 buttons. CTAs belong in `_ScreenHeader.ActionsPartial`. |
| `Pages/Admin/AssetImport/Upload.cshtml` | KPI-ECHO (mild) + SECTION-FOR-CTA | L30-62 single section-card wraps the whole form — but the header *itself* duplicates the page title "Upload Excel" (L32 vs L11 HeaderTitle). |
| `Pages/Quality/Fai/Detail.cshtml` | KPI-ECHO + INNER-TITLE-ECHO + FORM-NO-CONTEXT | L23 Subtitle *"@f.PartNumberSnapshot · @f.Type · @f.PartType"* + L46-69 KPI grid (Characteristics / Non-Conform / Waived / AI Risk = redundant with header status). L72-90 *Form 1 — Header* repeats the PartNumber already in Subtitle. L195-256 "Add a characteristic" lives in a sibling section-card with NO visual nesting under Form 3 (L122) — should be the empty-state / footer of Form 3, not a parallel block. |
| `Pages/Quality/Fai/Create.cshtml` | INNER-TITLE-ECHO | Header title is "New First Article Inspection"; first inner card title is "Identity" then "Snapshot fields (audit-legal)" — fine grouping but the form should be ONE card, not three. |
| `Pages/Quality/Fai/Index.cshtml` | SECTION-FOR-CTA | L34-55 "Start a new FAI" section card wraps a paragraph + 1 CTA. CTA belongs in `_ScreenHeader.ActionsPartial`; copy belongs in EmptyState when list is empty. |
| `Pages/CustomerProjects/Details.cshtml` | PARALLEL-STACK + INNER-TITLE-ECHO + 6× repeated child-table chrome | L10-30 hand-rolled `page-header` + `breadcrumb-trail` + `page-title` + Back button — entire stack should be `_ScreenHeader`. L96-250 six `<section class="detail-card detail-card--full">` blocks each with `<h3>Title (@count)</h3>` + table — identical shape for Jobs / Phases / Members / Amendments / FAI / Audit. **Today's FAI section (L205-250) was added in PR #338 in this exact pattern; it duplicates the chrome 5 ways.** |
| `Pages/CustomerProjects/Index.cshtml` | PARALLEL-STACK | L9-24 hand-rolled `page-header` instead of `_ScreenHeader`. |
| `Pages/CustomerProjects/Create.cshtml` | PARALLEL-STACK + BACK-X2 (visual) | L8-25 hand-rolled `page-header` + Cancel button rendered as "← Back" arrow + duplicate Cancel at L165 in form-actions. |
| `Pages/Production/Details.cshtml` | PARALLEL-STACK + INNER-TITLE-ECHO | L10-30 hand-rolled `page-header`. L42-54 "Order Header" detail-card whose first three rows (Type / Status / Description) are already in the H1 + status-chip + subtitle. |
| `Pages/Production/Index.cshtml` | PARALLEL-STACK | L16-31 hand-rolled `page-header`. |
| `Pages/Production/Create.cshtml` | PARALLEL-STACK | hand-rolled `page-header` (same pattern as the rest of Production/ and CustomerProjects/). |
| `Pages/Production/Operations/Index.cshtml` | PARALLEL-STACK | same. |
| `Pages/Admin/WorkCenters/Index.cshtml` | PARALLEL-STACK + KPI-ECHO | L8-28 hand-rolled `page-header` with subtitle *"N centers · N active · N in maintenance · BIC differentiator…"* — italicized marketing copy inside subtitle is mass. |
| `Pages/Admin/Routings/Index.cshtml` | PARALLEL-STACK | same pattern. |
| `Pages/Admin/WorkCalendars/Index.cshtml` | PARALLEL-STACK | same pattern. |
| `Pages/Admin/Carriers/Index.cshtml` | PARALLEL-STACK | same pattern. |
| `Pages/Admin/Countries/Index.cshtml` | PARALLEL-STACK | same pattern. |
| `Pages/Receiving/Details.cshtml` | INNER-TITLE-ECHO + BACK-X2 (Context) | L45 "Receipt Information" card; L50-51 first row literally restates HeaderTitle ("Receipt Number" = HeaderTitle gr.ReceiptNumber). |
| `Pages/AccountsPayable/Details.cshtml` | INNER-TITLE-ECHO + BACK-X3 | L27 EmptyState CtaLabel "Back to AP List", L29 SecondaryLabel "Back to Dashboard", L202 nested SecondaryLabel "Back to AP" — three differently-labeled back paths to the same place. |
| `Pages/Purchasing/Details.cshtml` | INNER-TITLE-ECHO | "Back to Purchasing" duplicated inside an EmptyState while ShowBackLink also active. |
| `Pages/CIP/Details.cshtml` | INNER-TITLE-ECHO + FORM-NO-CONTEXT | L36-95 "Edit Project" inline form hidden in pe-card; L653-695 "Capitalize CIP Project" inline form hidden in another pe-card — both are toggled by JS rather than living next to the data they edit. |
| `Pages/Maintenance/WorkRequests/_WorkRequestDetailsContext.cshtml` | BREADCRUMB-X2 | This Context partial renders `<nav class="breadcrumb">` and is plumbed into `_ScreenHeader.ContextPartial`. When the Details page ALSO sets `_ScreenHeader.Breadcrumbs`, the breadcrumb renders **twice** in the same header. Even if Breadcrumbs is left null, the partial is using the Context slot to render a duplicate of what the Breadcrumb-strip is designed for. |
| `Pages/Admin/_AdminDataImportContext.cshtml` | BREADCRUMB-X2 | same pattern — Context partial contains nav.breadcrumb. |
| `Pages/_IndexContext.cshtml` | BREADCRUMB-X2 (latent) | empty file in audit but flagged because it sits in the same Context-slot pattern. |
| `Pages/Assets/Asset.cshtml` | BACK-X2 (orchestrated) | L70 manually renders `_BackLink` partial *outside* `_ScreenHeader` because the view-mode header is nested inside an asset-hero wrapper. Works, but it's a workaround that proves the chrome doesn't compose cleanly inside other shells. |
| `Pages/Shared/_AssetMaintenanceHeader.cshtml` | duplicate of `_ScreenHeader` for one domain | One-off header partial for asset-maintenance that overlaps `_ScreenHeader`. Should be deleted in favor of `_ScreenHeader` + a KpisPartial. |

---

## Repeated child-table chrome — the biggest single redundancy

The pattern below appears **roughly 30 times across the codebase** (Details pages for CustomerProjects, Production, Books, CIP, Maintenance, WorkOrders, Purchasing, Receiving, AccountsPayable, Journals, Periods, etc.):

```razor
<section class="detail-card detail-card--full">
    <h3 class="detail-card-title">SECTION NAME <span class="muted">(@Model.X.Count)</span></h3>
    @if (Model.X.Count == 0)
    {
        <p class="muted">EMPTY-STATE TEXT.</p>
    }
    else
    {
        <table class="data-table">
            <thead>…</thead>
            <tbody>@foreach (…) { … }</tbody>
        </table>
    }
</section>
```

In `Pages/CustomerProjects/Details.cshtml` alone this exact shape is repeated **5 times** (Jobs L96, Phases L126, Members L151, Amendments L175, FAI L205) plus a 6th near-identical Audit card at L252. PR #338 added the 5th (FAI) by literal copy-paste with a *slightly different header* (it has a flex-row with View-all + New-FAI buttons inside), proving the divergence is already starting.

This is the **#1 extraction candidate**. See *Consolidation candidates* below.

---

## Today's ships — direct review

### PR #337 — `Pages/Admin/AssetImport/Preview.cshtml`

**Verdict:** triple-renders status + lossy use of header actions slot.

- L29-32 Subtitle = `"Parsed {RowCount} rows · {ValidRowCount} valid · {ErrorRowCount} with errors"` + L31-32 StatusText = `batch.Status.ToString()`.
- L61-78 grid renders the SAME 3 numbers PLUS status as 4 kpi-card tiles. The KPIs add no information — Subtitle already carries them in the header band.
- L80-108 "Actions" section-card wraps Commit / Re-validate / Discard. **These are the page actions** — they belong in `_ScreenHeader.ActionsPartial`, not in a section-card. The section-card chrome (header + body + h3 "Actions") consumes ~70px of vertical real estate to wrap 3 buttons.
- L110-124 "Rows" section-card-header has a flex-row with sub-tabs (All / Valid / Errors). This is a legit secondary-nav use case — *keep* but move the tab UI into `_TabNav` partial for visual consistency.

**Compared to `Detail.cshtml` (same surface, different state):** Preview adds the Actions card + tab strip; Detail adds different KPIs (Created at / Committed at) that *also* duplicate header subtitle. Both should converge on the same Subform shape with Status-driven action visibility.

### PR #338 — `Pages/Quality/Fai/Detail.cshtml`

**Verdict:** four redundancies — fix as part of PR #4.

1. **KPI-ECHO** (L46-69) — 4 kpi-cards: Characteristics / Non-Conform / Waived / AI Risk. Header Subtitle (L23) is `"{PartNumber} · {Type} · {PartType}"`. CharacteristicCount appears AGAIN at L125 as a status-badge "(@Model.Characteristics.Count of @f.CharacteristicCount)". Three places for one number.
2. **INNER-TITLE-ECHO** (L72-90) — *Form 1 — Header* card's first 4 rows (FAI Number / Revision / Type · Part Type · Reason / Part Number) restate HeaderTitle + Subtitle verbatim.
3. **FORM-NO-CONTEXT** (L195-256) — "Add a characteristic" form is a sibling section-card to "Form 3 — Characteristic Accountability". The form *only exists to extend Form 3* but visually it's a peer block with its own h3 / status-badge / body. Should be inside the Form 3 card as an inline footer-row, or behind a "+ Add" disclosure inside Form 3's header.
4. **Workflow Actions** card (L92-119) is a 2-column grid sibling to Form 1 Header. With only 1-3 buttons + a hint line it earns no card chrome — should be in `_ScreenHeader.ActionsPartial`.

### PR #338 — `Pages/CustomerProjects/Details.cshtml` FAI section

**Verdict:** the new FAI section literally copies the same shape as the 4 sections above it, validating the *Consolidation candidate* below. The FAI block (L205-250) also adds a flex-row "View all + New FAI" pair in the header — Jobs/Phases/Members/Amendments don't have any per-section actions, so the FAI block is the *only* one with an action row, which makes it visually inconsistent without a contract. Either add per-section actions to all 5 or extract the partial with an `ActionsHtml` slot.

This entire page should be rewritten in PR #4 against `_ScreenHeader` + a new `_ProjectChildSection` partial. The current 250-line page would shrink to ~90 lines.

---

## CONSOLIDATION CANDIDATES — new shared partials to create in PR #4

### 1. `Pages/Shared/Primitives/_ChildSection.cshtml` (highest leverage)

**Why:** kills the 30+ copies of the `<section class="detail-card--full"><h3>Title (count)</h3>[table|empty]</section>` shape.

**Proposed API (TagHelper-style ViewData):**

```razor
@{
    var sectionVd = new ViewDataDictionary(ViewData) {
        ["Title"] = "Jobs",
        ["Count"] = Model.Jobs.Count,
        ["EmptyText"] = "No jobs linked to this project yet.",
        ["ActionsPartial"] = "/Pages/CustomerProjects/_JobsSectionActions.cshtml",  // optional
        ["ActionsViewData"] = … // optional
    };
}
@await Html.PartialAsync("Primitives/_ChildSection", sectionVd)
{
    <table class="data-table">…</table>
}
```

Actually since RenderBody isn't trivial in MVC partials, simpler API:
- partial wraps the H3 + Count badge + optional Actions row + optional Empty fallback
- caller renders the table content separately and only invokes the partial for the title strip

**Or even simpler — `Pages/Shared/Primitives/_ChildSectionHeader.cshtml`** (no table, just header):

```razor
@{
    var t = ViewData["Title"]?.ToString();
    var c = (int?)ViewData["Count"];
    var actionsPartial = ViewData["ActionsPartial"]?.ToString();
}
<header class="child-section__header">
    <h3 class="child-section__title">@t @if (c.HasValue) { <span class="muted">(@c)</span> }</h3>
    @if (!string.IsNullOrEmpty(actionsPartial)) {
        <div class="child-section__actions">
            @await Html.PartialAsync(actionsPartial)
        </div>
    }
</header>
```

Page rewrites itself as `<section class="child-section"> @await … @* then table or empty-state *@ </section>` — drops 5 of the 9 lines per section.

### 2. `Pages/Shared/Primitives/_InlineFormDisclosure.cshtml`

**Why:** CIP/Details.cshtml hides "Edit Project" and "Capitalize" forms via `display: none` toggled by `toggleInlineForm()` JS. Same pattern in FAI/Detail.cshtml for "Add a characteristic". Same pattern in Maintenance/Details.cshtml for material issue/return.

Extract into a partial that gives:
- Trigger button in a parent card's header
- Disclosure region (form body) below the table or as an inline row
- `<dialog>` element if we want a modal upgrade later

This kills the `<div id="capitalizeInline" style="display: none">` anti-pattern and gives the dialog/disclosure semantic correctness.

### 3. Delete `Pages/Maintenance/WorkRequests/_WorkRequestDetailsContext.cshtml`

It only renders `<nav class="breadcrumb">` — that's what `_ScreenHeader.Breadcrumbs` does. Convert the Details page to set `Breadcrumbs` directly and drop the Context partial. Apply the same surgery to:
- `Pages/Admin/_AdminDataImportContext.cshtml`
- any other `*Context.cshtml` whose body is solely a breadcrumb nav

### 4. Delete `Pages/Shared/_AssetMaintenanceHeader.cshtml`

It's a one-off header for a single domain that duplicates `_ScreenHeader` semantics with bespoke markup. Consolidate by passing a KpisPartial + ContextPartial to `_ScreenHeader` instead.

### 5. Promote `_ScreenHeader.ActionsPartial` usage on all Index pages

Today, Index pages with single CTAs (FAI/Index "Start a new FAI", AssetImport/Index "Start a new import", many Admin lookups) render a full section-card to wrap the CTA. Extract a tiny `_PrimaryCtaActions.cshtml` partial that takes a label + href and renders one `.hero-btn-primary`. All 6 SECTION-FOR-CTA offenders shrink by ~30 lines each.

### 6. Convert all 12 `PARALLEL-STACK` pages to `_ScreenHeader`

CustomerProjects/Index, CustomerProjects/Create, CustomerProjects/Details, Production/Index, Production/Details, Production/Create, Production/Operations/Index, Admin/WorkCenters/Index, Admin/Routings/Index, Admin/WorkCalendars/Index, Admin/Carriers/Index, Admin/Countries/Index.

These 12 pages were written before `_ScreenHeader`/ADR-008 was adopted or by an agent that didn't reference the existing partial. Converting drops ~25 lines per page and produces visual parity with the rest of the app.

---

## Specific edit instructions for PR #4

### File-by-file removals (precise line numbers, repo HEAD as of 2026-05-25)

#### `Pages/Quality/Fai/Detail.cshtml`

- **Delete L46-69** (the 4-tile KPI grid). Move the same data into a `_FaiDetailKpis.cshtml` partial and wire as `KpisPartial` in `headerVd` (L20-30). The header band will then show those 4 numbers in the compact KpiBand format instead of full-size kpi-cards.
- **Delete L72-90** *Form 1 — Header* card. Move PartNumber/PartName/Drawing/Customer/CustomerProject/Item/Created/Submitted/Approved fields into a `_FaiDetailContext.cshtml` partial wired as `ContextPartial` — the Form 1 data is *header-band data*, not body content.
- **Delete L92-119** *Workflow Actions* card. Move buttons into a `_FaiDetailActions.cshtml` partial wired as `ActionsPartial`.
- **Restructure L122-256:** make "Form 3 — Characteristic Accountability" the only body section. Move the Add-characteristic form (L195-256) into Form 3's `section-card-body` as a disclosure: button in section-card-header → form revealed inline below the table.

**Expected size reduction:** ~258 lines → ~140 lines.

#### `Pages/Admin/AssetImport/Preview.cshtml`

- **Delete L61-78** (the 4-tile KPI grid). Move to a `_AssetImportPreviewKpis.cshtml` partial as `KpisPartial`.
- **Delete L80-108** (the "Actions" section-card). Move the 3 buttons into a `_AssetImportPreviewActions.cshtml` partial as `ActionsPartial`.
- **Keep L110-124** Rows section but convert the sub-tabs (All/Valid/Errors) into `_TabNav` partial for visual consistency.

**Expected size reduction:** ~211 lines → ~115 lines.

#### `Pages/Admin/AssetImport/Detail.cshtml`

- **Delete L34-51** (the 4-tile KPI grid). Move to `_AssetImportDetailKpis.cshtml` as `KpisPartial`. CreatedAt + CommittedAt go into the Subtitle or Context.

**Expected size reduction:** ~128 lines → ~75 lines.

#### `Pages/Admin/AssetImport/Index.cshtml`

- **Delete L50-74** (the "Start a new import" section-card). Move the 2 buttons (Download template + Upload Excel) into a `_AssetImportIndexActions.cshtml` partial as `ActionsPartial`. The 1-sentence body becomes an `EmptyStateV2` body when `Model.RecentBatches.Count == 0`.

**Expected size reduction:** ~151 lines → ~95 lines.

#### `Pages/Admin/AssetImport/Upload.cshtml`

- **Delete L32 inner h3 "Upload Excel"** — header already says it. Keep the section-card body but lose the redundant header.
- Even better: drop the section-card wrapper entirely; render the form directly under `_ScreenHeader`.

**Expected size reduction:** ~63 lines → ~40 lines.

#### `Pages/Quality/Fai/Index.cshtml`

- **Delete L34-55** (the "Start a new FAI" section-card). Move the CTA into a `_FaiIndexActions.cshtml` partial as `ActionsPartial`. The descriptive paragraph becomes an EmptyState body when `Model.Reports.Count == 0`.

**Expected size reduction:** ~140 lines → ~95 lines.

#### `Pages/CustomerProjects/Details.cshtml` (the headline rewrite)

- **Delete L10-30** (the entire hand-rolled `page-header` block). Replace with:
  ```razor
  @{
      var statusTone = p.Status switch { … };
      var headerVd = new ViewDataDictionary(ViewData) {
          ["HeaderTitle"] = p.Code,
          ["Subtitle"] = p.Name,
          ["TypeLabel"] = "Customer Project",
          ["StatusText"] = p.Status.ToString(),
          ["StatusTone"] = statusTone,
          ["Breadcrumbs"] = new (string, string)[] {
              ("Customer Projects", "/CustomerProjects"),
              (p.Code, "")
          },
          ["ShowBackLink"] = true,
          ["BackLinkFallback"] = "/CustomerProjects",
          ["BackLinkLabel"] = "Back to Customer Projects"
      };
  }
  @await Html.PartialAsync("Shared/_ScreenHeader", headerVd)
  ```
- **Refactor L96-250 (5 detail-card child sections)** to use the new `_ChildSectionHeader` partial:
  ```razor
  <section class="child-section">
      @await Html.PartialAsync("Primitives/_ChildSectionHeader",
          new ViewDataDictionary(ViewData) {
              ["Title"] = "Jobs",
              ["Count"] = Model.Jobs.Count
          })
      @if (Model.Jobs.Count == 0) { <p class="muted">No jobs linked yet.</p> }
      else { <table class="data-table">…</table> }
  </section>
  ```
- **Keep L96-258 structurally** but each section drops from ~25 lines to ~10 lines.

**Expected size reduction:** ~258 lines → ~155 lines.

#### `Pages/CustomerProjects/Index.cshtml`

- **Delete L9-24** (the hand-rolled page-header). Replace with `_ScreenHeader` invocation following the AccountsPayable/Index.cshtml pattern (which is canonical).
- KPI strip (L26-51) is fine; it's a status-filter chip pattern, not a metric duplicator.

**Expected size reduction:** ~127 lines → ~110 lines.

#### `Pages/CustomerProjects/Create.cshtml`

- **Delete L8-25** (the hand-rolled page-header). Replace with `_ScreenHeader` (ShowBackLink=true, BackLinkFallback=`/CustomerProjects`).
- **Delete L165** duplicate "Cancel" button — `_BackLink` inside `_ScreenHeader` already provides it.

**Expected size reduction:** ~170 lines → ~150 lines.

#### `Pages/Production/Details.cshtml`

- **Delete L10-30** (page-header). Replace with `_ScreenHeader`.
- **Delete L42-54** "Order Header" card's first 3 rows (Type / Status / Description) — already in H1 / status chip / subtitle.
- **Refactor L42 onwards** to use new `_ChildSectionHeader` partial.

#### `Pages/Production/Index.cshtml`, `Production/Create.cshtml`, `Production/Operations/Index.cshtml`

- Same `page-header` → `_ScreenHeader` conversion.

#### `Pages/Admin/WorkCenters/Index.cshtml`

- Convert page-header to `_ScreenHeader`. Move the *"BIC differentiator…"* italic copy from the subtitle into a doc comment — marketing prose has no place in a page subtitle.

#### `Pages/Admin/Routings/Index.cshtml`, `Admin/WorkCalendars/Index.cshtml`, `Admin/Carriers/Index.cshtml`, `Admin/Countries/Index.cshtml`

- Same page-header → `_ScreenHeader` conversion.

#### `Pages/Receiving/Details.cshtml`

- **Delete L45 "Receipt Information" section** — first card's first row literally restates HeaderTitle. Move ReceiptDate / ReceivedBy / ShippingCarrier / TrackingNumber / PackingSlip into the existing `_ReceiptDetailsContext.cshtml` Context partial.

#### `Pages/AccountsPayable/Details.cshtml`

- **Delete L27-30** EmptyState double-back CTA. The EmptyState already has one CtaHref ("Back to AP List"); the Secondary "Back to Dashboard" is noise — drop the SecondaryLabel/SecondaryHref properties.
- **Delete L202** nested "Back to AP" in EmptyState elsewhere on the page — same redundancy.

#### `Pages/Maintenance/WorkRequests/_WorkRequestDetailsContext.cshtml`

- **Delete the entire file.** Update the Details page that consumes it to set `Breadcrumbs` directly on `_ScreenHeader`.

#### `Pages/Admin/_AdminDataImportContext.cshtml`

- **Delete the entire file.** Same surgery as above.

#### `Pages/Shared/_AssetMaintenanceHeader.cshtml`

- **Delete the entire file.** Convert any page consuming it to `_ScreenHeader` + KpisPartial.

#### `Pages/Assets/Asset.cshtml`

- L70 manual `_BackLink` outside `_ScreenHeader` — replace by setting `ShowBackLink=true` on `heroHeaderVd` (L154-164) and letting `_ScreenHeader` render it in its standard position. The reason the manual call exists is the asset-hero wrapper sits *above* the heroHeader; if PR #4 normalizes that wrapper, the workaround can die.

---

## New partials needed in PR #4

Listed in order of leverage (highest first):

1. **`Pages/Shared/Primitives/_ChildSectionHeader.cshtml`** — drop-in `<h3>Title (count) + optional actions row</h3>` for the 30+ uses of the detail-card--full child-table pattern. Saves ~10 lines per use × 30+ uses = ~300 lines removed across the codebase.
2. **`Pages/Quality/Fai/_FaiDetailKpis.cshtml`** — wraps Characteristics/NonConform/Waived/AiRisk in the compact KpiBand format for `_ScreenHeader.KpisPartial`.
3. **`Pages/Quality/Fai/_FaiDetailContext.cshtml`** — wraps Form 1 Header fields (Drawing/Customer/CustomerProject/Created/Submitted/Approved) as Context chips for `_ScreenHeader.ContextPartial`.
4. **`Pages/Quality/Fai/_FaiDetailActions.cshtml`** — workflow buttons (Submit/Approve) for `_ScreenHeader.ActionsPartial`.
5. **`Pages/Quality/Fai/_FaiIndexActions.cshtml`** — single "+ New FAI" button for `_ScreenHeader.ActionsPartial`.
6. **`Pages/Admin/AssetImport/_PreviewKpis.cshtml`** — Total/Valid/Errors KpiBand. (Status already shown by `_ScreenHeader.StatusText`.)
7. **`Pages/Admin/AssetImport/_PreviewActions.cshtml`** — Commit/Re-validate/Discard buttons.
8. **`Pages/Admin/AssetImport/_DetailKpis.cshtml`** — totals KpiBand for committed batches.
9. **`Pages/Admin/AssetImport/_IndexActions.cshtml`** — Download Template + Upload Excel buttons.
10. **`Pages/Shared/Primitives/_InlineFormDisclosure.cshtml`** — disclosure-pattern wrapper that replaces `<div id="X" style="display:none">` + JS toggler used across CIP/Details, FAI/Detail, Maintenance/Details. Use `<details>` semantic where possible.

---

## Process / discipline recommendation

Per Dean's lock (*"Reuse shared Cockpit primitives, do NOT roll your own"*), PR #4 should also add a **CI lint rule** that fails any new `.cshtml` containing:
- `<div class="page-header">` or `<h1 class="page-title">` (use `_ScreenHeader` instead)
- `<nav class="breadcrumb"` or `<nav class="breadcrumb-trail"` outside `_ScreenHeader.cshtml` and `_ChildSectionHeader.cshtml`
- More than 3 instances of `<section class="detail-card--full">` (must use `_ChildSectionHeader`)
- `style="display: none"` + corresponding `toggleInlineForm` JS (use `_InlineFormDisclosure` instead)

Add this as a CI gate in `.github/workflows/` (mirroring `snapshot-drift-check.yml` from Lock 12). Without the gate, the next 5 PRs will reintroduce these patterns because they're the easy default.

---

## Lines-saved estimate

| Surface | Before | After | Saved |
|---------|--------|-------|-------|
| CustomerProjects/Details.cshtml | 258 | ~155 | ~100 |
| Quality/Fai/Detail.cshtml | 258 | ~140 | ~120 |
| Admin/AssetImport/Preview.cshtml | 211 | ~115 | ~95 |
| Admin/AssetImport/Detail.cshtml | 128 | ~75 | ~50 |
| Admin/AssetImport/Index.cshtml | 151 | ~95 | ~55 |
| Admin/AssetImport/Upload.cshtml | 63 | ~40 | ~25 |
| Quality/Fai/Index.cshtml | 140 | ~95 | ~45 |
| 12× PARALLEL-STACK pages | varies | ~25 lines/page | ~300 |
| 30+ child-table sections | varies | ~10 lines/section | ~300 |
| Deleted Context + Header files | ~60 | 0 | ~60 |
| **TOTAL** | | | **~1,150 lines** |

For a Sprint 13.6 PR #4 budget, this is a one-week ship with high visual impact and zero schema risk — pure Razor surgery against the existing `_ScreenHeader`, `_BackLink`, `_KpiStrip`, `_TabNav`, `_EmptyStateV2` primitives.

---

## Out-of-scope (deferred to PR #5+)

- **`Pages/Maintenance/Details.cshtml` modernization** — page has a "Switch to modern view" banner pointing to `/WorkOrders/Details`. Legacy surface; should be deprecated rather than refactored.
- **`Pages/Plant/Floor.cshtml`, `Pages/Plant/Index.cshtml`** — these use a custom plant-floor visualization, not the standard Detail/Index shape. Audit separately.
- **`Pages/Books/`, `Pages/Periods/`, `Pages/Journals/`** — Finance surfaces. Pattern fixes will mirror the Production/CustomerProjects edits but should be batched as their own PR after demo dry-run.
- **`Pages/Reports/`** — report builder pages have their own toolbar/render-target chrome; deferred.

---

## Sources audited

- `Pages/Shared/_ScreenHeader.cshtml` (151 lines, full chrome partial — confirmed renders breadcrumbs, back link, H1, subtitle, type label, status pill, context, KPIs, actions)
- `Pages/Shared/Primitives/Cockpit/_CockpitPageHeader.cshtml` (45 lines, Control Center variant)
- `Pages/Shared/_NavSidebar.cshtml` (66 lines, registry-driven)
- `Pages/Shared/_SectionCard.cshtml` (93 lines, generic card wrapper with icon library)
- `Pages/Shared/_BackLink.cshtml` (19 lines, single back link with ReturnUrlHelper)
- 14 deep-read pages: AssetImport/Preview, Detail, Index, Upload; Quality/Fai/Detail, Create, Index; CustomerProjects/Details, Index, Create; Production/Details, Index; Receiving/Details; CIP/Details; AccountsPayable/Details; Assets/Asset
- Greps run: `breadcrumb*` (19 files), `page-header|page-title|page-subtitle` (13 files), `_ScreenHeader` (131 files), `kpi-card|kpi-value|kpi-label`, `section-card-title`, `detail-card-title`, `pe-card__title`, `ShowBackLink`, `fa-arrow-left`, `Back to`
- Findings cross-checked against repo state at HEAD: PR #338 merged `fc31278` (FAI UI), PR #337 merged `f2ed63d` (AssetImport)

End of memo.
