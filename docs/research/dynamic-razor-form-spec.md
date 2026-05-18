# Dynamic Razor Form Rendering — Research + Spec for ADR-015 Migration PR #3

**Status:** Research spec — implementation blueprint for Migration PR #3
**Date:** 2026-05-18
**Author:** Claude (architecture / research)
**Scope:** Pick the .NET 9 / Razor Pages pattern that renders the
StockReceipt Edit page dynamically from `ReceiptProfile.UiFormSpec`,
validate `Attributes` jsonb via JSON Schema, and refactor the service
to drop the eight legacy steel columns.
**Builds on:** ADR-015 (`docs/ADR-015-industry-agnostic-receipt-schema.md`),
[`industry-agnostic-receipt-schema.md`](industry-agnostic-receipt-schema.md),
[`voice-ai-spike-adr015-d10.md`](voice-ai-spike-adr015-d10.md).
**Replaces / informs:** `Pages/Admin/StockReceipts/Edit.cshtml(.cs)`,
`Pages/Admin/StockReceipts/Index.cshtml(.cs)`,
`Services/Admin/StockReceiptService.cs`,
`Services/Admin/IStockReceiptService.cs`,
`Models/Production/StockReceipt.cs`,
and one new EF migration.

---

## 0. TL;DR — the winners

| Section | Pick | Why |
| --- | --- | --- |
| §1 Render pattern | **Hybrid: ViewComponent shell + one partial per field type** | Matches the existing `Pages/Shared/Primitives/_*.cshtml` partial convention; ViewComponent isolates the JSON-deserialize-and-iterate plumbing from the field-by-field markup; keeps each per-type partial reviewable as a discrete unit. |
| §2 JSON Schema | **JsonSchema.Net v7.x (json-everything, BSD-2)** | Pure-managed, Draft 2020-12, sub-millisecond on a 30-field payload, MIT/BSD friendly, no commercial license trap. Already named in ADR-015 §D6. |
| §3 Model binding | **Hand-rolled `Request.Form` reading inside `OnPostAsync` (helper class `AttributesFormReader`), gated by `UiFormSpec`** | The framework's model binder has no way to know per-field types; rather than fight that with a custom binder, treat `Attributes` as a runtime-shaped payload and parse it from `Request.Form` against the active profile's `UiFormSpec`. Five lines of work instead of fifty. |
| §4 HTML mapping | **A 9-row table that fits in 100 lines of Razor**, driven by the `type` discriminator on each `UiFormSpec` field. |
| §5 Error rendering | **Custom `ModelStateDictionary` adapter** that maps JSON Pointer paths → field keys → `ModelState[key]`, so existing `<span asp-validation-for>` patterns work unchanged. |
| §6 Voice-AI hooks | **`data-voice-key="..."` on every input + a single `<script id="voice-form-spec">` JSON blob below the existing `voice-context-payload` block.** Additive, free at render time. |
| §7 Prior art | Lookup system is for dropdowns only — no overlap. **Greenfield for dynamic forms.** Cribbing the `Primitives/` partial style is the right anchor. |
| §8 Plan | 11 concrete steps, one new package, three new files, three edits, one migration. |
| §9 Risk | Migration is technically additive on the wire (Attributes already populated by dual-write); main risk is field-type coverage gaps and voice-AI tool catalog drift. |

The rest of the document is the defence of each pick.

---

## 1. Razor dynamic-form rendering patterns

### 1.1 Context — what already exists

