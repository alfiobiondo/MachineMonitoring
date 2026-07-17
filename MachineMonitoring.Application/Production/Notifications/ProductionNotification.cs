namespace MachineMonitoring.Application.Production.Notifications;

public abstract record ProductionNotification(DateTimeOffset OccurredAt);
