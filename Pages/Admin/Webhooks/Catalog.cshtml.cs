using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Admin.Webhooks;

/// <summary>
/// Auto-generated event catalog page — Phase 3 of typed-outbox-payloads.
/// Renders <see cref="DomainEventRegistry"/> as a partner-facing reference.
/// Anyone integrating with our webhooks can hit this page to see the
/// authoritative list of events we emit and the exact property shape of
/// each payload.
/// </summary>
[Authorize(Roles = "Admin")]
public class CatalogModel : PageModel
{
    private readonly DomainEventRegistry _registry;

    public CatalogModel(DomainEventRegistry registry)
    {
        _registry = registry;
    }

    public List<EventDescriptor> Events { get; private set; } = new();

    public void OnGet()
    {
        Events = _registry.All()
            .Select(e => Describe(e.EventType, e.Version, e.ClrType))
            .ToList();
    }

    private static EventDescriptor Describe(string eventType, int version, Type clrType)
    {
        // Walk the primary constructor's parameters — for a record, this
        // matches the public property set in declaration order, which is
        // the order partners want for documentation. Skip IDomainEvent
        // members which are derived/constant on each record.
        var ctor = clrType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        var ifaceProps = new HashSet<string>(StringComparer.Ordinal)
            { nameof(IDomainEvent.EventType), nameof(IDomainEvent.Version) };

        var props = ctor?.GetParameters()
            .Where(p => !ifaceProps.Contains(p.Name ?? ""))
            .Select(p => new PropertyDescriptor(
                CamelCase(p.Name ?? ""),
                FriendlyTypeName(p.ParameterType),
                IsNullable(p.ParameterType, p)))
            .ToList() ?? new List<PropertyDescriptor>();

        // Pull EntityType off a freshly-constructed instance — it's a
        // computed get-only property on every record so reading the
        // attribute or constructor doesn't help.
        string entityType = "(unknown)";
        try
        {
            var args = ctor!.GetParameters().Select(p => p.ParameterType.IsValueType
                ? Activator.CreateInstance(p.ParameterType)
                : null).ToArray();
            // Substitute "" for any required string parameter to avoid
            // null-reference inside a record's get-only property body.
            for (int i = 0; i < args.Length; i++)
            {
                if (ctor.GetParameters()[i].ParameterType == typeof(string) && args[i] is null)
                    args[i] = "";
            }
            var instance = (IDomainEvent)ctor.Invoke(args);
            entityType = instance.EntityType;
        }
        catch { /* fall through to "(unknown)" */ }

        return new EventDescriptor(eventType, version, entityType, clrType.Name, props);
    }

    private static string CamelCase(string s)
        => string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    private static string FriendlyTypeName(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        if (underlying == typeof(string)) return "string";
        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short)) return "integer";
        if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float)) return "number";
        if (underlying == typeof(bool)) return "boolean";
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)) return "string (ISO 8601 date-time)";
        if (underlying == typeof(Guid)) return "string (UUID)";
        if (underlying.IsEnum) return "string";
        return underlying.Name;
    }

    private static bool IsNullable(Type t, ParameterInfo p)
    {
        if (Nullable.GetUnderlyingType(t) is not null) return true;
        if (!t.IsValueType)
        {
            // Reference types are nullable when the [Nullable] context
            // marks them so. C# 8+ records propagate this via NullableAttribute.
            var nullableAttr = p.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
            if (nullableAttr is not null)
            {
                var arg = nullableAttr.ConstructorArguments.FirstOrDefault();
                return arg.Value is byte b && b == 2;
            }
            // Default for reference types in nullable-context-enabled
            // assemblies is non-null; we err on the side of "not nullable".
            return false;
        }
        return false;
    }

    public sealed record EventDescriptor(
        string EventType,
        int Version,
        string EntityType,
        string ClrTypeName,
        List<PropertyDescriptor> Properties);

    public sealed record PropertyDescriptor(string Name, string Type, bool Nullable);
}
