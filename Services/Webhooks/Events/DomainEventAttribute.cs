using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Marks a record as a strongly-typed domain event with its stable
/// event-type string and integer version. Discovered at startup by
/// <see cref="DomainEventRegistry.FromAssembly(System.Reflection.Assembly)"/>.
///
/// Each (eventType, version) pair MUST be unique across the assembly;
/// the registry throws at construction if a duplicate is detected.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DomainEventAttribute : Attribute
{
    public string EventType { get; }
    public int Version { get; }

    public DomainEventAttribute(string eventType, int version)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("eventType must be non-empty", nameof(eventType));
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), version, "version must be >= 1");

        EventType = eventType;
        Version = version;
    }
}
