using System;
using System.Text.Json;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Webhooks;
using Xunit;

namespace Abs.FixedAssets.Tests.Webhooks;

/// <summary>
/// Lock-in tests for the envelope's payloadVersion field. Phase 1 of
/// the typed-outbox-payloads work; subscribers MUST tolerate this new
/// field per the schemaVersion-1.0 contract.
/// </summary>
public class WebhookEnvelopePayloadVersionTests
{
    private static OutboxEvent NewEvent(int? payloadVersion)
    {
        return new OutboxEvent
        {
            Id = 12345,
            TenantId = 1,
            CompanyId = 100,
            SiteId = 5,
            EventType = "workorder.closed",
            EntityType = "MaintenanceEvent",
            EntityId = "789",
            PayloadJson = """{"workOrderId":789,"status":"Completed"}""",
            PayloadVersion = payloadVersion,
            OccurredAt = new DateTime(2026, 5, 7, 18, 30, 0, DateTimeKind.Utc),
            CorrelationId = "corr-789"
        };
    }

    [Fact]
    public void BuildEnvelope_TypedEventV1_IncludesPayloadVersion1()
    {
        var json = WebhookEnvelopeBuilder.BuildEnvelope(NewEvent(payloadVersion: 1));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("payloadVersion").GetInt32());
    }

    [Fact]
    public void BuildEnvelope_NullPayloadVersion_DefaultsTo1()
    {
        // Existing rows from before the migration carry NULL; they must
        // dispatch as V1 (the implicit legacy version) rather than
        // throwing or omitting the field.
        var json = WebhookEnvelopeBuilder.BuildEnvelope(NewEvent(payloadVersion: null));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("payloadVersion").GetInt32());
    }

    [Fact]
    public void BuildEnvelope_PreservesSchemaVersion1String()
    {
        // schemaVersion is the WIRE-format version, NOT the payload
        // version. It MUST stay "1.0" — anything else breaks every
        // subscriber that filters on the existing constant.
        var json = WebhookEnvelopeBuilder.BuildEnvelope(NewEvent(payloadVersion: 1));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("1.0", doc.RootElement.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public void BuildEnvelope_PayloadVersion2_PreservesNonV1Value()
    {
        // Forward-looking: when WorkOrderClosedV2 ships, its events
        // should serialize with payloadVersion=2. The builder must not
        // override what the producer wrote.
        var json = WebhookEnvelopeBuilder.BuildEnvelope(NewEvent(payloadVersion: 2));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("payloadVersion").GetInt32());
    }

    [Fact]
    public void BuildEnvelope_PayloadVersionAndSchemaVersion_AreSiblingsAtTopLevel()
    {
        // Sibling-of-eventType placement is the design choice from
        // §6 question 2 of the design doc — payloadVersion is a routing
        // hint, kept next to eventType so subscribers see them together.
        var json = WebhookEnvelopeBuilder.BuildEnvelope(NewEvent(payloadVersion: 1));
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("payloadVersion", out _));
        Assert.True(doc.RootElement.TryGetProperty("eventType", out _));
        Assert.True(doc.RootElement.TryGetProperty("schemaVersion", out _));
    }
}
