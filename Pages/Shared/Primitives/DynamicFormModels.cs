// ADR-015 Migration PR #3 — view-models for the dynamic-form ViewComponent + partial.
//
// DynamicFormVm  — orchestrator view-model produced by DynamicFormViewComponent
// DynamicFieldModel — per-field view-model consumed by _DynamicField.cshtml
//
// See: docs/research/dynamic-razor-form-spec.md §8.3 + §5.3 (the From() factory)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
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

    // Per spec §5.3 — factory that hydrates a per-field model from the
    // FieldSpec + the orchestrator VM. Pulls value (formatted for input),
    // error (looked up by attrs[key] modelstate key), and presentational
    // hints (fullWidth for textarea/url).
    public static DynamicFieldModel From(FieldSpec spec, DynamicFormVm vm)
    {
        string? errorMsg = null;
        if (vm.Errors.TryGetValue($"attrs[{spec.Key}]", out var entry) && entry?.Errors is { Count: > 0 } errs)
        {
            errorMsg = errs[0].ErrorMessage;
        }

        var rawValue = vm.Values.TryGetValue(spec.Key, out var v) ? v : null;
        var stringValue = FormatForInput(rawValue, spec.Type);

        return new DynamicFieldModel
        {
            Key          = spec.Key,
            Label        = spec.Label,
            Type         = spec.Type,
            Required     = spec.Required,
            MaxLength    = spec.MaxLength,
            Pattern      = spec.Pattern,
            Min          = spec.Minimum?.ToString(CultureInfo.InvariantCulture),
            Max          = spec.Maximum?.ToString(CultureInfo.InvariantCulture),
            Step         = spec.Step,
            Rows         = spec.Rows,
            Options      = ResolveOptions(spec),
            Value        = stringValue,
            ErrorMessage = errorMsg,
            FullWidth    = spec.Type is "textarea" or "url",
            Description  = spec.Description,
        };
    }

    private static List<FieldOption> ResolveOptions(FieldSpec spec)
    {
        // v1: inline options only. Sprint 7+: pull from ILookupService when
        // spec.OptionsLookupKey is set.
        return spec.Options ?? new List<FieldOption>();
    }

    private static string FormatForInput(object? raw, string type)
    {
        if (raw is null) return "";

        // System.Text.Json elements come in as JsonElement when round-
        // tripped through Dictionary<string, object?>. Unwrap them.
        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? "",
                JsonValueKind.Number => je.GetRawText(),
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                JsonValueKind.Null   => "",
                JsonValueKind.Array  => string.Join(",", EnumerateArray(je)),
                _                    => je.GetRawText(),
            };
        }

        return type switch
        {
            // datetime-local needs the "yyyy-MM-ddTHH:mm" format the
            // browser input expects. We store UTC ISO; round-trip back
            // to local for display.
            "datetime" or "datetime-local" when raw is DateTime dt =>
                dt.ToLocalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),

            "date" when raw is DateTime dt2 =>
                dt2.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

            "stringArray" or "multi-select" when raw is IEnumerable<object?> arr =>
                string.Join(",", arr),

            _ => Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "",
        };
    }

    private static IEnumerable<string> EnumerateArray(JsonElement arr)
    {
        foreach (var el in arr.EnumerateArray())
        {
            yield return el.ValueKind == JsonValueKind.String
                ? el.GetString() ?? ""
                : el.GetRawText();
        }
    }
}
