# Design: Strongly-typed outbox payloads (Sprint 0 #8)

**Status:** proposal — not yet implemented.
**Owner:** Dean Dunagan (sponsor) / Claude Code (drafter).
**Date:** 2026-05-08.
**Tracks:** Sprint 0 #8 from [`docs/audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md`](../audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md).

The audit's last open production-readiness item. Strongly-typed payloads
with versioned `IDomainEvent` records replace today's `object`-typed
anonymous payloads, giving us compile-time schema enforcement, a type
registry that powers documentation and validation, and a migration
story for v1 → v2 event evolution that doesn't silently break partners.

---

## 1. Today

### 1.1. Wire format

Every webhook delivery is a JSON envelope, frozen at `schemaVersion: "1.0"`:

```json
{
  "schemaVersion": "1.0",
  "eventId": "12345",
  "eventType": "workorder.closed",
  "occurredAt": "2026-05-07T18:42:11.0123456Z",
  "tenantId": "1",
  "companyId": "100",
  "siteId": "5",
  "entity": { "type": "MaintenanceEvent", "id": "789" },
  "correlationId": "closeout-789",
  "data": { "workOrderId": 789, "workOrderNumber": "WO-001", "status": "Completed", "assetId": 42, "closedAt": "...", "closedBy": "alice" }
}
```

Built by [`WebhookEnvelopeBuilder.BuildEnvelope`](../../Services/Webhooks/OutboxWriter.cs), persisted as `OutboxEvents.PayloadJson` (just the `data` field, the rest is reconstructed from columns at dispatch time).

### 1.2. Producer side

Callers invoke `IOutboxWriter.EnqueueAsync` with an anonymous-object payload:

```csharp
// CloseoutService.cs:201 (representative)
await _outbox.EnqueueAsync(
    companyId,
    siteId,
    WebhookEventTypes.WorkOrderClosed,    // free string constant
    "MaintenanceEvent",                    // free string entity type
    workOrderId.ToString(),                // free string entity id
    new {                                  // anonymous object — no schema
        WorkOrderId = workOrderId,
        WorkOrderNumber = workOrder.WorkOrderNumber,
        Status = workOrder.Status.ToString(),
        AssetId = workOrder.AssetId,
        ClosedAt = workOrder.ClosedAt,
        ClosedBy = username
    },
    $"closeout-{workOrderId}"
);
```

Five known event types live in [`WebhookEventTypes`](../../Services/Webhooks/OutboxWriter.cs):
`workrequest.created`, `workorder.created`, `workorder.closed`, `closeout.summary.generated`, `lesson.saved`.

Three call sites:
- [`Services/Maintenance/CloseoutService.cs`](../../Services/Maintenance/CloseoutService.cs) — 3 enqueue calls.
- [`Services/Maintenance/WorkRequestConversionService.cs`](../../Services/Maintenance/WorkRequestConversionService.cs) — 1 enqueue call.
- [`Pages/Admin/Webhooks/Index.cshtml.cs`](../../Pages/Admin/Webhooks/Index.cshtml.cs) — 1 enqueue call (the "send test event" button).

### 1.3. Consumer side

The dispatcher [`WebhookDispatcherHostedService`](../../Services/Webhooks/WebhookDispatcherHostedService.cs) polls `OutboxEvents` every 10 seconds, builds the envelope, and POSTs to each matching `WebhookSubscription.Url` with HMAC-SHA256 signature. Subscribers route on the `eventType` string. The `data` object is opaque on this side — it's deserialized as `JsonElement` and passed straight through.

### 1.4. What hurts

- **No compile-time payload guarantee.** Adding, renaming, or retyping a field in CloseoutService is a silent change. Subscribers find out at runtime when their consumer breaks.
- **No payload-level versioning.** Envelope `schemaVersion` is wire-format level (the envelope itself); the payload `data` block has no version tag. There's no way to say "WorkOrderClosed payload V2 has these new fields, V1 had these older ones" — both look the same on the wire.
- **No registry.** The producer-known event-type list and the subscriber-side filter list are independent stringly-typed sets. Typos and drift go undetected.
- **No event documentation.** The Sprint 5 webhooks productization migration shipped subscription management UI, but there's no auto-generated event catalog ("here are the events we emit, here are their payload shapes"). Partners get a manually-maintained README at best.
- **Hard to evolve.** When we need to add `FailureCode` to `workorder.closed`, the safest path today is: ship the new field tomorrow, hope no consumer's strict-mode JSON parser rejects the unexpected key, and pray. With versioning we'd ship V2 alongside V1, let partners migrate, then deprecate V1.

