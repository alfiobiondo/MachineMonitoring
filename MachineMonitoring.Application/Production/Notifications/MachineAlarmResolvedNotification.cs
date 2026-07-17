namespace MachineMonitoring.Application.Production.Notifications;

public sealed record MachineAlarmResolvedNotification(
    Guid AlarmId,
    string MachineId,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
