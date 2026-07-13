namespace MachineMonitoring.Application.Production.Commands;

public sealed record CancelMachineOperationCommand(Guid OperationId);
