// ADR-015 Migration PR #3 — JSON-Schema-driven Attributes validator.
//
// Wraps JsonSchema.Net 7.x. Caches compiled JsonSchema instances per
// (ProfileId, ModifiedAt) so we pay the parse cost once. Returns flat
// ValidationError list with JSON Pointer paths consumable by
// JsonPointerToModelKey for ModelState integration.
//
// See: docs/research/dynamic-razor-form-spec.md §2.3 + §5

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Abs.FixedAssets.Models.Production;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;

namespace Abs.FixedAssets.Services.Admin;

public sealed class ReceiptAttributesValidator
{
    private readonly IMemoryCache _cache;

    public ReceiptAttributesValidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public IReadOnlyList<ValidationError> Validate(
        ReceiptProfile profile,
        IReadOnlyDictionary<string, object?> attrs)
    {
        var schema = _cache.GetOrCreate(
            $"receipt-schema:{profile.Id}:{profile.ModifiedAt?.Ticks ?? 0}",
            entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                return JsonSchema.FromText(profile.JsonSchema);
            })!;

        // Dictionary -> JsonNode tree. System.Text.Json handles primitives,
        // arrays, and nested dictionaries; we don't use nested in any v1
        // seed profile but the conversion is transparent if we ever do.
        var node = JsonSerializer.SerializeToNode(attrs);

        var result = schema.Evaluate(node, new EvaluationOptions
        {
            OutputFormat              = OutputFormat.List,
            RequireFormatValidation   = true,
        });

        if (result.IsValid) return Array.Empty<ValidationError>();

        var errors = new List<ValidationError>();
        FlattenErrors(result, errors);
        return errors;
    }

    private static void FlattenErrors(EvaluationResults res, List<ValidationError> sink)
    {
        if (res.HasErrors && res.Errors is { } errs)
        {
            var pointer = res.InstanceLocation.ToString();
            foreach (var kvp in errs)
            {
                sink.Add(new ValidationError(
                    Pointer: pointer,
                    Keyword: kvp.Key,
                    Message: kvp.Value));
            }
        }
        if (res.Details is { Count: > 0 })
        {
            foreach (var d in res.Details)
            {
                FlattenErrors(d, sink);
            }
        }
    }
}

public sealed record ValidationError(string Pointer, string Keyword, string Message);

// ---- JSON Pointer -> ModelState key translator -----------------------------
//
// JsonSchema.Net emits errors with JSON Pointer paths like "/heatNumber".
// Razor's ModelState keys for our dynamic inputs are "attrs[heatNumber]".
// This static helper bridges the two and produces friendly messages.
//
// Spec §5

public static class JsonPointerToModelKey
{
    public static IEnumerable<(string ModelKey, string Message)> Translate(IEnumerable<ValidationError> errors)
    {
        foreach (var e in errors)
        {
            // Root-level "required" errors carry the missing key inside
            // the message. Pull them out so we can render the error under
            // the actually-empty input.
            if (e.Pointer == "" && e.Keyword == "required")
            {
                foreach (var missingKey in ExtractMissingKeys(e.Message))
                {
                    yield return ($"attrs[{missingKey}]", $"{missingKey} is required");
                }
                continue;
            }

            // "/foo"        -> "attrs[foo]"
            // "/foo/bar"    -> "attrs[foo.bar]" (rare; we don't use nested today)
            var trimmed = e.Pointer.TrimStart('/');
            var dottedKey = trimmed.Replace('/', '.');
            var modelKey = string.IsNullOrEmpty(dottedKey) ? "attrs" : $"attrs[{dottedKey}]";

            yield return (modelKey, FriendlyMessage(e));
        }
    }

    private static IEnumerable<string> ExtractMissingKeys(string message)
    {
        // JsonSchema.Net 7.x message format varies. Common patterns:
        //   "Required properties [foo, bar] are not present"
        //   "Required property 'foo' was not found"
        //   "Required properties were not present: foo, bar"
        // We extract bracketed contents OR everything after the last ':'.
        if (string.IsNullOrEmpty(message)) yield break;

        var open = message.IndexOf('[');
        var close = message.IndexOf(']');
        if (open >= 0 && close > open)
        {
            var inner = message.Substring(open + 1, close - open - 1);
            foreach (var part in inner.Split(','))
            {
                var key = part.Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(key)) yield return key;
            }
            yield break;
        }

        // Fallback: try single-quoted name
        var firstQuote = message.IndexOf('\'');
        if (firstQuote >= 0)
        {
            var lastQuote = message.LastIndexOf('\'');
            if (lastQuote > firstQuote)
            {
                yield return message.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                yield break;
            }
        }

        // Last resort — emit the raw message as a key so something surfaces.
        yield return message;
    }

    private static string FriendlyMessage(ValidationError e) => e.Keyword switch
    {
        "pattern"   => "Value doesn't match the required format",
        "maxLength" => "Too long",
        "minLength" => "Too short",
        "minimum"   => $"Minimum value is {e.Message}",
        "maximum"   => $"Maximum value is {e.Message}",
        "enum"      => "Not a valid choice",
        "format"    => "Invalid format",
        "type"      => "Wrong type",
        _           => e.Message,
    };
}