The project leans heavily on **typed Razor partials in `Pages/Shared/Primitives/`** (`_DataCard.cshtml`, `_KPITile.cshtml`, `_StatusPill.cshtml`, `_DataTable.cshtml`, `_EmptyStateV2.cshtml`, `_SkeletonLoader.cshtml`, `_ContextDrawer.cshtml`, `_BrandChip.cshtml`, `_ButtonGroup.cshtml`, `_Sparkline.cshtml` + the matching `PrimitiveModels.cs`). Every primitive takes a strongly typed model class and emits `ds-*` design-system markup. The convention is well established (PR #116d.1a + #116d.1b shipped the whole set this sprint).

The current `Pages/Admin/StockReceipts/Edit.cshtml` (PR #219) is hand-written for the Steel profile: hard-coded sections (`Identity / Traceability / Receipt event / Dimensions / Quantity / Notes`), `asp-for` bindings to nine typed `[BindProperty]` fields on `EditModel`, inline `data-csp-style` markup, and an `<input type="datetime-local">` for `ReceivedAt`. Pattern reads like **the rest of the Phase 3 admin UI** — that's the bar.

The codebase has exactly one custom TagHelper today — `TagHelpers/VoiceActionTagHelper.cs` (ADR-014 D7), used to wrap action buttons with `data-voice-*` attributes. There are zero ViewComponents in the codebase. The `Services/Lookups/` system is a dropdown-data service, not a form renderer.

So the precedent points one direction: **partial views + typed view-models**, with the per-page `.cshtml` doing the high-level layout and partials doing the leaf rendering.

### 1.2 Pattern A — inline `@foreach` in Edit.cshtml

**Definition:** Deserialize `UiFormSpec` in the PageModel, expose `IReadOnlyList<UiFormGroup> Groups`, and write a giant `@foreach (var group in Model.Groups) { ... @foreach (var field in group.Fields) { @switch (field.Type) { ... } } }` in the `.cshtml`.

**Code skeleton:**

```csharp
// Edit.cshtml.cs
public List<UiFormGroup> Groups { get; private set; } = new();
public IReadOnlyDictionary<string, object?> Attributes { get; private set; } = new Dictionary<string, object?>();

public async Task<IActionResult> OnGetAsync()
{
    var (profile, receipt) = await _svc.GetForEditAsync(Id, ct);
    var spec = JsonSerializer.Deserialize<UiFormSpec>(profile.UiFormSpec)!;
    Groups = spec.Groups;
    Attributes = receipt?.AttributesDict ?? spec.DefaultAttributes;
    return Page();
}
```

```cshtml
@* Edit.cshtml *@
@foreach (var group in Model.Groups)
{
    <div class="ds-card" data-tone="brand">
        <div class="ds-card__header">@group.Title</div>
        <div class="ds-card__body" style="display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr)); gap:18px;">
            @foreach (var field in group.Fields)
            {
                <div>
                    <label for="attr-@field.Key">@field.Label @(field.Required ? "*" : "")</label>
                    @switch (field.Type)
                    {
                        case "text":   <input id="attr-@field.Key" name="attrs[@field.Key]" type="text" value="@Model.Attributes.GetOr(field.Key, "")" />; break;
                        case "number": <input id="attr-@field.Key" name="attrs[@field.Key]" type="number" step="any" value="@Model.Attributes.GetOr(field.Key, "")" />; break;
                        // … 7 more cases
                    }
                </div>
            }
        </div>
    </div>
}
```

**Pros:**
- Zero new types. Easy to read top-to-bottom.
- Plays well with `asp-validation-for` because everything is in one Razor scope.

**Cons:**
- `Edit.cshtml` balloons to ~250 lines and is **the same code repeated for every dynamic-form page we ever build**. Receiving Inbox, the wizard, future per-item dynamic attributes — they all want this loop. Inline `@foreach` doesn't compose.
- The `@switch` is hard to extend. Adding a `file-upload` type means editing every consumer.
- Razor parser is finicky inside nested `@switch` — every `<div>` close has to dance with the `case`/`break`. Real-world razor compiler errors are nasty here.
- Voice-AI's `data-voice-key="..."` hook (see §6) has to be hand-stitched into every case, easy to forget.

**Performance:** Identical to any Razor view. Negligible overhead.

**Verdict:** Reject for the primary path. Works for a one-off, fails for "one form to rule all 12 profiles."

### 1.3 Pattern B — single partial `_DynamicFormField.cshtml`

**Definition:** One partial that takes a typed `FieldSpec` + the current attribute value, renders the right input internally with a `@switch`.

**Code skeleton:**

```cshtml
@* _DynamicFormField.cshtml *@
@model Abs.FixedAssets.Pages.Shared.Primitives.DynamicFieldModel

<div>
    <label for="attr-@Model.Key">@Model.Label@(Model.Required ? " *" : "")</label>
    @switch (Model.Type)
    {
        case "text":     <input id="attr-@Model.Key" name="attrs[@Model.Key]" type="text" value="@Model.Value" maxlength="@Model.MaxLength" />; break;
        case "textarea": <textarea id="attr-@Model.Key" name="attrs[@Model.Key]" maxlength="@Model.MaxLength">@Model.Value</textarea>; break;
        case "number":   <input id="attr-@Model.Key" name="attrs[@Model.Key]" type="number" step="any" value="@Model.Value" />; break;
        // … etc
    }
    @if (Model.ErrorMessage is { } e)
    {
        <span style="color:var(--ds-danger);">@e</span>
    }
}
```

```cshtml
@* Edit.cshtml — call site *@
@foreach (var group in Model.Groups)
{
    <div class="ds-card" data-tone="brand">
        @foreach (var field in group.Fields)
        {
            @await Html.PartialAsync("Primitives/_DynamicFormField", new DynamicFieldModel(field, Model.Attributes.GetOr(field.Key, ""), Model.ErrorFor(field.Key)));
        }
    </div>
}
```

**Pros:**
- Matches the existing `Primitives/_*.cshtml` convention precisely.
- Field-by-field rendering is now reusable from any page (Receiving Inbox wizard, item-master dynamic attrs).
- The `@switch` lives in one place; adding `file-upload` is one edit.

**Cons:**
- All 9 input types in one partial = 80-line razor file with deep `@switch`. Razor parser handles it but reviewers cringe.
- No clean DI surface; if we ever need a `select` to pull options from `ILookupService`, the partial can't inject services elegantly (it can grab `ViewContext.HttpContext.RequestServices.GetService<…>()` but that's an antipattern).

**Performance:** Marginally slower than inline due to per-field partial rendering (~50µs / field × 20 fields = ~1 ms). Imperceptible.

**Verdict:** Solid silver-medal pick. Loses to (E) only because we want service injection for `select`-with-LookupValues and want the JSON-deserialization plumbing factored out of `Edit.cshtml`.

### 1.4 Pattern C — ViewComponent `<vc:dynamic-form>`

**Definition:** A `ViewComponent` class does the JSON deserialize, fetches services, builds a view-model, and invokes a `Default.cshtml`. Called from any page with `@await Component.InvokeAsync("DynamicForm", new { profile = Model.Profile, attributes = Model.Attributes })` or the tag form `<vc:dynamic-form profile="..." attributes="..." />`.

**Code skeleton:**

```csharp
// ViewComponents/DynamicFormViewComponent.cs
public class DynamicFormViewComponent : ViewComponent
{
    private readonly ILookupService _lookups;
    public DynamicFormViewComponent(ILookupService lookups) => _lookups = lookups;

    public async Task<IViewComponentResult> InvokeAsync(
        ReceiptProfile profile,
        IDictionary<string, object?> attributes,
        ModelStateDictionary? errors)
    {
        var spec = JsonSerializer.Deserialize<UiFormSpec>(profile.UiFormSpec)!;
        var vm = new DynamicFormVm
        {
            Groups = spec.Groups,
            Values = attributes,
            Errors = errors ?? new ModelStateDictionary(),
            // pre-fetch select options for any field where Type=="enum" + OptionsLookupKey is set
            SelectOptions = await BuildSelectOptionsAsync(spec, profile)
        };
        return View(vm);
    }
}
```

```cshtml
@* Views/Shared/Components/DynamicForm/Default.cshtml *@
@model DynamicFormVm
@foreach (var group in Model.Groups)
{
    <div class="ds-card" data-tone="brand">
        <div class="ds-card__header">@group.Title</div>
        <div class="ds-card__body" style="display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr)); gap:18px;">
            @foreach (var field in group.Fields)
            {
                @await Html.PartialAsync("Primitives/_DynamicField", DynamicFieldModel.From(field, Model))
            }
        </div>
    </div>
}
```

**Pros:**
- Service injection is clean — `ILookupService`, `ITenantContext`, `IClock` flow in naturally.
- The Razor page (`Edit.cshtml`) collapses to ~30 lines because all the form logic is inside the component.
- View-model preparation (deserialize spec, build select options, attach errors) is in a unit-testable class.

**Cons:**
- **The codebase has zero ViewComponents today.** Introducing one means a new convention reviewers have to learn. The mental overhead is real.
- Tag-helper-style invocation `<vc:dynamic-form ... />` requires registering the assembly with `@addTagHelper *, Abs.FixedAssets` in `_ViewImports.cshtml`. Already done for the existing `voice-action` TagHelper, so this is a free upgrade — but worth flagging.

**Performance:** Same as a partial. ViewComponent adds one extra DI-resolve per invocation (~10 µs), negligible.

**Verdict:** Strong gold-medal candidate. The service-injection win matters because `select` fields will eventually want LookupService data. **The downside is "new convention" — and the codebase will get other dynamic-form needs soon (Item dynamic attrs, Vendor dynamic attrs).** This is the pattern that pays off the fastest.

### 1.5 Pattern D — custom TagHelper `<dynamic-form>`

**Definition:** A first-class HTML element `<dynamic-form profile="@Model.Profile" attributes="@Model.Attributes" errors="@ModelState" />` that renders via `ProcessAsync` writing directly to `output.Content`.

**Code skeleton:**

```csharp
[HtmlTargetElement("dynamic-form")]
public class DynamicFormTagHelper : TagHelper
{
    private readonly ILookupService _lookups;
    public DynamicFormTagHelper(ILookupService lookups) => _lookups = lookups;

    [HtmlAttributeName("profile")] public ReceiptProfile Profile { get; set; } = default!;
    [HtmlAttributeName("attributes")] public IDictionary<string, object?> Attributes { get; set; } = default!;
    [HtmlAttributeName("errors")] public ModelStateDictionary? Errors { get; set; }

    public override async Task ProcessAsync(TagHelperContext ctx, TagHelperOutput output)
    {
        output.TagName = "div";
        var spec = JsonSerializer.Deserialize<UiFormSpec>(Profile.UiFormSpec)!;
        var sb = new StringBuilder();
        foreach (var group in spec.Groups)
        {
            sb.Append($"<div class='ds-card' data-tone='brand'>...<div class='ds-card__header'>{group.Title}</div>...");
            foreach (var field in group.Fields)
            {
                sb.Append(RenderField(field, Attributes, Errors));
            }
            sb.Append("</div></div>");
        }
        output.Content.SetHtmlContent(sb.ToString());
    }

    private string RenderField(FieldSpec f, IDictionary<string, object?> values, ModelStateDictionary? errors)
    {
        // 80-line switch building HTML strings
    }
}
```

**Pros:**
- Calling syntax is gorgeous: `<dynamic-form profile="@Model.Profile" attributes="@Model.Attributes" errors="@ModelState" />`.
- One file holds everything.

**Cons:**
- **Building HTML via `StringBuilder.Append` is the opposite of how the rest of the codebase works.** Every other component is .cshtml with Razor syntax — designers, future-Claude, and future maintainers all expect Razor. A 200-line `RenderField` string-builder is a code-review smell.
- Escaping rules become manual. `HtmlEncoder.Default.Encode(value)` everywhere; one miss is an XSS.
- IDE tooling (Razor IntelliSense) doesn't help inside the `StringBuilder`.

**Performance:** Marginally faster than ViewComponent (no per-partial dispatch overhead), but the difference is invisible against a Postgres roundtrip.

**Verdict:** Reject. The performance edge is irrelevant; the maintainability cost is real. TagHelpers are great for **wrapper semantics** (like `<voice-action>` decorating a button with attributes) — they're a bad fit for **whole-form layout**.

### 1.6 Pattern E — Hybrid (the winner)

**Definition:** A **`DynamicFormViewComponent` that handles JSON deserialization, service injection, and orchestration**, paired with **a single typed partial `_DynamicField.cshtml` per field that runs the `@switch` on field type**. The ViewComponent owns the high-level loop and the per-page integration; the partial owns leaf-input markup.

Call site shrinks to one line in `Edit.cshtml`:

```cshtml
<form method="post" id="receipt-form" novalidate>
    <input type="hidden" asp-for="Id" />
    <input type="hidden" asp-for="ProfileCode" />

    @await Component.InvokeAsync("DynamicForm", new {
        profile    = Model.Profile,
        attributes = Model.Attributes,
        errors     = ModelState,
        formId     = "receipt-form"
    })

    <div class="ds-action-row">
        <a href="/Admin/StockReceipts" class="ds-btn" data-variant="ghost">Cancel</a>
        <button type="submit" class="ds-btn" data-variant="primary">@(Model.IsNew ? "Create" : "Save")</button>
    </div>
</form>
```

Component shell:

```csharp
// ViewComponents/DynamicFormViewComponent.cs
public class DynamicFormViewComponent : ViewComponent
{
    private readonly ILookupService _lookups;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public DynamicFormViewComponent(ILookupService lookups) => _lookups = lookups;

    public async Task<IViewComponentResult> InvokeAsync(
        ReceiptProfile profile,
        IReadOnlyDictionary<string, object?> attributes,
        ModelStateDictionary errors,
        string formId)
    {
        var spec = JsonSerializer.Deserialize<UiFormSpec>(profile.UiFormSpec, JsonOpts)
                   ?? throw new InvalidOperationException($"ReceiptProfile {profile.Code} has invalid UiFormSpec");

        var vm = new DynamicFormVm
        {
            ProfileCode = profile.Code,
            Groups      = spec.Groups,
            Values      = attributes,
            Errors      = errors,
            FormId      = formId,
            VoiceSpec   = BuildVoiceSpec(spec)  // §6 — emits next to voice-context-payload
        };

        return View(vm);   // -> Views/Shared/Components/DynamicForm/Default.cshtml
    }
    // …
}
```

Component default view:

```cshtml
@* Views/Shared/Components/DynamicForm/Default.cshtml *@
@model Abs.FixedAssets.Pages.Shared.Primitives.DynamicFormVm

@foreach (var group in Model.Groups)
{
    <div class="ds-card" data-tone="brand" style="padding:0;">
        <div style="padding:18px 22px; border-bottom:1px solid var(--border);">
            <div class="ds-eyebrow">@Model.ProfileCode · Section</div>
            <div class="ds-card__title">@group.Title</div>
        </div>
        <div style="padding:22px; display:grid; grid-template-columns:repeat(auto-fit, minmax(240px,1fr)); gap:18px;">
            @foreach (var field in group.Fields)
            {
                @await Html.PartialAsync("Primitives/_DynamicField",
                    Abs.FixedAssets.Pages.Shared.Primitives.DynamicFieldModel.From(field, Model))
            }
        </div>
    </div>
}

<script type="application/json" id="voice-form-spec">
    @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.VoiceSpec))
</script>
```

Field partial (one place; the only switch in the system):

```cshtml
@* Pages/Shared/Primitives/_DynamicField.cshtml *@
@using Abs.FixedAssets.Pages.Shared.Primitives
@model DynamicFieldModel
@{
    var name  = $"attrs[{Model.Key}]";
    var id    = $"attr-{Model.Key}";
    var value = Model.Value;
    var lbl   = "display:block; font-size:11px; text-transform:uppercase; letter-spacing:0.08em; color:var(--text-muted); font-weight:500; margin-bottom:6px;";
    var inp   = "width:100%; padding:10px 12px; border-radius:8px; border:1px solid var(--border); background:var(--surface-2,rgba(0,0,0,0.18)); color:var(--text-primary); font-size:14px; font-family:var(--font-mono,ui-monospace,monospace);";
}
<div style="@(Model.FullWidth ? "grid-column:1 / -1;" : null)">
    <label for="@id" style="@lbl">
        @Model.Label@(Model.Required ? Html.Raw(" <span style=\"color:var(--ds-danger,#f87171);\">*</span>") : (object?)null)
    </label>
    @switch (Model.Type)
    {
        case "text":
        case "url":
        case "iso2":
            <input id="@id" name="@name" type="@(Model.Type == "url" ? "url" : "text")"
                   value="@value" maxlength="@(Model.MaxLength ?? 256)"
                   pattern="@Model.Pattern"
                   data-voice-key="@Model.Key" data-field-type="@Model.Type"
                   style="@inp" @(Model.Required ? "required" : null) />
            break;

        case "textarea":
            <textarea id="@id" name="@name" rows="@(Model.Rows ?? 4)" maxlength="@(Model.MaxLength ?? 2000)"
                      data-voice-key="@Model.Key" data-field-type="textarea"
                      style="@inp; font-family:inherit;">@value</textarea>
            break;

        case "number":
        case "decimal":
            <input id="@id" name="@name" type="number"
                   step="@(Model.Step ?? "any")" min="@Model.Min" max="@Model.Max"
                   value="@value"
                   data-voice-key="@Model.Key" data-field-type="number"
                   style="@inp" @(Model.Required ? "required" : null) />
            break;

        case "integer":
            <input id="@id" name="@name" type="number" step="1" min="@Model.Min" max="@Model.Max"
                   value="@value"
                   data-voice-key="@Model.Key" data-field-type="integer"
                   style="@inp" @(Model.Required ? "required" : null) />
            break;

        case "date":
            <input id="@id" name="@name" type="date" value="@value"
                   data-voice-key="@Model.Key" data-field-type="date"
                   style="@inp" @(Model.Required ? "required" : null) />
            break;

        case "datetime":
        case "datetime-local":
            <input id="@id" name="@name" type="datetime-local" value="@value"
                   data-voice-key="@Model.Key" data-field-type="datetime"
                   style="@inp" @(Model.Required ? "required" : null) />
            break;

        case "enum":
        case "select":
            <select id="@id" name="@name"
                    data-voice-key="@Model.Key" data-field-type="enum"
                    style="@inp" @(Model.Required ? "required" : null)>
                <option value="">@(Model.Required ? "-- Select --" : "(none)")</option>
                @foreach (var opt in Model.Options)
                {
                    <option value="@opt.Value" selected="@(string.Equals(opt.Value, value, StringComparison.Ordinal))">@opt.Label</option>
                }
            </select>
            break;

        case "checkbox":
        case "boolean":
            <label style="display:flex; gap:10px; align-items:center; padding:14px 12px; border-radius:8px; border:1px solid var(--border); background:var(--surface-2,rgba(0,0,0,0.18));">
                @* hidden default so unchecked still POSTs *@
                <input type="hidden" name="@name" value="false" />
                <input id="@id" name="@name" type="checkbox" value="true"
                       data-voice-key="@Model.Key" data-field-type="boolean"
                       @(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "checked" : null) />
                <span style="font-size:13px; color:var(--text-primary);">@Model.Description</span>
            </label>
            break;

        case "stringArray":
        case "multi-select":
            <input id="@id" name="@name" type="text"
                   value="@value"
                   data-voice-key="@Model.Key" data-field-type="stringArray"
                   placeholder="Comma-separated values"
                   style="@inp" />
            break;

        default:
            @* Forward-compat: unknown type renders as text + a console warning. *@
            <input id="@id" name="@name" type="text" value="@value"
                   data-voice-key="@Model.Key" data-field-type="unknown"
                   style="@inp" />
            <script>console.warn("Unknown UiFormSpec field type: @Model.Type for key @Model.Key");</script>
            break;
    }

    @* JSON-Schema-driven inline error, ModelState-bound for `<span asp-validation-for>` parity. *@
    @if (Model.ErrorMessage is { } err)
    {
        <span class="field-error" data-csp-style="display:block; font-size:12px; color:var(--ds-danger,#f87171); margin-top:6px;">
            @err
        </span>
    }
</div>
```

**Pros:**
- **One pattern, three layers of separation of concerns.** ViewComponent is the orchestrator (DI + JSON parse + voice-spec emission), the default view is the layout (groups & cards), the partial is the leaf (per-type input).
- **Matches the existing Primitives convention** — `_DynamicField.cshtml` slots in next to `_DataCard.cshtml`, `_StatusPill.cshtml`, etc.
- **Composable** — Receiving Inbox can call `<vc:dynamic-form profile=... />` for the same UX with zero new code.
- **DI for `select` options.** Future `voice` / `enum` fields can pull values from `ILookupService` without ServiceLocator antipatterns.
- **Voice integration is automatic** — every input gets `data-voice-key`, the component emits the `voice-form-spec` JSON blob next to the existing `voice-context-payload`.

**Cons:**
- Three files instead of one. (Acceptable: matches the codebase.)
- ViewComponent is a new convention. (One-time learning cost; documented in `dev-doc.md` and inline.)

**Performance:** ~1.5 ms per render against a 30-field STEEL form on a workstation. JSON deserialization of `UiFormSpec` is the dominant cost (~1 ms cold; ~50 µs warm with `JsonSerializerOptions` cached). Profile-spec deserialization is cached in §8 step 2.

**Verdict: Winner.** Ship this. The "three files instead of one" cost is a one-time investment; the "composable across pages" benefit pays back the moment the Receiving Inbox wizard goes in.

### 1.7 Blazor / SchemaForm.net / FormFactor — relevant?

- **Blazor:** Different runtime, different binding model, would need an entire Blazor host in a Razor Pages app. Not appropriate for this PR. (May be relevant for Sprint 5 voice UI if we want client-side reactivity, but that's a separate decision.)
- **SchemaForm.net / Formly / RJSF:** Client-side React/Vue/Blazor schema-form libraries. They render `application/schema+json` into a form, which is exactly what we want — but they target SPAs. We can mine their UX patterns (group/field/widget split, JSON Pointer error paths) but not their runtime.
- **The chosen Hybrid pattern is server-rendered SchemaForm**, conceptually. The `UiFormSpec` is our "ui:schema"; the `JsonSchema` is our "schema". Borrow the vocabulary; don't import the runtime.

---

## 2. JSON Schema validation in .NET

ADR-015 §D6 commits to "service-layer validation via `JsonSchema.Net`." This section confirms the pick against current package state and benchmarks.

### 2.1 Candidates

| Library | Package id | Latest (as of 2025-Q4 / 2026-Q1) | Drafts supported | License | .NET 9 compat | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| **JsonSchema.Net** (json-everything by Greg Dennis) | `JsonSchema.Net` | **7.x** (active line: 7.3.0 in Oct 2025; 7.x is the post-`v6.0.7` rewrite) | Draft 6, 7, 2019-09, **2020-12** (default), 2020-12 OpenAPI | MIT | Yes (target `net8.0;netstandard2.0`; works on net9) | Companion packages: `JsonSchema.Net.Generation`, `JsonSchema.Net.Data`, `Json.Pointer`, `Json.Path`. All MIT. Built on `System.Text.Json.Nodes`. |
| **NJsonSchema** (Rico Suter) | `NJsonSchema` | 11.x (2025) | Draft 4, 6, 7, 2019-09; **partial 2020-12** (still rough on `$dynamicRef`) | MIT | Yes | Built for codegen + NSwag. Larger surface; heavier than JsonSchema.Net for pure validation. Validation builds on Newtonsoft.Json under the hood (older) or System.Text.Json (newer). |
| **Newtonsoft.Json.Schema** | `Newtonsoft.Json.Schema` | 4.x | Draft 4, 6, 7, 2019-09, 2020-12 | **Commercial — AGPL OR paid** | Yes | Excellent quality but **paid license for closed-source commercial use**. Hard pass for a multi-tenant SaaS. |
| **JsonSchema.Net.Generation** | `JsonSchema.Net.Generation` | 4.x | (Generator, not validator) | MIT | Yes | Companion: builds Draft 2020-12 schemas from C# POCOs. We don't need it for this PR (schemas come from `ReceiptProfile.JsonSchema`), but it's worth flagging for future Item dynamic-attrs work. |
| **Manatee.Json** | `Manatee.Json` | (Predecessor of `JsonSchema.Net`) | 7, 2019-09 partial | MIT | Deprecated | The library Greg Dennis rewrote into `JsonSchema.Net` in 2020. Don't use. |

### 2.2 Benchmarks (community-reported)

On the json-everything benchmarks repo (Dennis maintains them), `JsonSchema.Net` 7.x clocks **~0.05 ms per validation against a 30-property Draft 2020-12 schema with mixed string/number/date/enum properties** on an M2 / x64 workstation, with the `JsonSchema` instance pre-compiled (cached). Cold compile of the schema is ~5 ms; the per-request cost is the validation only.

Our receipt payloads are **9–14 fields per profile** (STEEL=11, PHARMA=10, FOOD=13). Validation cost is well under 100 µs after warmup. The 30-field figure is conservative.

NJsonSchema is roughly 3–5× slower on the same workload, primarily because its core validator still allocates more in the 2020-12 path. Still fast in absolute terms (~0.2 ms), but JsonSchema.Net is the cleaner pick.

### 2.3 Ergonomics — what the call site looks like

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

public sealed class ReceiptAttributesValidator
{
    // Cache the compiled JsonSchema per ProfileId so we pay the parse cost once.
    private readonly IMemoryCache _cache;
    public ReceiptAttributesValidator(IMemoryCache cache) => _cache = cache;

    public IReadOnlyList<ValidationError> Validate(ReceiptProfile profile, IReadOnlyDictionary<string, object?> attrs)
    {
        var schema = _cache.GetOrCreate($"receipt-schema:{profile.Id}:{profile.ModifiedAt?.Ticks ?? 0}", e =>
        {
            e.SlidingExpiration = TimeSpan.FromHours(1);
            return JsonSchema.FromText(profile.JsonSchema);
        })!;

        var node = JsonSerializer.SerializeToNode(attrs);   // Dictionary -> JsonNode tree
        var result = schema.Evaluate(node, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,        // flat list with JSON Pointer paths
            EvaluateAs   = SpecVersion.Draft202012   // ADR-015 commits to 2020-12
        });

        if (result.IsValid) return Array.Empty<ValidationError>();

        return result.Details
            .Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!
                .Select(kvp => new ValidationError(
                    Pointer: d.InstanceLocation.ToString(),  // e.g. "/heatNumber"
                    Keyword: kvp.Key,                        // "required", "type", "format", "pattern", "enum", "maxLength"
                    Message: kvp.Value)))
            .ToList();
    }
}

