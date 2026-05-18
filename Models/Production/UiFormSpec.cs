using System.Collections.Generic;
using System.Linq;

namespace Abs.FixedAssets.Models.Production;

// ADR-015 Migration PR #3 — Strongly-typed view of the
// ReceiptProfile.UiFormSpec JSON payload.
//
// Used by:
//   - DynamicFormViewComponent to drive the per-profile form render
//   - AttributesFormReader to coerce form POST values per field type
//   - The voice-AI Sprint 5 layer (via BuildVoiceSpec) to ground the
//     LLM in the active profile's field grammar
//
// Wire shape (camelCase JSON in the ReceiptProfile.UiFormSpec column):
//   {
//     "groups": [
//       { "title": "Traceability",
//         "fields": [
//           { "key": "heatNumber", "label": "Heat #", "type": "text",
//             "required": true, "maxLength": 64,
//             "voice": ["heat","heat number"],
//             "scope": ["STEEL","AEROSPACE"],
//             ... } ] } ],
//     "defaultAttributes": { ... }
//   }
//
// See: docs/research/dynamic-razor-form-spec.md §8.2
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

    // enum/select choices (inline)
    public List<FieldOption> Options { get; set; } = new();

    // Future: pull options from ILookupService at render time (Sprint 7+)
    public string? OptionsLookupKey { get; set; }
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
