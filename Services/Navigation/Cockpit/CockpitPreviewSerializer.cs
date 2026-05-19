using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Abs.FixedAssets.Services.Navigation.Cockpit;

// ADR-018 §D6 — emit the page's JSON preview blob safely.
//
// The legacy Pages/Receiving/Index.cshtml builds the blob inline via an
// anonymous-object Select + JsonSerializer.Serialize. That worked for one
// page but doesn't generalize across Control Centers and accidentally
// emits any field the developer happens to add. CockpitPreviewSerializer
// inverts the default: ONLY [CockpitPreviewVisible] properties are
// emitted, and nothing else. New fields are opt-in.
//
// Output:
//   <script id="__poDetails" type="application/json">[…]</script>
// is the convention used by wwwroot/js/cockpit.js, so the default
// Serialize(...) emits a JSON array. Pages may pick their own script tag id
// (e.g. "__asnDetails" for the ASN queue tab) and pass it to the helper.
//
// This class is stateless. Reflection caching is keyed off the runtime
// item type, so repeated calls for the same TPreview type are amortized.
public static class CockpitPreviewSerializer
{
    private static readonly Dictionary<Type, IReadOnlyList<MemberPlan>> _planCache = new();
    private static readonly object _planCacheLock = new();

    private sealed record MemberPlan(PropertyInfo Prop, string JsonName);

    private static IReadOnlyList<MemberPlan> GetPlan(Type t)
    {
        lock (_planCacheLock)
        {
            if (_planCache.TryGetValue(t, out var cached))
            {
                return cached;
            }

            var plan = t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new
                {
                    Prop = p,
                    Attr = p.GetCustomAttribute<CockpitPreviewVisibleAttribute>(inherit: true)
                })
                .Where(x => x.Attr != null)
                .Select(x => new MemberPlan(x.Prop, x.Attr!.Name ?? CamelCase(x.Prop.Name)))
                .ToList();

            _planCache[t] = plan;
            return plan;
        }
    }

    // Convert "RequiredDate" → "requiredDate". Used when the
    // [CockpitPreviewVisible] attribute did not override the name.
    private static string CamelCase(string s)
    {
        if (string.IsNullOrEmpty(s) || char.IsLower(s[0]))
        {
            return s;
        }
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    // Serialize a single preview item to a JSON object containing only its
    // [CockpitPreviewVisible] properties.
    public static string Serialize<TPreview>(TPreview item) where TPreview : class
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        var plan = GetPlan(typeof(TPreview));
        var dict = new Dictionary<string, object?>(plan.Count);
        foreach (var m in plan)
        {
            dict[m.JsonName] = m.Prop.GetValue(item);
        }
        return JsonSerializer.Serialize(dict);
    }

    // Serialize a collection of preview items to a JSON array. This is the
    // shape `wwwroot/js/cockpit.js` expects.
    public static string SerializeMany<TPreview>(IEnumerable<TPreview> items) where TPreview : class
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        var plan = GetPlan(typeof(TPreview));
        var list = new List<Dictionary<string, object?>>();
        foreach (var item in items)
        {
            if (item == null) continue;
            var dict = new Dictionary<string, object?>(plan.Count);
            foreach (var m in plan)
            {
                dict[m.JsonName] = m.Prop.GetValue(item);
            }
            list.Add(dict);
        }
        return JsonSerializer.Serialize(list);
    }

    // For tests — clear the reflection cache between runs.
    internal static void ClearCacheForTests()
    {
        lock (_planCacheLock)
        {
            _planCache.Clear();
        }
    }
}