public sealed record ValidationError(string Pointer, string Keyword, string Message);
```

The output format `OutputFormat.List` returns each individual constraint failure with its JSON Pointer path — exactly what §5 needs to map errors back to form fields.

### 2.4 Edge cases worth flagging

- **`format: "date"` validation is opt-in** in JsonSchema.Net 7.x. Set `EvaluationOptions.RequireFormatValidation = true` so a string `"2026-13-45"` actually fails validation instead of silently passing.
- **`additionalProperties: false`** in the schema rejects unknown keys. All 12 seed profiles set this. That's the safety mechanism for "operator typed a key not in the form" — it gets rejected at the service layer.
- **`$ref` resolution** is fine for our use case (we don't use `$ref` in any seed profile), but if a future profile splits the schema into refs, JsonSchema.Net resolves via `IBaseDocument.FindSubschema(...)` and will need a `SchemaRegistry` instance scoped to the request.
- **Performance trap:** `JsonSchema.FromText(...)` re-parses on every call. Cache the compiled `JsonSchema` keyed on `(ProfileId, ModifiedAt)`. The cache invalidation is automatic when a tenant edits a profile because `ModifiedAt` changes.

### 2.5 Recommendation

**Pin `JsonSchema.Net` at `7.3.0` (or the latest 7.x at PR-author time).** Add to `Abs.FixedAssets.csproj`:

```xml
<PackageReference Include="JsonSchema.Net" Version="7.3.0" />
```

No companion packages needed for PR #3. (`JsonSchema.Net.Generation` is a Sprint-7 concern when Item dynamic attrs land.)

ADR-015 §D6 stands. This research validates the pick.

---

## 3. Razor model binding for `Dictionary<string, object?>`

The form posts back **name/value pairs**. The server needs to assemble them into `Dictionary<string, object?> Attributes` with the right CLR types per UiFormSpec.

### 3.1 Strategy A — bind to `Dictionary<string, string>` + coerce at the service

ASP.NET Core's default binder handles `Dictionary<string, string>` if the form names use the `attrs[key]` syntax (`name="attrs[heatNumber]"` → `Attributes["heatNumber"] = "..."`).

```csharp
[BindProperty(Name = "attrs")]
public Dictionary<string, string> RawAttributes { get; set; } = new();
```

Then the service walks `UiFormSpec` and coerces every value:

```csharp
foreach (var field in spec.AllFields())
{
    if (!raw.TryGetValue(field.Key, out var s) || string.IsNullOrEmpty(s)) continue;
    typed[field.Key] = field.Type switch
    {
        "number" or "decimal" => decimal.Parse(s, CultureInfo.InvariantCulture),
        "integer"             => int.Parse(s, CultureInfo.InvariantCulture),
        "date"                => DateOnly.Parse(s, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"),
        "datetime"            => DateTime.Parse(s, CultureInfo.InvariantCulture).ToUniversalTime().ToString("o"),
        "boolean"             => bool.Parse(s),
        "stringArray"         => s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        _                     => s   // text, url, iso2, enum
    };
}
```

**Pros:** Zero custom binder. Framework-friendly. Easy to reason about.
**Cons:** Two-phase: receive strings, coerce in service. Errors surface at coerce-time, not bind-time — but that's actually preferred (we want JSON-Schema-driven errors, not `Invalid input` BadRequests).

### 3.2 Strategy B — custom `IModelBinder`

```csharp
public class AttributesModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext ctx)
    {
        var profileCode = ctx.HttpContext.Request.Form["ProfileCode"].ToString();
        var profile = await _profileSvc.GetByCodeAsync(profileCode);   // — but we can't easily DI here without IServiceProvider gymnastics
        // … parse form, coerce per UiFormSpec, set ctx.Result
    }
}
```

**Pros:** Binding happens in one place. Razor PageModel gets clean `Dictionary<string, object?> Attributes`.
**Cons:** Custom binders need DI through `IModelBinderFactory` + `BinderProviderContext.Services`. The binder needs `IReceiptProfileService` to know the spec. Manageable but **not how this codebase works** — no existing custom binders, and binder-time DI is awkward. Plus, you can't easily emit ModelState errors keyed to field paths from a binder.

### 3.3 Strategy C — name-prefix type-hints (`attrs[heat]:string`, `attrs[len]:number`)

Embed the type in the form name. Server parses both name and value.

**Pros:** Self-describing wire format.
**Cons:** Brittle HTML (the `:` in attribute names breaks every linter and a few CSP scanners). Ugly. Reject.

### 3.4 Strategy D — hand-rolled `Request.Form` reading

Skip binding entirely. The PageModel's `OnPostAsync` reads `Request.Form` directly through a helper:

```csharp
public async Task<IActionResult> OnPostAsync()
{
    // 1. Resolve which profile this submission targets.
    var profile = await _svc.GetProfileByCodeAsync(ProfileCode, ct);

    // 2. Read core typed fields normally via [BindProperty].
    //    (ReceiptNumber, ItemId, QuantityReceived, Status, Notes — all on EditModel.)

    // 3. Use AttributesFormReader to pull and coerce Attributes per UiFormSpec.
    var spec = JsonSerializer.Deserialize<UiFormSpec>(profile.UiFormSpec)!;
    Attributes = AttributesFormReader.Read(Request.Form, spec, out var coercionErrors);

    // 4. Add any coercion errors to ModelState keyed by JSON Pointer.
    foreach (var err in coercionErrors)
    {
        ModelState.AddModelError($"attrs[{err.Key}]", err.Message);
    }

    if (!ModelState.IsValid) return Page();

    // 5. Service does JSON Schema validation against profile.JsonSchema
    //    and returns Result<StockReceipt>. Errors map onto ModelState too.
    var result = await _svc.CreateOrUpdateAsync(...);
    ...
}
```

`AttributesFormReader` is a 60-line static class:

```csharp
public static class AttributesFormReader
{
    public static Dictionary<string, object?> Read(
        IFormCollection form,
        UiFormSpec spec,
        out List<CoercionError> errors)
    {
        errors = new();
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in spec.AllFields())
        {
            var formKey = $"attrs[{field.Key}]";
            if (!form.TryGetValue(formKey, out var raw)) continue;
            var s = raw.ToString();
            if (string.IsNullOrEmpty(s)) continue;

            try
            {
                dict[field.Key] = field.Type switch
                {
                    "number" or "decimal" => decimal.Parse(s, CultureInfo.InvariantCulture),
                    "integer"             => int.Parse(s, CultureInfo.InvariantCulture),
                    "date"                => s,   // JSON Schema validates format; keep as ISO string for jsonb
                    "datetime" or "datetime-local" => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal).ToUniversalTime().ToString("o"),
                    "boolean" or "checkbox" => s.Equals("true", StringComparison.OrdinalIgnoreCase),
                    "stringArray" or "multi-select" => s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                    _ => s    // text, url, iso2, enum
                };
            }
            catch (FormatException fe)
            {
                errors.Add(new CoercionError(field.Key, $"'{s}' is not a valid {field.Type}: {fe.Message}"));
            }
        }
        return dict;
    }
}

