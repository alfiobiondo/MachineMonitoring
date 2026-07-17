using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Notifications;

public sealed record OperationEventAppendedNotification(
    Guid EventId,
    Guid OperationId,
    MachineOperationEventType EventType,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
