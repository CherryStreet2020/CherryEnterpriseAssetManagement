using System;
using System.Linq;
using System.Text.Json;
using Abs.FixedAssets.Services.Webhooks.Events;
using Xunit;

namespace Abs.FixedAssets.Tests.Webhooks;

/// <summary>
/// LIFECYCLE SNAPSHOT TESTS — these lock in the wire shape of every V1
/// payload. ANY failure here is a breaking-change signal:
///
///   - DO NOT update the expected property set to make the test pass.
///   - Bump the record to V2 instead and add a new
///     <c>*V2_PayloadShape</c> snapshot.
///
/// The whole point of versioning is that V1 is FROZEN. If you "fix"
/// the snapshot, you've silently broken every partner consuming V1.
///
/// We assert the property SET (names + presence + sample values) rather
/// than exact string equality so test stability isn't held hostage to
/// System.Text.Json's DateTime/decimal format variations across .NET
/// patch versions.
/// </summary>
public class DomainEventLifecycleSnapshotTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static JsonDocument SerializeAndParse(IDomainEvent evt)
    {
        var json = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);
        return JsonDocument.Parse(json);
    }

    private static void AssertPropertySet(JsonElement root, params string[] expectedNames)
    {
        var actual = root.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        var expected = expectedNames.OrderBy(n => n).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WorkOrderCreatedV1_PayloadShape_MatchesProducerCallSite()
    {
        using var doc = SerializeAndParse(new WorkOrderCreatedV1(
            WorkOrderId: 100,
            WorkOrderNumber: "WO-100",
            Status: "Scheduled",
            Priority: "High",
            AssetId: 42,
            SourceWorkRequestId: 7,
            OperationCount: 3,
            CreatedAt: new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc)));

        var root = doc.RootElement;
        AssertPropertySet(root,
            "eventType", "version", "entityType", "entityId",
            "workOrderId", "workOrderNumber", "status", "priority",
            "assetId", "sourceWorkRequestId", "operationCount", "createdAt");

        Assert.Equal("workorder.created", root.GetProperty("eventType").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("MaintenanceEvent", root.GetProperty("entityType").GetString());
        Assert.Equal("100", root.GetProperty("entityId").GetString());
        Assert.Equal(100, root.GetProperty("workOrderId").GetInt32());
        Assert.Equal("Scheduled", root.GetProperty("status").GetString());
        Assert.Equal(3, root.GetProperty("operationCount").GetInt32());
    }

    [Fact]
    public void WorkOrderClosedV1_PayloadShape_MatchesProducerCallSite()
    {
        using var doc = SerializeAndParse(new WorkOrderClosedV1(
            WorkOrderId: 789,
            WorkOrderNumber: "WO-789",
            Status: "Completed",
            AssetId: 42,
            ClosedAt: new DateTime(2026, 5, 8, 18, 30, 0, DateTimeKind.Utc),
            ClosedBy: "alice"));

        var root = doc.RootElement;
        AssertPropertySet(root,
            "eventType", "version", "entityType", "entityId",
            "workOrderId", "workOrderNumber", "status", "assetId",
            "closedAt", "closedBy");

        Assert.Equal("workorder.closed", root.GetProperty("eventType").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("alice", root.GetProperty("closedBy").GetString());
    }

    [Fact]
    public void CloseoutSummaryGeneratedV1_PayloadShape_MatchesProducerCallSite()
    {
        using var doc = SerializeAndParse(new CloseoutSummaryGeneratedV1(
            WorkOrderId: 789,
            WorkOrderNumber: "WO-789",
            SummaryLength: 250,
            OperationsCount: 5,
            HasLessonsLearned: true));

        var root = doc.RootElement;
        AssertPropertySet(root,
            "eventType", "version", "entityType", "entityId",
            "workOrderId", "workOrderNumber", "summaryLength",
            "operationsCount", "hasLessonsLearned");

        Assert.Equal("closeout.summary.generated", root.GetProperty("eventType").GetString());
        Assert.True(root.GetProperty("hasLessonsLearned").GetBoolean());
        Assert.Equal(250, root.GetProperty("summaryLength").GetInt32());
    }

    [Fact]
    public void LessonSavedV1_PayloadShape_MatchesProducerCallSite()
    {
        using var doc = SerializeAndParse(new LessonSavedV1(
            LessonId: 11,
            SourceWorkOrderId: 789,
            WorkOrderNumber: "WO-789",
            FailureCode: "BEARING_FAILURE",
            Tags: "preventative,bearings",
            CreatedBy: "alice"));

        var root = doc.RootElement;
        AssertPropertySet(root,
            "eventType", "version", "entityType", "entityId",
            "lessonId", "sourceWorkOrderId", "workOrderNumber",
            "failureCode", "tags", "createdBy");

        Assert.Equal("lesson.saved", root.GetProperty("eventType").GetString());
        Assert.Equal("LessonLearned", root.GetProperty("entityType").GetString());
        Assert.Equal("BEARING_FAILURE", root.GetProperty("failureCode").GetString());
    }

    [Fact]
    public void TestPingV1_PayloadShape_MatchesProducerCallSite()
    {
        using var doc = SerializeAndParse(new TestPingV1(
            EntityId: "abc123",
            Message: "ping",
            Timestamp: new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc),
            SubscriptionId: 5,
            SubscriptionName: "Slack"));

        var root = doc.RootElement;
        AssertPropertySet(root,
            "eventType", "version", "entityType", "entityId",
            "message", "timestamp", "subscriptionId", "subscriptionName");

        Assert.Equal("test.ping", root.GetProperty("eventType").GetString());
        Assert.Equal("TestEvent", root.GetProperty("entityType").GetString());
        Assert.Equal("abc123", root.GetProperty("entityId").GetString());
        Assert.Equal(5, root.GetProperty("subscriptionId").GetInt32());
    }
}
