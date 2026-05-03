# CSP Inline Migration Plan — Removing `script-src-attr` / `style-src-attr` `'unsafe-inline'`

Status: **DEFERRED** (Task #18 — see `.local/tasks/task-18.md`).
Owner: TBD. Scope is too large for an autonomous single-task run; this
document plus the tooling under `scripts/codemod_csp_inline.js` and
`wwwroot/js/csp-bootstrap.js` are deliverables to enable a future,
dedicated execution cycle.

---

## 1. Goal

Drop the last two CSP carve-outs from `Middleware/SecurityHeadersMiddleware.cs`:

```
script-src-attr 'unsafe-inline'
style-src-attr  'unsafe-inline'
```

so the CSP3 `*-src-attr` directives inherit from the nonce-only `script-src`
and `style-src`. After the migration, no inline `on*=` event-handler
attribute and no inline `style="…"` attribute may appear in any rendered
HTML response.

## 2. Current scope (measured 2026-05-03)

Source-tree counts under `Pages/**/*.cshtml`:

| Surface                              | Files | Call-sites |
| ------------------------------------ | ----: | ---------: |
| `onclick=`                           |     ~ |        333 |
| `onchange=`                          |     ~ |         22 |
| `oninput=`                           |     ~ |         21 |
| `onsubmit=`                          |     ~ |          7 |
| `ondrag*` / `ondrop=`                |     ~ |          5 |
| **Total inline event handlers**      | **116** | **~388** |
| `style="…"` attributes               | **147** | **~2 276** |

Verify before starting work:

```sh
rg --no-filename -o '\bon[a-z]+=' Pages -g '*.cshtml' | sort | uniq -c | sort -rn
rg -c ' style="' Pages -g '*.cshtml' | awk -F: '{s+=$2} END {print s}'
```

## 3. Hard constraints discovered during planning

1. **Razor loop scoping.** The vast majority of handlers live inside
   `@foreach (var x in …)` blocks and embed per-iteration expressions
   (`onclick="doX(@x.Id)"`). Any codemod that hoists the handler body to a
   file-end `<script>` block evaluates `@x.Id` exactly once and silently
   binds every row to the same value. Hoisted blocks must therefore be
   emitted **inside the loop scope**, not at file end.

2. **Invalid-HTML insertion points.** Companion `<script>` siblings cannot
   live as direct children of `<table>`, `<thead>`, `<tbody>`, `<tr>`,
   `<select>`, `<optgroup>`, etc. The `<tr class="clickable-row" onclick=…>`
   pattern alone appears on dozens of pages and forces the rewrite to
   either:
     * convert the click target to a wrapping `<a>`/`<button>` (preferred,
       more accessible), or
     * register the row via a delegated listener attached to the enclosing
       `<table>` keyed off `data-csp-action` + `data-csp-arg-*`.

3. **Razor encoding context drift.** A handler body that today renders as
   an attribute value (`@expr` is HTML-attribute-encoded — `"` → `&quot;`)
   will, when moved into a `<script>` body, be HTML-element-encoded
   (`"` stays as `"`, `<` → `&lt;`). Any handler that already works around
   the attribute encoder (e.g. `'@account.Name.Replace("'", "\\'")'`) must
   be re-audited after the move.

4. **`return false;` semantics.** A handful of handlers rely on the
   inline-attribute return-value contract to suppress default action
   (`onclick="…; return false;"`). The migrated `addEventListener` form
   must wrap the body and call `event.preventDefault(); event.stopPropagation();`
   when the body returns `false`.

5. **Dynamically-injected DOM.** Several pages append rows/cards via
   `fetch` + `innerHTML`. Any element rendered after the bootstrap's
   initial DOMContentLoaded pass must be picked up by a `MutationObserver`
   (already wired in the bootstrap, see §6).

## 4. Strategy

The migration is split into **three independent sub-projects** that can be
merged in order without breaking the app at any intermediate step.

### 4.1 Phase A — Inline `style="…"` → `data-csp-style="…"`

Lowest risk. Style values are pure CSS text, free of Razor logic in 99% of
cases, and the runtime bootstrap can copy them onto `el.style.cssText`
without invoking the CSS parser at parse time (CSP allows JS-driven style
mutation — only browser-parsed inline `style="…"` is gated by
`style-src-attr`).

Codemod: `scripts/codemod_csp_inline.js --pass=styles`.

Per-file behaviour:
  * `style="WIDTH:100px"` → `data-csp-style="WIDTH:100px"`
  * Skip occurrences inside `<style>…</style>` element bodies.
  * Skip the `style` attribute when it appears inside a Razor expression
    string literal (rare; flagged for manual review, never auto-rewritten).

Deliverable verification:
  * `rg ' style="' Pages -g '*.cshtml' | wc -l` → `0` (after the pass).
  * Visual smoke: `npm run test:ui` (dark-mode + datagrid suites).

Estimated effort: 0.5 dev-day once the codemod is reviewed.

### 4.2 Phase B — Inline event handlers → `data-csp-on*="…"` + per-scope script

Highest risk. Two sub-strategies, picked per call-site by the codemod's
heuristic:

**B-1: "Sibling-script" rewrite** (used when the parent element accepts a
`<script>` child — i.e. NOT inside `<table>` / `<select>` / `<tr>`).

```html
@{ var __c = $"csp_{Guid.NewGuid():N}"; }
<button data-csp-bind="@__c">Save</button>
<script>
  document.querySelector('[data-csp-bind="@__c"]').addEventListener('click', function (event) {
    saveRow(@row.Id);
  });
</script>
```

Notes: Razor evaluates the GUID + interpolation per loop iteration, so
each rendered button gets a unique selector and a correctly-scoped binding.

**B-2: "Delegated-action" rewrite** (used when sibling `<script>` is
invalid HTML, e.g. inside `<tr>` / `<option>` / `<thead>`).

```html
<tr class="clickable-row"
    data-csp-action="navigate"
    data-csp-arg-href="/Assets/Asset/@asset.Id">…</tr>
```

…handled by a dispatch table inside `wwwroot/js/csp-bootstrap.js`. The
codemod extracts the handler body into a named action; the bootstrap
contains the implementations. A small registry of ~15 actions covers
~80% of current call-sites (`navigate`, `submitForm`, `toggleHidden`,
`closePanel`, `clickById`, `setLocation`, `confirmAndSubmit`, …).

Codemod: `scripts/codemod_csp_inline.js --pass=handlers --strategy=auto`.

Manual-review queue: any handler body that the codemod cannot match
against the action registry AND whose insertion point is inside a
`<table>`/`<select>` is dumped to `out/csp_handler_manual_review.tsv`
for hand-rewrite (typically by lifting the click target out of the
`<tr>` into a wrapping `<a href>` — the recommended accessibility fix).

Deliverable verification:
  * `rg '\bon[a-z]+="' Pages -g '*.cshtml' | wc -l` → `0`.
  * Full Playwright suite green: `npm test`.

Estimated effort: 3–5 dev-days (codemod + manual queue).

### 4.3 Phase C — CSP middleware + smoke assertion

Once A and B land and CI is green for at least one full nightly cycle:

1. In `Middleware/SecurityHeadersMiddleware.cs`, delete the two lines
   inside `BuildCsp` and update the file header comment to reflect that
   the carve-out has been retired.
2. In `tests/smoke_phase4.spec.js`, in the existing
   *"Security headers — CSP/XCTO/Referrer/Permissions on /\_live"* test,
   append:

   ```js
   // Task #18 — the last `unsafe-inline` carve-out (script-src-attr /
   // style-src-attr) must be gone; CSP3 *-src-attr fall back to the
   // nonce-only script-src / style-src.
   expect(csp).not.toMatch(/script-src-attr[^;]*'unsafe-inline'/);
   expect(csp).not.toMatch(/style-src-attr[^;]*'unsafe-inline'/);
   ```

3. Re-run all CI suites: `smoke`, `flows`, `auth`, `nav`, `fa`, `reports`,
   `ui`.

Estimated effort: 0.5 dev-day.

## 5. Tooling delivered with this plan

  * `scripts/codemod_csp_inline.js` — Node.js codemod, dry-run by default,
    emits a unified diff and the manual-review TSV. Accepts `--pass=styles`,
    `--pass=handlers`, `--apply`, `--strategy=auto|sibling|delegated`,
    `--limit-to=<glob>`.
  * `wwwroot/js/csp-bootstrap.js` — runtime helper. Two responsibilities:
    1. On DOMContentLoaded **and** via a `MutationObserver`, copy
       `el.dataset.cspStyle` → `el.style.cssText` for every
       `[data-csp-style]` element.
    2. Expose `window.CspActions` — the dispatch table backing the
       Phase B-2 delegated-action rewrite. Pre-populated with the ~15
       common actions described in §4.2.

The bootstrap is **safe to ship today** even before the codemod runs:
absent any `data-csp-style` / `data-csp-action` attributes it is a no-op.
Wiring it into `_ModernLayout.cshtml` is part of Phase A.

## 6. Roll-back plan

Each phase is independently revertible at git granularity:

  * Phase A revert: restore `style=` attributes (codemod can run in
    reverse — `--reverse` flag in the script).
  * Phase B revert: restore inline handlers from a per-file backup written
    by the codemod into `out/csp_codemod_backup/`.
  * Phase C revert: re-add the two lines to `BuildCsp` and drop the new
    test assertions.

## 7. Open questions for the executing engineer

1. Are wrapping `<a href>` or `<button>` rewrites for the `<tr
   onclick="window.location='…'">` pattern acceptable accessibility-wise?
   (Recommended: yes — these rows are already announced as clickable.)
2. Do we want to keep `style-src-attr 'unsafe-inline'` for one extra
   release behind an `appsettings` flag (`Security:AllowInlineStyleAttr`)
   to give partner integrations time to migrate? Default off.
3. Should the codemod emit `data-csp-style` or expand the styles into
   utility CSS classes generated into `wwwroot/css/csp-utilities.css`?
   The latter is purer (no JS round-trip per element) but produces a
   long-tail of class names; the former is simpler and chosen here.