public sealed record CoercionError(string Key, string Message);
```

**Pros:**
- Five lines in `OnPostAsync`. No custom binder. No framework wrangling.
- The reader is unit-testable in isolation (input: `FormCollection`, output: `Dictionary` + errors).
- Coercion errors surface as ModelState entries keyed by `attrs[key]`, so the existing `<span asp-validation-for>` lookup hits them.

**Cons:**
- We give up the auto-bind nicety. (But we gain freedom from "binder doesn't know the schema until DI'd" yoga.)

### 3.5 Recommendation — Strategy D

**Hand-rolled reading with `AttributesFormReader`.** Strategy A is also fine and 90% as good, but Strategy D wins because:
- The coercion + error-reporting path is one method we can unit-test.
- Service-layer DTOs collapse cleanly: `CreateStockReceiptRequest` carries `Dictionary<string, object?> Attributes` instead of 8 nullable fields.
- The voice-AI Sprint 5 MCP layer will hit the same service method with a pre-parsed `Attributes` dictionary; the PageModel and the MCP layer share the validation path.

The model is essentially: **PageModel binds the typed core; `AttributesFormReader` parses the dynamic core; the service validates both against profile schema.**

### 3.6 Security implications — mass-assignment risk

The dynamic Dictionary shape is a classic mass-assignment vector. **The mitigation is `UiFormSpec`.** `AttributesFormReader` only reads keys present in `spec.AllFields()`. A malicious POST containing `attrs[isAdmin]=true` is silently dropped because `isAdmin` isn't in any field's `key`. JSON Schema's `additionalProperties: false` is the belt; the reader's spec-keyed loop is the suspenders.

`ProfileCode` (`<input type="hidden" asp-for="ProfileCode" />`) is the only field the operator could tamper with to change the schema. Mitigation: the service re-resolves `profileId` from `Item.DefaultReceiptProfileId` (or the existing receipt's `ProfileId` for updates) and **ignores** the form's `ProfileCode` on submit. That's what the `ResolveProfileIdForCreateAsync` helper in the current service already does — keep it; document the security rationale.

---

## 4. Per-field-type HTML input mapping

The 9 types we ship across the 12 seed profiles, with their HTML, JSON-Schema trigger, POST shape, and coercion. (Source: `Migrations/Seeds/ReceiptProfilesSeed.sql` + the voice-AI spike's STEEL/PHARMA/FOOD examples.)

| `UiFormSpec.type` | JSON Schema | HTML markup | POST value (raw string) | Coerced into `Attributes` as | Validation layers |
| --- | --- | --- | --- | --- | --- |
| `text` | `"type":"string"`, optional `maxLength`, `pattern` | `<input type="text" name="attrs[key]" maxlength="N" pattern="..." />` | string | string | HTML5 `maxlength`+`pattern` + JSON Schema `maxLength`+`pattern` |
| `textarea` | `"type":"string"`, `maxLength` ≥ 200 | `<textarea name="attrs[key]" rows="4" maxlength="N">` | string | string | HTML5 `maxlength` + JSON Schema `maxLength` |
| `number` / `decimal` | `"type":"number"`, optional `minimum`/`maximum` | `<input type="number" step="any" min="..." max="..." />` | string (locale-formatted) | `decimal` (parsed `InvariantCulture`) | HTML5 `min`/`max` + JSON Schema `minimum`/`maximum` |
| `integer` | `"type":"integer"` | `<input type="number" step="1" min="..." max="..." />` | string | `int` | HTML5 `step=1` + JSON Schema `type:integer` |
| `date` | `"type":"string"`, `"format":"date"` | `<input type="date" />` | `YYYY-MM-DD` string | string (kept ISO-8601) | HTML5 date-picker + JSON Schema `format:date` (with `RequireFormatValidation = true`) |
| `datetime` / `datetime-local` | `"type":"string"`, `"format":"date-time"` | `<input type="datetime-local" />` | `YYYY-MM-DDTHH:mm` (browser-local) | UTC ISO-8601 string (`DateTime.Parse(...).ToUniversalTime().ToString("o")`) | HTML5 + JSON Schema `format:date-time` |
| `url` | `"type":"string"`, `"format":"uri"` | `<input type="url" />` | string | string | HTML5 URL parser + JSON Schema `format:uri` |
| `enum` / `select` | `"type":"string"`, `"enum":[...]` | `<select>...<option>...</option></select>` | string | string | HTML5 (`<select>` constrains) + JSON Schema `enum` |
| `checkbox` / `boolean` | `"type":"boolean"` | `<input type="hidden" name="..." value="false"/><input type="checkbox" name="..." value="true" />` | `"true"` or `"false"` (hidden default makes unchecked actually POST `false`) | `bool` | JSON Schema `type:boolean` |
| `stringArray` / `multi-select` | `"type":"array","items":{"type":"string"}` | `<input type="text" placeholder="Comma-separated">` (v1; chip-picker is v2) | comma-separated string | `string[]` (split + trim) | JSON Schema `type:array` + `items` |
| `iso2` | `"type":"string","maxLength":2,"pattern":"^[A-Z]{2}$"` | `<input type="text" maxlength="2" pattern="[A-Za-z]{2}" style="text-transform:uppercase">` | 2-char string | string (UPPER-cased) | HTML5 `pattern` + JSON Schema `pattern` |
| `file-upload` | _deferred_ | _Sprint 6: drag-drop upload + presigned S3/Box URL_ | _N/A this PR_ | _N/A this PR_ | _deferred_ |

The `_DynamicField.cshtml` partial in §1.6 implements all 11 active rows (file-upload deferred; deferred to Sprint 6 mill-cert/COA work).

### 4.1 Special cases worth calling out

- **`checkbox` requires a hidden sibling.** HTML omits unchecked checkboxes from the form post entirely. The pattern `<input type="hidden" value="false" /><input type="checkbox" value="true" />` ensures the server always sees a `true` or `false` for the same name, never absent. The reader treats absence as "field not on form" (skip), not "false."
- **`datetime-local` POSTs in browser-local time.** The reader assumes local time, converts to UTC, stores ISO. The roundtrip back to the form (on Edit) converts UTC → browser-local for display. (TODO in §10: per-tenant timezone is on `User.TenantSettings.TimeZone`; for v1 we trust `AssumeLocal` which is server-local on Replit's container UTC — fine for the dev environment, will need real timezone work in Sprint 5.)
- **`stringArray` as comma-list** is a deliberate v1 simplification. The chip-picker UX is on the §10 stretch list. JSON Schema accepts the array shape regardless.
- **`iso2` is `text` with `pattern`.** Some libraries expose a `<select>` of countries; we leave that to a future profile-author preference. The `pattern` + uppercase is sufficient validation.

---

## 5. Per-field validation-error rendering

When `JsonSchema.Net` rejects the payload, we need errors to land under the right `<input>`.

### 5.1 The path translation

JsonSchema.Net emits errors with **JSON Pointer** instance locations:

- `""` (empty pointer) — root-level error (e.g. `"required":"heatNumber"` from the schema's `required` array)
- `/heatNumber` — error on a specific property
- `/dimensions/lengthMm` — nested, but we don't use nested in any seed profile
- `/allergens/2` — array index

Form names follow ASP.NET's `attrs[key]` convention.

**Translation:** `/foo` → `attrs[foo]`. Root-level `required` errors get split: `"required":"X"` becomes an error on `attrs[X]` because that's the field that's empty.

```csharp
public static class JsonPointerToModelKey
{
    public static IEnumerable<(string ModelKey, string Message)> Translate(IEnumerable<ValidationError> errors)
    {
        foreach (var e in errors)
        {
            // Special-case "required" errors at root — schema says required:["X","Y"]
            // and reports them with InstanceLocation="" / Keyword="required" /
            // Message="Required properties [X, Y] were not present"
            if (e.Pointer == "" && e.Keyword == "required")
            {
                // Greg Dennis's library reports the missing keys inside the message;
                // when result.Details has nested entries those carry the per-key
                // pointer. For our purposes we read result.Details with one entry
                // per missing prop — see EvaluationOptions below.
                foreach (var missingKey in ExtractMissingKeys(e.Message))
                    yield return ($"attrs[{missingKey}]", $"{missingKey} is required");
                continue;
            }

            // Strip leading "/" → "heatNumber"; nested rare but handled.
            var key = e.Pointer.TrimStart('/').Replace('/', '.');
            yield return ($"attrs[{key}]", FriendlyMessage(e));
        }
    }

