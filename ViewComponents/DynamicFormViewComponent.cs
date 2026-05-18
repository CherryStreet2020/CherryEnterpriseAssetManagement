// ADR-015 Migration PR #3 — Dynamic form ViewComponent.
//
// First ViewComponent in the codebase. Pattern is intentional — see
// docs/research/dynamic-razor-form-spec.md §1.4 + §1.6 for the rationale.
//
// Orchestrates:
//   - Deserialize ReceiptProfile.UiFormSpec JSON (cached per ProfileId/ModifiedAt)
//   - Build the DynamicFormVm consumed by Default.cshtml
//   - Emit the voice-form-spec JSON blob alongside voice-context-payload
//
// Call site (from any Razor page):
//   @await Component.InvokeAsync("DynamicForm", new {
//       profile    = Model.Profile,
//       attributes = Model.Attributes,
//       errors     = ModelState,
//       formId     = "receipt-form" })

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared.Primitives;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Caching.Memory;

namespace Abs.FixedAssets.ViewComponents;

public class DynamicFormViewComponent : ViewComponent
{
    private readonly IMemoryCache _cache;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling  = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    public DynamicFormViewComponent(IMemoryCache cache)
    {
        _cache = cache;
    }

    public IViewComponentResult Invoke(
        ReceiptProfile profile,
        IReadOnlyDictionary<string, object?> attributes,
        ModelStateDictionary errors,
        string formId)
    {
        var spec = GetCachedSpec(profile);

        var vm = new DynamicFormVm
        {
            ProfileCode = profile.Code,
            Groups      = spec.Groups,
            Values      = attributes ?? new Dictionary<string, object?>(),
            Errors      = errors ?? new ModelStateDictionary(),
            FormId      = string.IsNullOrEmpty(formId) ? "receipt-form" : formId,
            VoiceSpec   = BuildVoiceSpec(profile, spec),
        };

        return View(vm);
    }

    // Spec §8.4 — cache deserialized UiFormSpec per (ProfileId, ModifiedAt).
    // 30-min sliding expiry; ModifiedAt change invalidates automatically.
    private UiFormSpec GetCachedSpec(ReceiptProfile profile)
    {
        var key = $"uiformspec:{profile.Id}:{profile.ModifiedAt?.Ticks ?? 0}";
        return _cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(30);
            return JsonSerializer.Deserialize<UiFormSpec>(profile.UiFormSpec, JsonOpts)
                   ?? throw new InvalidOperationException(
                       $"ReceiptProfile '{profile.Code}' has invalid UiFormSpec JSON");
        })!;
    }

    // Spec §6 — extract the voice metadata for the LLM grounding blob
    // emitted as <script id="voice-form-spec"> alongside voice-context-payload.
    // Strictly additive — voice-AI Sprint 5 consumes it; no runtime impact.
    private static object BuildVoiceSpec(ReceiptProfile profile, UiFormSpec spec)
    {
        var fields = spec.AllFields().Select(f => new
        {
            key            = f.Key,
            label          = f.Label,
            type           = f.Type,
            required       = f.Required,
            voice          = f.Voice,
            scope          = f.Scope,
            exampleQueries = f.ExampleQueries,
            disambiguation = f.Disambiguation,
            semanticAction = f.SemanticAction,
        }).ToList();

        return new
        {
            profileCode = profile.Code,
            profileName = profile.Name,
            fields,
        };
    }
}
