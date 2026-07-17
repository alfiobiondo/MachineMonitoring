namespace MachineMonitoring.Application.Production.Notifications;

public sealed record MachineAlarmRaisedNotification(
    Guid AlarmId,
    string MachineId,
    Guid? OperationId,
    DateTimeOffset OccurredAt
) : ProductionNotification(OccurredAt);
