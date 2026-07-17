namespace MachineMonitoring.Application.Production.Commands;

public sealed record ResolveMachineAlarmCommand(Guid AlarmId, string? ResolutionNotes);
