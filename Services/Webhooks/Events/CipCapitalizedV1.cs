using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// CIP project capitalized to a fixed asset, V1. Emitted by
/// <c>Services/Cip/CipCapitalizationService.CapitalizeAsync</c> after
/// the new asset, capitalization journal entry (Dr Asset / Cr CIP),
/// CipCapitalization mapping rows, and depreciation snapshot have all
/// committed. The CIP-driven <c>asset.created</c> event also fires
/// from this site with <c>Origin="cip.capitalized"</c> so consumers
/// can correlate the lifecycle.
/// </summary>
[DomainEvent("cip.capitalized", version: 1)]
public sealed record CipCapitalizedV1(
    int CipProjectId,
    string ProjectNumber,
    string ProjectName,
    int CapitalizationId,
    int NewAssetId,
    string AssetNumber,
    int? CompanyId,
    int? SiteId,
    decimal TotalCapitalized,
    int CapitalizableCostCount,
    int JournalEntryId,
    DateTime CapitalizedAt,
    string CapitalizedByUserId
) : IDomainEvent
{
    public string EventType => "cip.capitalized";
    public int Version => 1;
    public string EntityType => "CipProject";
    public string EntityId => CipProjectId.ToString();
}
