namespace MachineMonitoring.Application.Production.Notifications;

public sealed record OperationProgressChangedNotification(
    Guid OperationId,
    int ProgressPercentage,
    string? CurrentPhase,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