---

## 2. Goals & non-goals

### 2.1. Goals

1. Compile-time enforcement of payload shape at every producer call site.
2. Explicit per-payload version tag, independent of the envelope's wire-format version.
3. A registry of all known event types and versions, queryable at runtime.
4. A migration path that lets V1 and V2 coexist while partners cut over, with a clean deprecation lifecycle.
5. Backward compatibility: today's pending `OutboxEvents` rows must continue to dispatch unchanged after the refactor lands. External subscribers using the existing `schemaVersion: "1.0"` envelope must not see breaking changes.
6. Auto-generated event catalog documentation, wired into `/swagger` (or its sibling page) so the partner integrator sees a live, accurate view of what we emit.

### 2.2. Non-goals (deferred)

- **Schema-validation at the dispatcher level.** We trust producers; a schema validator is value-add but not load-bearing.
- **Event sourcing / projections.** This is webhook payloads, not a CQRS rewrite.
- **External schema-registry integration** (e.g., Confluent Schema Registry, AsyncAPI server). Auto-generating an AsyncAPI spec from the registry is a tempting Phase 4, but Phase 1 ships flat C# records.
- **Removing the legacy `object`-typed overload.** It stays as a `[Obsolete]`-marked back-compat path for at least one release after every internal call site has migrated. We DO NOT delete it in the rollout — too risky.

---

## 3. Proposed design

### 3.1. The `IDomainEvent` interface

```csharp
namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Marker interface for every strongly-typed webhook payload. Implementations
/// are immutable records decorated with <see cref="DomainEventAttribute"/> to
/// declare their event-type string and version. The runtime registry
/// (DomainEventRegistry) maps (eventType, version) → CLR type, powering
/// documentation, validation, and partner-facing schema export.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Stable event-type string. Matches the producer constant
    /// (e.g., "workorder.closed") and the subscription filter.</summary>
    string EventType { get; }

    /// <summary>Monotonic integer payload version. Increment on any
    /// breaking change to the schema; non-breaking field additions
    /// (with sensible defaults on deserialization) MAY stay on the same
    /// version but discouraged — bump when in doubt.</summary>
    int Version { get; }

    /// <summary>The "subject" entity — what the event is about. The
    /// envelope's `entity.type` and `entity.id` come from these.</summary>
    string EntityType { get; }
    string EntityId { get; }
}
```

### 3.2. Concrete event records

```csharp
namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>WorkOrderClosed event payload, version 1.</summary>
[DomainEvent("workorder.closed", version: 1)]
public sealed record WorkOrderClosedV1(
    int WorkOrderId,
    string WorkOrderNumber,
    string Status,
    int? AssetId,
    DateTime? ClosedAt,
    string? ClosedBy
) : IDomainEvent
{
    public string EventType => "workorder.closed";
    public int Version => 1;
    public string EntityType => "MaintenanceEvent";
    public string EntityId => WorkOrderId.ToString();
}
```

The properties on the record are EXACTLY what callers serialize today; this makes the migration mechanical. C# records give us value-equality and a concise constructor.

### 3.3. The `[DomainEvent]` attribute & registry

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DomainEventAttribute : Attribute
{
    public string EventType { get; }
    public int Version { get; }
    public DomainEventAttribute(string eventType, int version)
    {
        EventType = eventType;
        Version = version;
    }
}

public sealed class DomainEventRegistry
{
    private readonly Dictionary<(string, int), Type> _byTypeAndVersion;
    private readonly Dictionary<string, List<int>> _versionsByType;

    /// <summary>Build the registry by scanning the given assembly for
    /// types decorated with <see cref="DomainEventAttribute"/>.</summary>
    public static DomainEventRegistry FromAssembly(Assembly asm) { ... }

