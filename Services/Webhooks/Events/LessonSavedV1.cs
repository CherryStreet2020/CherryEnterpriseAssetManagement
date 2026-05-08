namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Lesson learned saved payload, V1. Mirrors the anonymous-object
/// payload emitted by <c>Services/Maintenance/CloseoutService.cs</c>
/// when a lesson is captured against a closed work order.
/// </summary>
[DomainEvent("lesson.saved", version: 1)]
public sealed record LessonSavedV1(
    int LessonId,
    int SourceWorkOrderId,
    string WorkOrderNumber,
    string? FailureCode,
    string? Tags,
    string? CreatedBy
) : IDomainEvent
{
    public string EventType => "lesson.saved";
    public int Version => 1;
    public string EntityType => "LessonLearned";
    public string EntityId => LessonId.ToString();
}