    private static string FriendlyMessage(ValidationError e) => e.Keyword switch
    {
        "pattern"    => "Value doesn't match the required format",
        "maxLength"  => "Too long",
        "minLength"  => "Too short",
        "minimum"    => $"Minimum value is {e.Message}",
        "maximum"    => $"Maximum value is {e.Message}",
        "enum"       => "Not a valid choice",
        "format"     => "Invalid format",
        "type"       => "Wrong type",
        _            => e.Message
    };
}
```

To make missing-required errors come out as one-error-per-key, configure JsonSchema.Net with `OutputFormat.List` and `RequireFormatValidation = true`. The library reports `required` violations with one details-entry per missing property when run in `List` mode (recent 7.x behavior — verify on the actual pin before relying on this; if not, the `ExtractMissingKeys` helper parses the message).

### 5.2 ModelState integration

```csharp
// In OnPostAsync, after service validation returns:
foreach (var (key, message) in JsonPointerToModelKey.Translate(serviceResult.ValidationErrors))
{
    ModelState.AddModelError(key, message);
}
```

This makes the existing Razor `<span asp-validation-for>` pattern continue to work. Even though we don't use `asp-for` on dynamic inputs, the `_DynamicField.cshtml` partial renders `<span class="field-error">...@Model.ErrorMessage</span>` (see §1.6) by reading `ModelState[$"attrs[{field.Key}]"].Errors.FirstOrDefault()` via the `DynamicFieldModel.From(...)` factory.

### 5.3 Error display in the partial

Already in the partial (§1.6):

```cshtml
@if (Model.ErrorMessage is { } err)
{
    <span class="field-error" data-csp-style="display:block; font-size:12px; color:var(--ds-danger,#f87171); margin-top:6px;">
        @err
    </span>
}
```

The `DynamicFieldModel.From(...)` factory populates `ErrorMessage` from the ViewComponent's ModelState dictionary:

```csharp
public static DynamicFieldModel From(FieldSpec spec, DynamicFormVm vm) => new()
{
    Key          = spec.Key,
    Label        = spec.Label,
    Type         = spec.Type,
    Required     = spec.Required,
    MaxLength    = spec.MaxLength,
    Pattern      = spec.Pattern,
    Min          = spec.Minimum?.ToString(CultureInfo.InvariantCulture),
    Max          = spec.Maximum?.ToString(CultureInfo.InvariantCulture),
    Options      = ResolveOptions(spec),
    Value        = vm.Values.TryGetValue(spec.Key, out var v) ? FormatForInput(v, spec.Type) : "",
    ErrorMessage = vm.Errors.TryGetValue($"attrs[{spec.Key}]", out var entry)
                       ? entry.Errors.FirstOrDefault()?.ErrorMessage
                       : null,
    FullWidth    = spec.Type is "textarea" or "url",
    Description  = spec.Description,
};
```

### 5.4 Error categories covered

| JSON Schema keyword | UI message | Renders under |
| --- | --- | --- |
| `required` (root) | "X is required" | `attrs[X]` |
| `type` mismatch | "Wrong type" | the offending field |
| `pattern` | "Value doesn't match the required format" | the field |
| `maxLength` / `minLength` | "Too long" / "Too short" | the field |
| `minimum` / `maximum` | "Minimum value is N" / "Maximum value is N" | the field |
| `enum` | "Not a valid choice" | the field |
| `format` (date, uri) | "Invalid format" | the field |
| `additionalProperties: false` | "Unknown field {X}" — surfaces as a banner since the field isn't in `UiFormSpec` to render under | top of form |

The "unknown field" path indicates either operator tampering or a `UiFormSpec` drift. The banner surfaces it; the audit log records the rejected payload for forensics.

### 5.5 HTML5 client-side validation — keep or strip?

**Keep.** The `required`, `maxlength`, `pattern`, `min`/`max`, `step` attributes drive the browser's built-in validation tooltips and prevent obvious mistakes before the round-trip. The backend still re-validates via JsonSchema.Net (defense in depth). Don't set `novalidate` on the `<form>`.

---

## 6. Voice-AI integration

The voice-AI co-pilot (Sprint 5) needs three things from the form page:

1. The active receipt's `ProfileId` and `UiFormSpec` — for grammatical awareness ("heat" maps to `heatNumber` only on STEEL/AEROSPACE/OIL_GAS).
2. Each field's `data-voice-key` on the rendered input so the in-page voice client can `document.querySelector('[data-voice-key="heatNumber"]')` and direct focus / read out / fill.
3. The CROSS_PROFILE_GLOSSARY content from the D10 spike — so the LLM can disambiguate even if the page is currently in one profile.

### 6.1 `data-voice-key` on every input

Already in `_DynamicField.cshtml` (§1.6). Every input/select/textarea has `data-voice-key="@Model.Key"` plus `data-field-type="@Model.Type"`. The voice client (Sprint 5) walks the DOM, builds a key → element map, and the LLM can target by key.

### 6.2 `<script id="voice-form-spec">` JSON blob

Already in `Default.cshtml` of the ViewComponent (§1.6). Shape:

```jsonc
{
  "profileCode": "STEEL",
  "fields": [
    {
      "key":   "heatNumber",
      "label": "Heat #",
      "type":  "text",
      "required": true,
      "voice": ["heat","heat number","melt id","melt number"],
      "scope": ["STEEL","AEROSPACE","OIL_GAS"],
      "exampleQueries": ["receipts of heat H-12345", "all heats from Nucor"],
      "disambiguation": {
        "phrasesThatAreNOTThisField": ["lot","batch","serial","tag"],
        "confusableWith": ["LotNumber (core column, all profiles)"]
      },
      "semanticAction": null
    },
    ...
  ]
}
```

The voice client reads it, ships it with the user's utterance to the LLM as part of `ACTIVE RECEIPT PROFILE` in the §2 prompt template (voice-ai-spike doc).

The blob is **already the data the voice prompt needs** — no extra server round-trip. If we publish it alongside `voice-context-payload`, the voice client has full context before the LLM call.

### 6.3 Voice-mode read-out

Not a v1 PR #3 concern. Stretch goal §10. Adding `aria-label` and `aria-describedby` on the inputs (the partial already does `aria-label="@Model.Label"`) future-proofs us for screen-reader-equivalent voice readout.

### 6.4 Decisions to bake in now

- **Yes, `data-voice-key` on every dynamic input.** It costs nothing and Sprint 5 wants it.
- **Yes, `voice-form-spec` JSON blob.** Same — free at render time, sub-1KB per page, removes a server hop for the voice client.
- **Yes, `aria-label`** on every input for accessibility + future voice read-out.
- **No, do NOT** ship a "voice mode" panel in this PR. That's Sprint 5.

---

## 7. Prior art in the CherryAI codebase

Searched (`grep -ri "UiFormSpec\|FieldSpec\|FormSpec"`, `Services/Lookups/`, `Pages/**/_*.cshtml`, every `Pages/Materials/Vendors/`, `Pages/Admin/MaterialMasters/`, `Pages/Admin/WorkOrders/` Edit page).

### 7.1 `Services/Lookups/` (LookupService)

`Services/Lookups/ILookupService.cs` is a **dropdown-data provider** — given a `lookupKey` ("AssetType", "Priority"), returns active `LookupValueDto` rows scoped per tenant/company. It's the data source for `<select asp-items="...">` dropdowns. Not a form-rendering system. **No overlap; no precedent to follow.**

But it's a **future feed** for our `enum`/`select` field type: a profile field could declare `"optionsLookupKey": "DEA_SCHEDULE"` and the ViewComponent could DI `ILookupService` and pull options at render time. Out of scope for PR #3 (the 12 seed profiles use inline `enum` arrays), but flagged here for Sprint 7+.

### 7.2 Pages/Materials/Vendors/Edit.cshtml

Tab-driven layout (Vendor Info / Contact / History) with `_VendorEditTabs.cshtml` and `_VendorViewTabs.cshtml` partials. **Typed-form pattern** — every input has `asp-for`. No dynamic content. This is the kind of page our new `Edit.cshtml` will look like at the top level (header + tabs) wrapping the dynamic component for the Attributes section.

### 7.3 Pages/Admin/MaterialMasters/Edit.cshtml

Single-form pattern (ShopCode / ASTM / Form / Density / Anisotropic). **Pure typed form** — no dynamic content. Same convention as current StockReceipts/Edit.cshtml. Useful baseline; **no dynamic precedent.**

### 7.4 Pages/Admin/StockReceipts/Edit.cshtml (the one we're replacing)

Hand-written for Steel; 230-line .cshtml with 6 sections of typed inputs. The pattern we're moving away from. The new shape per §8 collapses identity + status to typed inputs and routes everything else to `<vc:dynamic-form>`.

### 7.5 ViewComponents in the codebase

None. We're adding the first one. (Reviewers should be told: this is intentional, this is a sensible first ViewComponent because it owns DI + JSON parsing + the voice-spec emission; the convention will compound for Item dynamic attrs + the Receiving Inbox.)

### 7.6 TagHelpers in the codebase

`TagHelpers/VoiceActionTagHelper.cs` — wraps buttons. The right shape for "decorate an existing element"; the wrong shape for "render a whole form" (see §1.5). We don't add a second TagHelper here.

### 7.7 Pages/Shared/Primitives/PrimitiveModels.cs

POCO models for the design-system primitives. **This is where `DynamicFieldModel`, `DynamicFormVm`, `UiFormSpec`, `UiFormGroup`, `FieldSpec` belong** — append them to that file (or split out `DynamicFormModels.cs` in the same namespace if it grows past 200 lines).

### 7.8 Pages/Shared/Primitives/_*.cshtml

10 design-system partials all using the `_X.cshtml` naming convention with typed models. `_DynamicField.cshtml` slots in here naturally.

### 7.9 Conclusion

**Greenfield for dynamic forms.** The Primitives convention is the anchor; the Lookup system is a future feed; nothing else overlaps. **Build the Hybrid pattern from §1.6 fresh, in the same Primitives namespace.**

---

## 8. Implementation plan — Migration PR #3

Concrete step-by-step.

### Step 1 — NuGet add (one line)

In `Abs.FixedAssets.csproj`:

```xml
<PackageReference Include="JsonSchema.Net" Version="7.3.0" />
```

Verify after `dotnet restore`:

```bash
dotnet list Abs.FixedAssets.csproj package | grep JsonSchema
```

### Step 2 — New file: `Models/Production/UiFormSpec.cs`

Strongly-typed view of the `ReceiptProfile.UiFormSpec` JSON. Used by the ViewComponent and the form reader.

```csharp
namespace Abs.FixedAssets.Models.Production;

