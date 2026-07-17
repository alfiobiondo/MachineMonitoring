using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Notifications;

public sealed record OperationStatusChangedNotification(
    Guid OperationId,
    MachineOperationStatus Status,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
