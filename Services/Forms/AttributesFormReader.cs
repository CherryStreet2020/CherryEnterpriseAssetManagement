// ADR-015 Migration PR #3 — Hand-rolled form reader (Strategy D).
//
// Walks UiFormSpec.AllFields() and pulls `attrs[<key>]` values from
// Request.Form, coercing each to the CLR type the field declares.
// Unknown form keys are silently dropped — mass-assignment defense.
//
// Returns a Dictionary<string, object?> ready for:
//   1. JSON Schema validation via ReceiptAttributesValidator
//   2. JsonSerializer.Serialize -> stored in StockReceipt.Attributes jsonb
//
// See: docs/research/dynamic-razor-form-spec.md §3.4

using System;
using System.Collections.Generic;
using System.Globalization;
using Abs.FixedAssets.Models.Production;
using Microsoft.AspNetCore.Http;

namespace Abs.FixedAssets.Services.Forms;

public static class AttributesFormReader
{
    public static Dictionary<string, object?> Read(
        IFormCollection form,
        UiFormSpec spec,
        out List<CoercionError> errors)
    {
        errors = new List<CoercionError>();
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var field in spec.AllFields())
        {
            var formKey = $"attrs[{field.Key}]";
            if (!form.TryGetValue(formKey, out var raw)) continue;

            // For checkbox: the hidden=false + checkbox=true pattern means
            // both values arrive. Take the LAST one — true wins if checked,
            // false if not.
            var s = field.Type is "checkbox" or "boolean"
                ? (raw.Count > 0 ? raw[raw.Count - 1] ?? "" : "")
                : raw.ToString();

            if (string.IsNullOrEmpty(s) && field.Type is not ("checkbox" or "boolean"))
                continue;

            try
            {
                dict[field.Key] = field.Type switch
                {
                    "number" or "decimal" => decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture),
                    "integer"             => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    "date"                => s,
                    "datetime" or "datetime-local" =>
                        DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
                                .ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                    "boolean" or "checkbox" =>
                        s.Equals("true", StringComparison.OrdinalIgnoreCase),
                    "stringArray" or "multi-select" =>
                        s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                    "iso2" => s.ToUpperInvariant(),
                    _ => (object?)s,  // text, url, enum, select
                };
            }
            catch (FormatException fe)
            {
                errors.Add(new CoercionError(field.Key,
                    $"'{s}' is not a valid {field.Type}: {fe.Message}"));
            }
            catch (OverflowException oe)
            {
                errors.Add(new CoercionError(field.Key,
                    $"'{s}' is out of range for {field.Type}: {oe.Message}"));
            }
        }

        return dict;
    }
}

public sealed record CoercionError(string Key, string Message);