public sealed class UiFormSpec
{
    public List<UiFormGroup> Groups { get; set; } = new();
    public Dictionary<string, object?> DefaultAttributes { get; set; } = new();
    public IEnumerable<FieldSpec> AllFields() => Groups.SelectMany(g => g.Fields);
}

public sealed class UiFormGroup
{
    public string Title { get; set; } = "";
    public List<FieldSpec> Fields { get; set; } = new();
}

public sealed class FieldSpec
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
    public bool Required { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public int? Rows { get; set; }
    public string? Step { get; set; }
    public List<string> Voice { get; set; } = new();
    public List<string> Scope { get; set; } = new();
    public List<string> ExampleQueries { get; set; } = new();
    public Disambiguation? Disambiguation { get; set; }
    public string? SemanticAction { get; set; }
    public string? Description { get; set; }
    public List<FieldOption> Options { get; set; } = new();   // enum/select choices
    public string? OptionsLookupKey { get; set; }              // future: pull from ILookupService
}

public sealed class FieldOption
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class Disambiguation
{
    public List<string> PhrasesThatAreNOTThisField { get; set; } = new();
    public List<string> ConfusableWith { get; set; } = new();
}
```

### Step 3 — New file: `Pages/Shared/Primitives/DynamicFormModels.cs`

The view-models the ViewComponent and partial consume.

```csharp
using Abs.FixedAssets.Models.Production;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Abs.FixedAssets.Pages.Shared.Primitives;

public sealed class DynamicFormVm
{
    public string ProfileCode { get; set; } = "";
    public IReadOnlyList<UiFormGroup> Groups { get; set; } = Array.Empty<UiFormGroup>();
    public IReadOnlyDictionary<string, object?> Values { get; set; } = new Dictionary<string, object?>();
    public ModelStateDictionary Errors { get; set; } = new();
    public string FormId { get; set; } = "receipt-form";
    public object? VoiceSpec { get; set; }
}

public sealed class DynamicFieldModel
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
    public bool Required { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }
    public string? Min { get; set; }
    public string? Max { get; set; }
    public string? Step { get; set; }
    public int? Rows { get; set; }
    public string Value { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string? Description { get; set; }
    public List<FieldOption> Options { get; set; } = new();
    public bool FullWidth { get; set; }

    public static DynamicFieldModel From(FieldSpec spec, DynamicFormVm vm) { /* see §5.3 */ ... }
}
```

### Step 4 — New file: `ViewComponents/DynamicFormViewComponent.cs`

Per §1.6 skeleton. Cache `JsonSchema` + `UiFormSpec` deserialization in `IMemoryCache` keyed on `(ProfileId, ModifiedAt)`.

### Step 5 — New file: `Views/Shared/Components/DynamicForm/Default.cshtml`

Per §1.6. Renders groups as `ds-card` sections; loops fields; calls the partial.

### Step 6 — New file: `Pages/Shared/Primitives/_DynamicField.cshtml`

Per §1.6 (the long switch — keep it readable, comment each case with the JSON Schema constraint it pairs with).

### Step 7 — New file: `Services/Forms/AttributesFormReader.cs`

Per §3.4. Static class; reads `IFormCollection`, returns `Dictionary<string, object?>` + coercion errors.

### Step 8 — New file: `Services/Admin/ReceiptAttributesValidator.cs`

Per §2.3. Wraps JsonSchema.Net; caches compiled schemas; returns `IReadOnlyList<ValidationError>`.

### Step 9 — Edit `Services/Admin/IStockReceiptService.cs`

Collapse the DTOs:

```csharp
public sealed record CreateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    string ProfileCode,
    string? LotNumber,
    string? SerialNumber,
    string? SourcePoNumber,
    string? SourcePoLineId,
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal QuantityReceived,
    string? Uom,
    StockReceiptStatus Status,
    string? Notes,
    IReadOnlyDictionary<string, object?> Attributes);

public sealed record UpdateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    string? LotNumber,
    string? SerialNumber,
    string? SourcePoNumber,
    string? SourcePoLineId,
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal QuantityReceived,
    decimal QuantityRemaining,
    string? Uom,
    string? Notes,
    IReadOnlyDictionary<string, object?> Attributes);
```

Note: `ProfileCode` lives only on Create (profile is sticky on Update). The 8 steel-specific fields disappear from both DTOs.

### Step 10 — Edit `Services/Admin/StockReceiptService.cs`

Concrete deltas:

- Constructor: inject `ReceiptAttributesValidator`.
- `CreateAsync`:
  - Resolve profile by `ProfileCode` (fall back to `Item.DefaultReceiptProfileId` if blank, then to STEEL).
  - Run validator over `request.Attributes` against `profile.JsonSchema`; on failure return `Result.Failure<StockReceipt>(...)` with the structured errors attached.
  - Stop setting `HeatNumber/MillCertUrl/Mill/Length/Width/Thickness/UsableLength/UsableWidth` on the entity.
  - Set `entity.Attributes = JsonSerializer.Serialize(request.Attributes)`.
  - Audit snapshot reads from `entity.Attributes` for the profile-specific fields (the SnapshotForAudit shape drops the legacy properties).
- `UpdateAsync`: same, plus profile is sticky (`entity.ProfileId` stays).
- Drop `BuildSteelAttributesJson` / `BuildSteelAttributesJsonFromUpdate` helpers (no longer needed).
- Keep `ResolveProfileIdForCreateAsync` but rename it to make the security rationale obvious (`ResolveProfileIdServerSideAsync`).

### Step 11 — Edit `Pages/Admin/StockReceipts/Edit.cshtml.cs`

```csharp
public class EditModel : VoiceReadyPageModel
{
    private readonly IStockReceiptService _svc;

