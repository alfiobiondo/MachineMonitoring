namespace MachineMonitoring.Application.Production.Notifications;

public sealed record MachineAlarmAcknowledgedNotification(
    Guid AlarmId,
    string MachineId,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