    public Type Resolve(string eventType, int version) { ... }
    public IReadOnlyList<int> VersionsFor(string eventType) { ... }
    public IReadOnlyCollection<(string EventType, int Version, Type ClrType)> All() { ... }
}
```

Registered at startup as a singleton:

```csharp
builder.Services.AddSingleton(DomainEventRegistry.FromAssembly(typeof(WorkOrderClosedV1).Assembly));
```

### 3.4. The new `IOutboxWriter` overload

```csharp
public interface IOutboxWriter
{
    // NEW: strongly-typed path. THIS is the recommended call.
    Task EnqueueAsync<T>(int companyId, int? siteId, T evt, string? correlationId = null) where T : IDomainEvent;

    // EXISTING: kept as [Obsolete] back-compat. Phase 1 marks it obsolete;
    // Phase 2 removes it after every internal call site migrates and at
    // least one release ships with the obsolete warning visible.
    [Obsolete("Use EnqueueAsync<T>(IDomainEvent) instead. This overload " +
              "is preserved for migration only and will be removed in v2.")]
    Task EnqueueAsync(int companyId, int? siteId, string eventType, string entityType, string entityId, object payload, string? correlationId = null);

    // ... other untyped overloads kept identically obsolete ...
}
```

The strongly-typed implementation:

```csharp
public async Task EnqueueAsync<T>(int companyId, int? siteId, T evt, string? correlationId = null)
    where T : IDomainEvent
{
    var outboxEvent = new OutboxEvent
    {
        TenantId = _tenantContext.TenantId,
        CompanyId = companyId,
        SiteId = siteId,
        EventType = evt.EventType,
        EntityType = evt.EntityType,
        EntityId = evt.EntityId,
        PayloadJson = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions),
        PayloadVersion = evt.Version,         // NEW column (see §3.5)
        OccurredAt = DateTime.UtcNow,
        Status = OutboxEventStatus.Pending,
        AttemptCount = 0,
        NextAttemptAt = DateTime.UtcNow,
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
    };
    _db.OutboxEvents.Add(outboxEvent);
    await _db.SaveChangesAsync();
}
```

Key detail: `JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions)` — passing the runtime type instead of `T` so the serializer emits all properties on the concrete record, not just those visible at the `IDomainEvent` static type.

The legacy untyped overload internally constructs an `UntypedLegacyEvent` wrapper for back-compat and routes through the same code path:

```csharp
[Obsolete(...)] public Task EnqueueAsync(int companyId, int? siteId, string eventType, string entityType, string entityId, object payload, string? correlationId = null)
    => EnqueueAsync(companyId, siteId, new UntypedLegacyEvent(eventType, entityType, entityId, payload), correlationId);

internal sealed record UntypedLegacyEvent(string EventType, string EntityType, string EntityId, object Payload) : IDomainEvent
{
    public int Version => 1; // legacy path is always V1
}
```

### 3.5. Schema change: add `OutboxEvents.PayloadVersion`

A single new nullable column:

```sql
ALTER TABLE "OutboxEvents" ADD COLUMN "PayloadVersion" integer NULL;
```

Nullable so historic rows that predate the migration carry `NULL` — these are interpreted as V1 at dispatch time. A backfill is **not required**; the dispatcher's behavior is identical for `NULL` and `1`. Any new write goes through the strongly-typed path which sets it explicitly.

### 3.6. Envelope changes (wire format)

We extend the envelope with a `payloadVersion` field, but keep `schemaVersion: "1.0"` unchanged. Subscribers using the existing v1 envelope MUST tolerate unknown fields (that's standard for JSON consumers); adding `payloadVersion` is a non-breaking change at the envelope level.

```json
{
  "schemaVersion": "1.0",
  "eventId": "12345",
  "eventType": "workorder.closed",
  "payloadVersion": 1,                        // NEW
  "occurredAt": "2026-05-07T18:42:11Z",
  ...
  "data": { ... }
}
```

If a subscriber's parser rejects unknown keys, they're already broken
against any future envelope-level extension, so we accept this risk.
Documenting "MUST tolerate unknown fields" in the partner integration
README is a separate followup.

For the partner-side routing, this means:

```python
# Subscriber Python pseudo-code
if payload["eventType"] == "workorder.closed":
    if payload.get("payloadVersion", 1) == 1:
        handle_v1(payload["data"])
    elif payload["payloadVersion"] == 2:
        handle_v2(payload["data"])
    else:
        log_unknown_version(payload)
