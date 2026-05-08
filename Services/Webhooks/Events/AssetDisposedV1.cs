using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Asset disposed payload, V1. Emitted by the <c>/Assets/Dispose</c>
/// page after the disposal row + (optional) JE are committed. The JE
/// id is null when the user opted out of journal posting.
/// <see cref="GainLoss"/> is positive for a gain, negative for a loss.
/// </summary>
[DomainEvent("asset.disposed", version: 1)]
public sealed record AssetDisposedV1(
    int AssetId,
    string AssetNumber,
    int? CompanyId,
    DateTime DisposalDate,
    string DisposalType,
    decimal Proceeds,
    decimal DisposalExpense,
    decimal AcquisitionCost,
    decimal AccumulatedDepreciation,
    decimal NetBookValue,
    decimal GainLoss,
    int? JournalEntryId,
    int? BookId,
    string? Notes
) : IDomainEvent
{
    public string EventType => "asset.disposed";
    public int Version => 1;
    public string EntityType => "Asset";
    public string EntityId => AssetId.ToString();
}
