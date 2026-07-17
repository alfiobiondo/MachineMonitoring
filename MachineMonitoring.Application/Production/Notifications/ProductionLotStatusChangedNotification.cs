using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Notifications;

public sealed record ProductionLotStatusChangedNotification(
    Guid ProductionLotId,
    ProductionLotStatus Status,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
