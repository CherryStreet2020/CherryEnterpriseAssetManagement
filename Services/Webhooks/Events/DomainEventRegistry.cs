using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Runtime registry of every strongly-typed <see cref="IDomainEvent"/>
/// in the application. Built once at startup by scanning an assembly
/// for types decorated with <see cref="DomainEventAttribute"/>.
///
/// Used for:
/// <list type="bullet">
///   <item><description>Validation: confirm a payload's event-type and
///   version are known before enqueue.</description></item>
///   <item><description>Documentation: generate the partner-facing
///   event catalog page from the registered records' shapes.</description></item>
///   <item><description>Dispatch (Phase 3): deserialize PayloadJson into
///   the matching CLR type for in-process subscribers.</description></item>
/// </list>
///
/// Design: docs/design/OUTBOX_TYPED_PAYLOADS.md
/// </summary>
public sealed class DomainEventRegistry
{
    private readonly Dictionary<(string EventType, int Version), Type> _byTypeAndVersion;

    private DomainEventRegistry(Dictionary<(string, int), Type> byTypeAndVersion)
    {
        _byTypeAndVersion = byTypeAndVersion;
    }

    /// <summary>Scan the given assembly for types decorated with
    /// <see cref="DomainEventAttribute"/> that also implement
    /// <see cref="IDomainEvent"/>. Throws on duplicate (eventType, version).</summary>
    public static DomainEventRegistry FromAssembly(Assembly asm)
    {
        if (asm is null) throw new ArgumentNullException(nameof(asm));

        var byTypeAndVersion = new Dictionary<(string, int), Type>();

        foreach (var type in asm.GetTypes())
        {
            var attr = type.GetCustomAttribute<DomainEventAttribute>();
            if (attr is null) continue;

            if (!typeof(IDomainEvent).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"{type.FullName} is decorated with [DomainEvent] but does not implement IDomainEvent.");
            }

            var key = (attr.EventType, attr.Version);
            if (byTypeAndVersion.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate [DomainEvent(\"{attr.EventType}\", {attr.Version})] on " +
                    $"{type.FullName} and {existing.FullName}. Each (eventType, version) " +
                    "pair must map to exactly one CLR type.");
            }

            byTypeAndVersion[key] = type;
        }

        return new DomainEventRegistry(byTypeAndVersion);
    }

    /// <summary>Resolve a registered event by (eventType, version).
    /// Returns null if the pair is unknown.</summary>
    public Type? Resolve(string eventType, int version)
    {
        return _byTypeAndVersion.TryGetValue((eventType, version), out var type) ? type : null;
    }

    /// <summary>All known versions for an event type, sorted ascending.
    /// Empty list if the event type has no registered records.</summary>
    public IReadOnlyList<int> VersionsFor(string eventType)
    {
        return _byTypeAndVersion.Keys
            .Where(k => k.EventType == eventType)
            .Select(k => k.Version)
            .OrderBy(v => v)
            .ToList();
    }

    /// <summary>Every (eventType, version, clrType) tuple in the registry,
    /// sorted by event type then version. Used to render the catalog page.</summary>
    public IReadOnlyCollection<(string EventType, int Version, Type ClrType)> All()
    {
        return _byTypeAndVersion
            .Select(kvp => (kvp.Key.EventType, kvp.Key.Version, kvp.Value))
            .OrderBy(t => t.EventType)
            .ThenBy(t => t.Version)
            .ToList();
    }
}
