namespace MachineMonitoring.Application.Production.Commands;

public sealed record StartMachineOperationCommand(Guid OperationId, string InitialPhase);
