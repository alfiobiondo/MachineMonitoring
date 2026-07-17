using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Notifications;

public sealed record MachineRuntimeStatusChangedNotification(
    string MachineId,
    MachineRuntimeStatus Status,
    Guid? CurrentOperationId,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
