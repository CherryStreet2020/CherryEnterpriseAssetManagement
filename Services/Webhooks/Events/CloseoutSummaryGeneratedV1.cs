namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Closeout summary generated payload, V1. Mirrors the anonymous-object
/// payload emitted by <c>Services/Maintenance/CloseoutService.cs</c>
/// after a work-order closeout summary is produced.
/// </summary>
[DomainEvent("closeout.summary.generated", version: 1)]
public sealed record CloseoutSummaryGeneratedV1(
    int WorkOrderId,
    string WorkOrderNumber,
    int SummaryLength,
    int OperationsCount,
    bool HasLessonsLearned
) : IDomainEvent
{
    public string EventType => "closeout.summary.generated";
    public int Version => 1;
    public string EntityType => "WorkOrder";
    public string EntityId => WorkOrderId.ToString();
}
