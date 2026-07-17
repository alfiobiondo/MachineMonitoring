using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Notifications;

public sealed record WorkpieceStatusChangedNotification(
    Guid WorkpieceId,
    WorkpieceStatus Status,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