```

### 3.7. Dispatcher changes

The dispatcher gains optional type-aware dispatch — a feature that's
value-add but not blocking. Two modes:

**Mode A (default, ships in Phase 1):** dispatcher remains pass-through.
Reads `PayloadJson`, builds the envelope, sends. Identical to today
plus the new `payloadVersion` field on the envelope.

**Mode B (Phase 3 — deferred):** dispatcher consults the registry, can
deserialize `PayloadJson` into the concrete CLR type, and exposes
strongly-typed hooks for transformation (e.g., apply per-subscription
field-masking). This is mostly useful for in-process subscribers; HTTP
delivery doesn't need it.

### 3.8. Auto-generated event catalog

A new endpoint `/admin/webhooks/catalog` (admin-gated; or in `/swagger`
as a custom doc page) renders the registry as an event catalog:

| Event type | Version | CLR type | Properties | Sample payload |
|---|---|---|---|---|
| `workorder.closed` | 1 | `WorkOrderClosedV1` | `WorkOrderId: int`, `WorkOrderNumber: string`, `Status: string`, `AssetId: int?`, `ClosedAt: DateTime?`, `ClosedBy: string?` | (rendered example) |

Sample payloads are produced by reflecting over the record's
constructor parameters and substituting placeholder values from the
property type — close-enough for partner reference without hand-writing
a JSON schema for each event.

A subsequent followup may export the same data as an AsyncAPI 3.0 spec
for partners using AsyncAPI tooling.

### 3.9. Schema migration: V1 → V2 lifecycle

When a payload needs to evolve in a breaking way (renamed/retyped
field, removed field), the producer ships V2 alongside V1:

```csharp
[DomainEvent("workorder.closed", version: 2)]
public sealed record WorkOrderClosedV2(
    int WorkOrderId,
    string WorkOrderNumber,
    string Status,
    int? AssetId,
    DateTime? ClosedAt,
    string? ClosedBy,
    string? FailureCode,                    // new
    decimal? ActualCost,                    // new
    int? OperationsCount                    // new
) : IDomainEvent { ... }
```

The producer emits BOTH versions during a migration window. Each
subscription declares its preferred version (new `MinPayloadVersion`
column on `WebhookSubscription`); the dispatcher routes to the
matching version. After all known partners are on V2, the producer
stops emitting V1 (single config flag) and `WorkOrderClosedV1` is
marked `[Obsolete]`. After at least one full release with V1 obsolete,
the type is deleted; historic `OutboxEvents` rows with version=1 can
still be dispatched on a best-effort basis using a legacy fallback,
or marked terminal (sent without delivery, with a log line).

The same flow applies for non-breaking changes too if the producer
team prefers — versioning per breaking change, period.

---

## 4. Backward compatibility

### 4.1. Pending events at deploy time

`OutboxEvents` rows queued before this PR ships have `PayloadVersion = NULL` and a payload JSON in whatever shape the producing call wrote.
The dispatcher treats `NULL` as `1`; the envelope it builds carries
`payloadVersion: 1`; subscribers receive an extra `payloadVersion` key
they didn't see before. This is the only observable wire change for
already-queued events. Partners using strict JSON parsers may need a
heads-up; that's coordinated through the partner-integration release
notes.

### 4.2. External subscribers

Existing subscribers see:
- Same `schemaVersion: "1.0"` (unchanged).
- New `payloadVersion: 1` field at the top level (additive; ignorable).
- Same `data: { ... }` block (unchanged).

The HMAC signature still covers the full envelope, so signature
verification continues to work.

### 4.3. Internal call sites

The five existing enqueue calls keep compiling because the legacy
overload is preserved (just `[Obsolete]`-tagged). The build emits
warnings but doesn't fail. Phase 2 migrates the call sites.

### 4.4. Tests

The smoke test at `Services/Testing/SmokeTestRunner.cs:1353` asserts
the literal `"\"schemaVersion\":\"1.0\""` string. That assertion still
passes — `schemaVersion` doesn't change. We add a new assertion that
the envelope contains `payloadVersion`.

---

## 5. Migration plan

### Phase 1 — registry & typed overload (single PR, ~400 LOC)

- Add `IDomainEvent`, `DomainEventAttribute`, `DomainEventRegistry`.
- Add `WorkRequestCreatedV1`, `WorkOrderCreatedV1`, `WorkOrderClosedV1`, `CloseoutSummaryGeneratedV1`, `LessonSavedV1` records — each mirrors the EXACT shape its current call site writes.
- Add `EnqueueAsync<T>(T evt) where T : IDomainEvent` overload + `UntypedLegacyEvent` for back-compat.
- Add `OutboxEvents.PayloadVersion` column (migration: nullable int, default null, index optional).
- Update `WebhookEnvelopeBuilder` to emit `payloadVersion` based on `OutboxEvents.PayloadVersion ?? 1`.
- Mark legacy `EnqueueAsync(...)` `[Obsolete]`.
- DI registration for `DomainEventRegistry`.
- Tests: registry resolves all 5 events, registry detects duplicate (eventType, version) attributes, envelope includes `payloadVersion`, legacy untyped path still works and produces version=1 envelope.

**Acceptance:** CI green, `OutboxEvents.PayloadVersion` column applied, smoke test still passes including the new assertion.

### Phase 2 — migrate call sites (5 small PRs, ~50–80 LOC each)

One PR per producer event, each migrating its call site from anonymous-object enqueue to typed `EnqueueAsync<T>` and removing the `[Obsolete]` warning at that site.

Order matters by call-site complexity:
1. `WorkRequestConversionService` (1 call site, simplest)
2. `Pages/Admin/Webhooks/Index.cshtml.cs` (1 call site, the test event)
3. `CloseoutService.WorkOrderClosed` (1 call site)
4. `CloseoutService.CloseoutSummaryGenerated` (1 call site)
5. `CloseoutService.LessonSaved` (1 call site)

After all five, the obsolete overload still exists but no internal
caller uses it. The compiler warning surface is clean.

### Phase 3 — event catalog page (1 PR, ~150 LOC)

Add `/admin/webhooks/catalog` page that renders `DomainEventRegistry.All()`
as a partner-facing reference. Link from `/admin/webhooks` and from
`/swagger`'s landing page. Wire into the navigation.

### Phase 4 — V1 → V2 migration tooling (deferred until first need)

When the first event needs a V2, build the parallel-emit logic +
subscription-version-preference column. Skipping until there's actual
demand keeps speculative plumbing out.

### Phase 5 — remove legacy untyped overload (1 PR, ~30 LOC)

After Phase 2 lands and a release ships with no internal callers
hitting the `[Obsolete]` path, delete the overload, the
`UntypedLegacyEvent` wrapper, and any remaining `object`-payload code.
Phase 5 is a pure cleanup PR.

---

## 6. Open questions

1. **Should the `EntityType` be a strongly-typed enum?** Five types today
   (`MaintenanceEvent`, `LessonLearned`, `WorkRequest`, `Asset`, etc.).
   An enum trades flexibility for safety. **Recommend:** keep as
   property string for now, add an `EntityTypes` static class with
   constants in Phase 1 to give producers a centralized list. Promote
   to enum only if we hit a real bug from a string typo.

2. **`payloadVersion` placement: top-level vs. inside `data`?**
   - Top-level is more discoverable for routing.
   - Inside `data` keeps the wire shape "envelope vs. payload" cleanly
     separated.
   **Recommend:** top-level, sibling of `eventType`. Subscribers route
   on `(eventType, payloadVersion)` together — keeping them next to
   each other in the JSON makes that obvious.

3. **Do we ship a `FailureCode` field on `WorkOrderClosedV2` now while
   we're here?** Tempting because it's available on the model but the
   v1 record doesn't include it. **Recommend:** no — Phase 1 stays a
   pure refactor with zero behavior change. Version bumps are a Phase 4
   concern.

4. **Should `DomainEventRegistry` validate at startup that every
   declared `WebhookEventTypes.*` constant has a matching IDomainEvent
   record?** **Recommend:** yes, fail-fast in `Program.cs` with a clear
   error. Catches a class of "forgot to add a V1 record" bugs.

5. **Performance of `JsonSerializer.Serialize(evt, evt.GetType(), ...)`
   reflection cost.** System.Text.Json caches type-info per type, so
   first-write is slow but steady-state matches today's anonymous-object
   path. **Recommend:** measure but don't optimize — outbox enqueue is
   not a hot path.

---

## 7. Tests

Phase 1 ships with these test classes (~250 LOC total):

```
tests/Abs.FixedAssets.Tests/Webhooks/
├── DomainEventRegistryTests.cs
│   - All_RegisteredEvents_AreDiscoveredAtStartup
│   - Resolve_KnownEventAndVersion_ReturnsClrType
│   - Resolve_UnknownEventOrVersion_ReturnsNull
│   - DuplicateAttribute_ThrowsAtRegistration
├── OutboxWriterTypedEnqueueTests.cs
│   - EnqueueAsync_TypedEvent_PersistsCorrectShape
│   - EnqueueAsync_TypedEvent_StampsPayloadVersion
│   - EnqueueAsync_LegacyOverload_StillWorksAndStampsVersion1
├── WebhookEnvelopeTypedTests.cs
│   - BuildEnvelope_IncludesPayloadVersion
│   - BuildEnvelope_NullPayloadVersion_DefaultsTo1
│   - BuildEnvelope_PreservesSchemaVersionString
└── DomainEventLifecycleSnapshotTests.cs
    - WorkRequestCreatedV1_PayloadShape_MatchesProducerCallSite
    - WorkOrderClosedV1_PayloadShape_MatchesProducerCallSite
    - ... one per event
