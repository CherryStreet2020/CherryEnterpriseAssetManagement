using System.Linq;
using Abs.FixedAssets.Pages.Admin.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Xunit;

namespace Abs.FixedAssets.Tests.Webhooks;

/// <summary>
/// Smoke tests for the auto-generated event catalog page (Phase 3).
/// We don't assert specific HTML — just that the page model produces
/// the right descriptors for partner-facing rendering.
/// </summary>
public class CatalogPageTests
{
    private static CatalogModel NewPage()
    {
        var registry = DomainEventRegistry.FromAssembly(typeof(IDomainEvent).Assembly);
        return new CatalogModel(registry);
    }

    [Fact]
    public void OnGet_ProducesDescriptorForEveryRegisteredEvent()
    {
        var page = NewPage();
        page.OnGet();

        // Phase 1 + 2: five records emitted.
        Assert.Equal(5, page.Events.Count);
        Assert.Contains(page.Events, e => e.EventType == "workorder.created" && e.Version == 1);
        Assert.Contains(page.Events, e => e.EventType == "workorder.closed" && e.Version == 1);
        Assert.Contains(page.Events, e => e.EventType == "closeout.summary.generated" && e.Version == 1);
        Assert.Contains(page.Events, e => e.EventType == "lesson.saved" && e.Version == 1);
        Assert.Contains(page.Events, e => e.EventType == "test.ping" && e.Version == 1);
    }

    [Fact]
    public void OnGet_DescriptorPropertiesMatchRecordShape()
    {
        var page = NewPage();
        page.OnGet();

        var closed = page.Events.Single(e => e.EventType == "workorder.closed");
        Assert.Equal("MaintenanceEvent", closed.EntityType);
        Assert.Equal(nameof(WorkOrderClosedV1), closed.ClrTypeName);

        var propNames = closed.Properties.Select(p => p.Name).ToList();
        Assert.Contains("entityType", propNames);
        Assert.Contains("entityId", propNames);
        Assert.Contains("workOrderId", propNames);
        Assert.Contains("workOrderNumber", propNames);
        Assert.Contains("status", propNames);
        Assert.Contains("assetId", propNames);
        Assert.Contains("closedAt", propNames);
        Assert.Contains("closedBy", propNames);

        // EventType + Version are interface-level and excluded from the
        // property table (they're already shown in the card header).
        Assert.DoesNotContain("eventType", propNames);
        Assert.DoesNotContain("version", propNames);
    }

    [Fact]
    public void OnGet_NullableValueTypes_AreFlaggedNullable()
    {
        var page = NewPage();
        page.OnGet();

        var closed = page.Events.Single(e => e.EventType == "workorder.closed");
        var assetId = closed.Properties.Single(p => p.Name == "assetId");
        Assert.True(assetId.Nullable); // int? in record
        Assert.Equal("integer", assetId.Type);

        var workOrderId = closed.Properties.Single(p => p.Name == "workOrderId");
        Assert.False(workOrderId.Nullable); // int (non-null)
    }

    [Fact]
    public void OnGet_DateTimeTypes_RenderAsIso8601String()
    {
        var page = NewPage();
        page.OnGet();

        var closed = page.Events.Single(e => e.EventType == "workorder.closed");
        var closedAt = closed.Properties.Single(p => p.Name == "closedAt");
        Assert.Contains("ISO 8601", closedAt.Type);
        Assert.True(closedAt.Nullable); // DateTime? in record
    }
}
