namespace MachineMonitoring.Application.Production.Commands;

public sealed record PauseMachineOperationCommand(Guid OperationId);
