using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Capital improvement applied to an asset, V1. Emitted by the
/// <c>/Assets/Improve</c> page after the improvement row is committed,
/// the cost basis is incremented (if capitalized), and the depreciation
/// snapshot is recomputed. <see cref="Capitalized"/> distinguishes a
/// true cost-basis change from an expensed improvement.
/// </summary>
[DomainEvent("asset.improved", version: 1)]
public sealed record AssetImprovedV1(
    int AssetId,
    string AssetNumber,
    int CapitalImprovementId,
    DateTime ImprovementDate,
    string Description,
    decimal Cost,
    bool Capitalized,
    int? UsefulLifeExtensionMonths,
    decimal NewAcquisitionCost,
    int NewUsefulLifeMonths,
    int? CompanyId,
    string? Vendor,
    string? InvoiceNumber
) : IDomainEvent
{
    public string EventType => "asset.improved";
    public int Version => 1;
    public string EntityType => "Asset";
    public string EntityId => AssetId.ToString();
}