```

The lifecycle snapshot tests are the load-bearing ones — they
construct each `IDomainEvent` record from a hand-written reference
fixture, serialize it, and compare to a checked-in JSON snapshot. Any
breaking change to a record's shape fails the snapshot test
immediately, surfacing the breaking-change before partners see it.

---

## 8. Risks

- **Snapshot drift** — if developers update the snapshot blindly to
  pass CI, the test loses its value. Mitigation: PR review checklist
  item: "any change to a `*V1.json` snapshot is a breaking event-payload
  change. Bump to V2 instead."
- **Old `OutboxEvents` rows.** Rows with shapes that don't match the
  current V1 record (because someone hand-wrote a slightly different
  payload at some point) will deserialize partially. Mitigation: the
  dispatcher uses the raw JSON, not the typed deserialization, so old
  rows pass through unchanged on the wire. Only Phase 3+ in-process
  consumers risk this.
- **Anyone who hand-built a subscriber against today's exact field
  set** without tolerating extra keys will see `payloadVersion` and
  potentially break. Mitigation: announce in partner release notes one
  release before Phase 1 ships.

---

## 9. References

- Audit Sprint 0 #8: [`docs/audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md`](../audit-2026-05-07/09_DISRUPTION_PLAYBOOK_AND_90_DAY_ROADMAP.md).
- Existing outbox: [`Services/Webhooks/OutboxWriter.cs`](../../Services/Webhooks/OutboxWriter.cs), [`Services/Webhooks/WebhookDispatcherHostedService.cs`](../../Services/Webhooks/WebhookDispatcherHostedService.cs).
- Producer call sites: [`Services/Maintenance/CloseoutService.cs`](../../Services/Maintenance/CloseoutService.cs), [`Services/Maintenance/WorkRequestConversionService.cs`](../../Services/Maintenance/WorkRequestConversionService.cs), [`Pages/Admin/Webhooks/Index.cshtml.cs`](../../Pages/Admin/Webhooks/Index.cshtml.cs).
- Models: [`Models/OutboxEvent.cs`](../../Models/OutboxEvent.cs), [`Models/WebhookSubscription.cs`](../../Models/WebhookSubscription.cs).
- Industry pattern: AsyncAPI 3.0 (the long-term north star for
  publishing this same data in a partner-toolable spec).
