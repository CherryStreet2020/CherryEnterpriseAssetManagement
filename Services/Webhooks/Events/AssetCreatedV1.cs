using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Asset created payload, V1. Emitted when a new <c>Asset</c> row is
/// committed via a domain transition — the <c>/Assets/Asset</c> create
/// page handler. Bulk-import and seeder paths intentionally do NOT
/// emit this event; they are bootstrap/migration tooling, not domain
/// transitions, and emitting per-row would flood subscribers during
/// onboarding.
/// </summary>
[DomainEvent("asset.created", version: 1)]
public sealed record AssetCreatedV1(
    int AssetId,
    string AssetNumber,
    string Description,
    int? CompanyId,
    int? SiteId,
    decimal AcquisitionCost,
    DateTime InServiceDate,
    string Status,
    int? AssetCategoryId,
    int? VendorId,
    string? CreatedBy,
    string Origin
) : IDomainEvent
{
    public string EventType => "asset.created";
    public int Version => 1;
    public string EntityType => "Asset";
    public string EntityId => AssetId.ToString();
}
