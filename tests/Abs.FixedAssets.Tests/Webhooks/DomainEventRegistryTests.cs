using System.Linq;
using Abs.FixedAssets.Services.Webhooks.Events;
using Xunit;

namespace Abs.FixedAssets.Tests.Webhooks;

/// <summary>
/// Lock-in tests for the DomainEventRegistry. Phase 1 of the
/// typed-outbox-payloads work — see docs/design/OUTBOX_TYPED_PAYLOADS.md.
/// </summary>
public class DomainEventRegistryTests
{
    private static DomainEventRegistry RealRegistry()
        => DomainEventRegistry.FromAssembly(typeof(IDomainEvent).Assembly);

    [Fact]
    public void FromAssembly_RealAssembly_DiscoversAllPhase1Events()
    {
        var registry = RealRegistry();
        var all = registry.All();

        // Phase 1 ships records for the four events we actually emit
        // today. WorkRequestCreated has no producer yet (constant in
        // WebhookEventTypes is unused) — no record until a producer
        // exists, by design.
        Assert.Contains(all, t => t.EventType == "workorder.created" && t.Version == 1);
        Assert.Contains(all, t => t.EventType == "workorder.closed" && t.Version == 1);
        Assert.Contains(all, t => t.EventType == "closeout.summary.generated" && t.Version == 1);
        Assert.Contains(all, t => t.EventType == "lesson.saved" && t.Version == 1);
    }

    [Fact]
    public void Resolve_KnownEventAndVersion_ReturnsClrType()
    {
        var registry = RealRegistry();
        var clr = registry.Resolve("workorder.closed", 1);
        Assert.NotNull(clr);
        Assert.Equal(typeof(WorkOrderClosedV1), clr);
    }

    [Fact]
    public void Resolve_UnknownEvent_ReturnsNull()
    {
        var registry = RealRegistry();
        Assert.Null(registry.Resolve("definitely.not.real", 1));
    }

    [Fact]
    public void Resolve_KnownEventUnknownVersion_ReturnsNull()
    {
        var registry = RealRegistry();
        Assert.Null(registry.Resolve("workorder.closed", 999));
    }

    [Fact]
    public void VersionsFor_KnownEvent_ReturnsAllRegisteredVersionsAscending()
    {
        var registry = RealRegistry();
        var versions = registry.VersionsFor("workorder.closed");
        Assert.NotEmpty(versions);
        Assert.Equal(versions.OrderBy(v => v), versions); // ascending
        Assert.Contains(1, versions);
    }

    [Fact]
    public void VersionsFor_UnknownEvent_ReturnsEmpty()
    {
        var registry = RealRegistry();
        Assert.Empty(registry.VersionsFor("not.a.real.event"));
    }

    // Note: duplicate-(eventType,version) detection in
    // DomainEventRegistry.FromAssembly is a straight-line guard
    // (TryGetValue + throw on collision). Verified by code review;
    // not exercised here because synthesizing a second decorated type
    // in-process would require IL emit that adds CI fragility for
    // little incremental coverage. If a real duplicate ever ships, the
    // production startup path throws before any request is served — a
    // single-DI-resolve smoke test would catch it.
}