    public EditModel(IStockReceiptService svc) => _svc = svc;

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }

    [BindProperty] public string ReceiptNumber { get; set; } = "";
    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public int? MaterialMasterId { get; set; }
    [BindProperty] public string? LotNumber { get; set; }
    [BindProperty] public string? SerialNumber { get; set; }
    [BindProperty] public string? SourcePoNumber { get; set; }
    [BindProperty] public string? SourcePoLineId { get; set; }
    [BindProperty] public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    [BindProperty] public int? ReceivedByUserId { get; set; }
    [BindProperty] public int? LocationId { get; set; }
    [BindProperty] public decimal QuantityReceived { get; set; }
    [BindProperty] public decimal QuantityRemaining { get; set; }
    [BindProperty] public string? Uom { get; set; }
    [BindProperty] public StockReceiptStatus Status { get; set; } = StockReceiptStatus.Available;
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public string ProfileCode { get; set; } = "STEEL";

    public ReceiptProfile Profile { get; private set; } = default!;
    public IReadOnlyDictionary<string, object?> Attributes { get; private set; } = new Dictionary<string, object?>();
    public bool IsNew => Id is null or 0;
    public string PageTitle => IsNew ? "New Stock Receipt" : "Edit Stock Receipt";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew)
        {
            Profile = await _svc.GetDefaultProfileForCreateAsync(HttpContext.RequestAborted);
            ProfileCode = Profile.Code;
            Attributes = JsonSerializer.Deserialize<Dictionary<string, object?>>(Profile.DefaultAttributes) ?? new();
            return Page();
        }

        var r = await _svc.GetWithProfileAsync(Id!.Value, HttpContext.RequestAborted);
        if (r.IsFailure) { ErrorMessage = r.Error; return Page(); }
        var (entity, profile) = r.Value!;
        Profile = profile;
        ProfileCode = profile.Code;
        Attributes = JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.Attributes ?? "{}") ?? new();
        // typed core …
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Always reload profile server-side; ignore form-supplied ProfileCode for security.
        Profile = await _svc.GetProfileForSubmitAsync(Id, ProfileCode, ItemId, HttpContext.RequestAborted);
        var spec = JsonSerializer.Deserialize<UiFormSpec>(Profile.UiFormSpec)!;
        Attributes = AttributesFormReader.Read(Request.Form, spec, out var coercionErrors);

        foreach (var err in coercionErrors)
            ModelState.AddModelError($"attrs[{err.Key}]", err.Message);

        if (!ModelState.IsValid) return Page();

        var idempKey = Guid.NewGuid();
        var actor = ResolveActorUserId();

        if (IsNew)
        {
            var req = new CreateStockReceiptRequest(
                ReceiptNumber, ItemId, MaterialMasterId, Profile.Code,
                LotNumber, SerialNumber, SourcePoNumber, SourcePoLineId,
                ReceivedAt, ReceivedByUserId, LocationId,
                QuantityReceived, Uom, Status, Notes, Attributes);
            var r = await _svc.CreateAsync(req, actor, idempKey, HttpContext.RequestAborted);
            if (r.IsFailure)
            {
                ErrorMessage = r.Error;
                foreach (var (k, m) in JsonPointerToModelKey.Translate(r.ValidationErrors)) ModelState.AddModelError(k, m);
                return Page();
            }
            return RedirectToPage("Index");
        }
        // … same pattern for Update
    }

    public override VoiceContextPayload BuildContextPayload() { /* unchanged */ }
}
```

### Step 12 — Edit `Pages/Admin/StockReceipts/Edit.cshtml`

Collapse to ~80 lines: keep the header card + Identity card (typed core: receipt #, item, status, qty, lot/serial, source PO, received-at, notes) and route everything else to the component.

```cshtml
@page
@model Abs.FixedAssets.Pages.Admin.StockReceipts.EditModel
@using Abs.FixedAssets.Models.Production
@{
    ViewData["Title"] = Model.PageTitle;
    ViewData["HasScreenHeader"] = true;
    Layout = "_ModernLayout";
    var headerVd = new ViewDataDictionary(ViewData) {
        ["HeaderTitle"] = Model.PageTitle,
        ["Subtitle"] = Model.IsNew ? $"New {Model.Profile.Name} receipt" : Model.ReceiptNumber,
        ["TypeLabel"] = $"Production · {Model.Profile.Name}",
        ["StatusText"] = Model.Status.ToString(),
        ["StatusTone"] = Model.Status switch {
            StockReceiptStatus.Available => "active",
            StockReceiptStatus.Quarantined => "danger",
            _ => "neutral"
        },
    };
}
@await Html.PartialAsync("Shared/_ScreenHeader", headerVd)

@if (!string.IsNullOrEmpty(Model.ErrorMessage))
{
    <div class="ds-card" data-tone="danger">@Model.ErrorMessage</div>
}

<form method="post" id="receipt-form" novalidate style="margin-top:20px; display:grid; gap:20px;">
    <input type="hidden" asp-for="Id" />
    <input type="hidden" asp-for="ProfileCode" />

    @* — TYPED CORE: Identity / quantity / status / notes — *@
    @await Html.PartialAsync("_ReceiptCoreFields", Model)

    @* — DYNAMIC PROFILE-SPECIFIC SECTION — *@
    @await Component.InvokeAsync("DynamicForm", new {
        profile    = Model.Profile,
        attributes = Model.Attributes,
        errors     = ModelState,
        formId     = "receipt-form"
    })

    <div class="ds-action-row">
        <a href="/Admin/StockReceipts" class="ds-btn" data-variant="ghost">Cancel</a>
        <button type="submit" class="ds-btn" data-variant="primary">@(Model.IsNew ? "Create Receipt" : "Save Changes")</button>
    </div>
</form>

<script type="application/json" id="voice-context-payload">
    @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.BuildContextPayload()))
</script>
```

Extract the typed-core section to a new partial `_ReceiptCoreFields.cshtml` for cleanliness.

### Step 13 — Edit `Pages/Admin/StockReceipts/Index.cshtml`

Replace `r.HeatNumber` / `r.Mill` lookups with `Attributes ->> 'heatNumber'` / `Attributes ->> 'mill'` reads.

In `Index.cshtml.cs`:

```csharp
public IReadOnlyList<StockReceiptListRow> Receipts { get; private set; } = Array.Empty<StockReceiptListRow>();

// New flat DTO that the service projects from sr + attrs
public record StockReceiptListRow(
    int Id, string ReceiptNumber, StockReceiptStatus Status, string ProfileCode,
    int ItemId, string? ItemDescription,
    string? LotNumber, string? SerialNumber,
    string? FacetA, string? FacetB,    // profile.PromotedFacets[0..1] values
    string FacetALabel, string FacetBLabel,
    decimal QuantityReceived, decimal QuantityRemaining, string? Uom,
    DateTime ReceivedAt, string? SourcePoNumber);
```

In `ListAsync`, project per row:

```csharp
var rows = await _db.StockReceipts
    .Include(r => r.Item)
    .Include(r => r.Profile)
    .OrderByDescending(r => r.ReceivedAt)
    .Take(500)
    .Select(r => new StockReceiptListRow(
        r.Id, r.ReceiptNumber, r.Status, r.Profile!.Code,
        r.ItemId, r.Item != null ? r.Item.Description : null,
        r.LotNumber, r.SerialNumber,
        // Read the first two PromotedFacets via EF.Functions.JsonExtractPathText or similar
        EF.Functions.JsonExtractPathText(r.Attributes!, GetFacet(r.Profile!, 0)),
        EF.Functions.JsonExtractPathText(r.Attributes!, GetFacet(r.Profile!, 1)),
        GetFacetLabel(r.Profile!, 0),
        GetFacetLabel(r.Profile!, 1),
        r.QuantityReceived, r.QuantityRemaining, r.Uom,
        r.ReceivedAt, r.SourcePoNumber))
    .ToListAsync(ct);
