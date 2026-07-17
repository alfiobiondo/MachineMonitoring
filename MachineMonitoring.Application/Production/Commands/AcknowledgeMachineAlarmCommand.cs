namespace MachineMonitoring.Application.Production.Commands;

public sealed record AcknowledgeMachineAlarmCommand(Guid AlarmId);