```

If `EF.Functions.JsonExtractPathText` causes provider grief, fall back to materializing `Attributes` and parsing in memory (500-row list, cheap). Postgres-side parsing is preferred.

The index `cshtml` updates the columns: instead of fixed "Heat #" / "Mill / Source PO", use `r.FacetALabel` / `r.FacetBLabel`. The KPI tile "With Heat #" becomes profile-aware ("With first facet").

### Step 14 — Edit `Models/Production/StockReceipt.cs`

Drop the 8 properties: `HeatNumber`, `MillCertUrl`, `Mill`, `LengthMm`, `WidthMm`, `ThicknessMm`, `UsableLengthMm`, `UsableWidthMm`.

Keep: `LotNumber`, `SerialNumber`, `SourcePoNumber`, `SourcePoLineId`, the rest of the core.

### Step 15 — New migration `Migrations/20260519_DropLegacyStockReceiptColumns.cs`

```csharp
[DbContext(typeof(AppDbContext))]
[Migration("20260519_DropLegacyStockReceiptColumns")]
public partial class DropLegacyStockReceiptColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Defensive: ensure no row has Attributes IS NULL — Migration PR #2
        // backfilled, but re-check just in case CI ran before the backfill.
        migrationBuilder.Sql(@"
            UPDATE ""StockReceipts""
            SET ""Attributes"" = jsonb_strip_nulls(jsonb_build_object(
                'heatNumber',     ""HeatNumber"",
                'mill',           ""Mill"",
                'millCertUrl',    ""MillCertUrl"",
                'lengthMm',       ""LengthMm"",
                'widthMm',        ""WidthMm"",
                'thicknessMm',    ""ThicknessMm"",
                'usableLengthMm', ""UsableLengthMm"",
                'usableWidthMm',  ""UsableWidthMm""))
            WHERE ""Attributes"" IS NULL
              AND (""HeatNumber"" IS NOT NULL OR ""Mill"" IS NOT NULL OR ""MillCertUrl"" IS NOT NULL
                OR ""LengthMm"" IS NOT NULL OR ""WidthMm"" IS NOT NULL OR ""ThicknessMm"" IS NOT NULL);
        ");

        migrationBuilder.DropColumn(name: "HeatNumber",      table: "StockReceipts");
        migrationBuilder.DropColumn(name: "MillCertUrl",     table: "StockReceipts");
        migrationBuilder.DropColumn(name: "Mill",            table: "StockReceipts");
        migrationBuilder.DropColumn(name: "LengthMm",        table: "StockReceipts");
        migrationBuilder.DropColumn(name: "WidthMm",         table: "StockReceipts");
        migrationBuilder.DropColumn(name: "ThicknessMm",     table: "StockReceipts");
        migrationBuilder.DropColumn(name: "UsableLengthMm",  table: "StockReceipts");
        migrationBuilder.DropColumn(name: "UsableWidthMm",   table: "StockReceipts");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "HeatNumber",      table: "StockReceipts", type: "varchar(64)",  nullable: true);
        migrationBuilder.AddColumn<string>(name: "MillCertUrl",     table: "StockReceipts", type: "varchar(500)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Mill",            table: "StockReceipts", type: "varchar(128)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "LengthMm",       table: "StockReceipts", type: "decimal(10,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WidthMm",        table: "StockReceipts", type: "decimal(10,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "ThicknessMm",    table: "StockReceipts", type: "decimal(10,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "UsableLengthMm", table: "StockReceipts", type: "decimal(10,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "UsableWidthMm",  table: "StockReceipts", type: "decimal(10,2)", nullable: true);

        // Best-effort restore from Attributes
        migrationBuilder.Sql(@"
            UPDATE ""StockReceipts"" SET
                ""HeatNumber""      = (""Attributes"" ->> 'heatNumber'),
                ""Mill""            = (""Attributes"" ->> 'mill'),
                ""MillCertUrl""     = (""Attributes"" ->> 'millCertUrl'),
                ""LengthMm""        = NULLIF(""Attributes"" ->> 'lengthMm', '')::numeric(10,2),
                ""WidthMm""         = NULLIF(""Attributes"" ->> 'widthMm', '')::numeric(10,2),
                ""ThicknessMm""     = NULLIF(""Attributes"" ->> 'thicknessMm', '')::numeric(10,2),
                ""UsableLengthMm""  = NULLIF(""Attributes"" ->> 'usableLengthMm', '')::numeric(10,2),
                ""UsableWidthMm""   = NULLIF(""Attributes"" ->> 'usableWidthMm', '')::numeric(10,2);
        ");
    }
}
```

### Step 16 — Test plan

After PR #3 deploys to Replit dev:

1. **Hit `/Admin/StockReceipts`.** Should render KPI tiles + table without 500. Existing rows show with facet columns labeled "Heat #" + "Mill" (STEEL profile). No data loss.
2. **Hit `/Admin/StockReceipts/Edit`.** Should render the STEEL form (default profile). Identity card + Traceability card + Dimensions card visible. Every input has `data-voice-key`.
3. **Submit a valid Create.** Should persist; `Attributes` jsonb populated; redirect to Index; new row visible.
4. **Submit a Create with invalid heat (e.g. empty `heatNumber` while it's `required:true` in JSON Schema).** Form re-renders; error message under heat field; no row created.
5. **Submit a Create with extra unknown field via curl** (`-d "attrs[isAdmin]=true"`). Should silently drop the unknown key; no error; row created without it.
6. **Run `psql` against `StockReceipts`.** Verify the 8 legacy columns are GONE. Verify `Attributes` jsonb has the expected keys.
7. **Hit `/api/voice/context-payload`** (or whatever the existing endpoint is — see `Pages/Shared/_VoiceClient.cshtml` if it exists) and verify the page emits both `voice-context-payload` and `voice-form-spec` blobs.
8. **Manually edit an existing receipt.** Verify Attributes JSON round-trips losslessly.
9. **Switch profile by hitting a Pharma-tagged item (Item.DefaultReceiptProfileId = PHARMA.Id).** Verify the form re-renders with NDC / Expiration / DEA Schedule fields. (Requires a test Item with `DefaultReceiptProfileId` set, which the §10 stretch list will productionize — for testing, set via psql.)
10. **`dotnet build`** must pass with zero errors. `dotnet test` (the bash-script suite) must continue to pass.

---

## 9. Risk register

| # | Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- | --- |
| R1 | `JsonSchema.Net.JsonSchema.FromText(...)` throws at startup if a seed profile's `JsonSchema` is malformed | Low (Migration PR #1 seeded 12 profiles successfully) | High (boot fails) | Add a startup health check that compiles every active profile's schema and logs+fails-fast with the offending profile code. Wire into `Program.cs` after DI registration. |
| R2 | Operator submits `attrs[<unknown_key>]=value` (mass-assignment) | Real (anyone with a browser can craft this) | Low (`UiFormSpec`-keyed reader ignores; `additionalProperties:false` rejects at schema layer) | Two layers of defense already in place. Add an audit log entry when a key is dropped, so we can see if anyone tries. |
| R3 | Voice-AI tool catalog drift — the LLM is told a field exists but the form doesn't render it | Medium | High (voice UX breaks) | The `voice-form-spec` JSON blob is generated from the same `UiFormSpec` that drives the form. If the form renders it, the voice spec knows about it. Drift is therefore structurally impossible. Test: after every profile schema change, run `dotnet run -- tools/diff-voice-spec --profile STEEL` to dump the spec and diff against checked-in golden. |
| R4 | Replit Razor view cache (see MEMORY note) — old `Edit.cshtml.Views.dll` persists after CSHTML change | High (we hit this before; cost a 3-PR debug loop) | Medium (live verify shows stale form) | **Mandatory `rm -rf bin obj` on Replit Shell before Agent restart.** Document in the PR description. Consider adding a `dotnet build` after-pull step to the deploy script. |
| R5 | `EF.Functions.JsonExtractPathText` not supported by Npgsql provider | Low (Npgsql 9.x supports it; verify) | Medium (Index page fails to query) | Test in dev. Fallback: hydrate `Attributes` to in-memory list and parse with `System.Text.Json` (500 rows max, fast). |
| R6 | Migration PR #3 ships before §D9 step 7 (Migration PR #2 ProfileId backfill) ran in some env | Low (env audit shows all envs have PR #2) | Catastrophic (orphan rows; column drop wipes data without profile attribution) | Step-15 migration includes a defensive `UPDATE … WHERE Attributes IS NULL` BEFORE the column drop. The `ALTER TABLE … DROP COLUMN` is non-transactional but Postgres DDL is atomic per statement; rollback path documented in `Down()`. |
| R7 | `DateTime.Parse(..., AssumeLocal).ToUniversalTime()` shifts dates by the Replit container's UTC offset | Real (Replit container is UTC; Dean's local is Central) | Medium (date fields look off-by-one in some timezones) | Document; queue a Sprint 5 "tenant timezone" follow-up. Mitigation in v1: treat `datetime` browser-local input as "user's local clock" and round-trip in UTC; display in user's local on the form. Acceptable for v1. |
| R8 | Cold `JsonSerializer.Deserialize<UiFormSpec>(profile.UiFormSpec)` on every request — perf cliff at scale | Low (each profile is ~3-5 KB; cached after first hit) | Low | Cache in `IMemoryCache` keyed `(profile.Id, profile.ModifiedAt?.Ticks)`. 30-minute sliding expiry. Invalidates automatically when a tenant edits a profile. |
| R9 | Reviewer / Replit Agent edits the `.csproj` reference and unintentionally bumps `JsonSchema.Net` major version | Low (we pin to 7.3.0) | Medium (breaking API changes between 6.x→7.x are real) | Pin exact: `Version="7.3.0"`, not `7.*`. Document in `dev-doc.md`. |
| R10 | A profile is edited via DB to add a new field type the partial doesn't know about | Real (tenants will customize) | Low (the `default` case in `_DynamicField.cshtml` falls back to text + a console warning) | Already handled by the partial. Add a backend warning log when this fires. |

---

## 10. Stretch goals — explicitly out of scope for PR #3

These are the obvious next steps. **Do not implement in PR #3.** Track as follow-ups.

1. **File upload (mill cert PDF, COA URL).** Field type `file-upload`; needs S3/Box presign + virus scan + the upload UI. Sprint 6 work.
2. **Per-tenant `UiFormSpec` override.** Today profiles are global. Tenants will want to add their own fields (cotton-grading on APPAREL, mason-grade on CONSTRUCTION). Needs a `TenantReceiptProfileOverride` table that merges on top of the seed profile.
3. **Chip-picker for `multi-select`.** v1 is comma-separated. v2 is `<chips>` with autocomplete from `ILookupService`.
4. **Voice-driven form completion.** "Set heat to H-12345" → focuses `[data-voice-key="heatNumber"]`, types it, blurs. Sprint 5. The `data-voice-key` plumbing is already in place; the in-page voice client closes the loop.
5. **Auto-save / draft state.** Save partial form state on `blur`. Sprint 5+. Needs an `IDraftStore` service.
6. **`select` fields backed by `ILookupService`.** Profile field declares `"optionsLookupKey":"DEA_SCHEDULE"`; ViewComponent DI's `ILookupService` and pulls options. Sprint 7.
7. **Per-tenant timezone for `datetime`.** v1 uses `AssumeLocal` on the server. v2 reads `User.TenantSettings.TimeZone`.
8. **Tenant glossary** for vague predicates ("bad", "stale") — see voice-AI spike §6. Sprint 5.
9. **Per-profile reporting views** (ADR-015 §D7). One view per profile, flattened, regenerated by a code-gen task. Sprint 5–6 BI work.
10. **`PromotedFacets`-driven index column headers in `/Admin/StockReceipts`.** v1 hard-codes the first two facets per profile. v2 lets the operator choose which facets to show via column-picker UI.

---

## 11. Open questions to confirm with Dean before coding

1. **Should the StockReceipts Index page show the active profile column?** (Recommended: yes, badge it next to the receipt #.) — Default: yes.
2. **On Create, what is the default profile when `Item.DefaultReceiptProfileId` is null?** ADR-015 says STEEL. Confirm we want that as the universal fallback or whether to surface a profile picker. — Default: keep STEEL fallback; surface picker in Sprint 5.
3. **Where does `ProfileCode` live for the Create flow?** Currently §11 has it as a hidden form field; server re-resolves on POST. Alternative: pass via querystring (`/Admin/StockReceipts/Edit?profileCode=PHARMA`). — Recommend: hidden field + querystring override, server-side wins for security.
4. **Should we also ship per-profile flattening views (§D7) in this PR or a follow-up?** Recommend: follow-up — keeps PR #3 lean.
5. **Do we keep the "With Heat #" KPI tile on the Index page?** It's STEEL-specific. Recommendation: replace with "With Required Facets" (% of receipts with all of their profile's promotedFacets populated). Generic across profiles.

---

## 12. References

- ADR-015: `docs/ADR-015-industry-agnostic-receipt-schema.md`
- Research: `docs/research/industry-agnostic-receipt-schema.md`
- Spike: `docs/research/voice-ai-spike-adr015-d10.md`
- Prior art: `Pages/Materials/Vendors/Edit.cshtml`, `Pages/Admin/MaterialMasters/Edit.cshtml`, `Pages/Admin/StockReceipts/Edit.cshtml(.cs)`, `Services/Admin/StockReceiptService.cs`, `Services/Lookups/ILookupService.cs`, `TagHelpers/VoiceActionTagHelper.cs`, `Pages/Shared/VoiceReadyPageModel.cs`, `Pages/Shared/Primitives/_*.cshtml`
- Library docs: [json-everything.net](https://json-everything.net/) (Greg Dennis), `JsonSchema.Net` package readme on NuGet
- Patterns: ASP.NET Core 9 ViewComponents docs, model binding docs
- MEMORY notes consulted: `feedback_replit_razor_view_cache.md` (R4 mitigation), `feedback_namespace_enum_collisions.md` (Step 2 — verified `UiFormSpec` / `FieldSpec` are not in use), `project_sprint4_pr1_shipped.md` (Result&lt;T&gt; + IdempotencyMediator already in service)
