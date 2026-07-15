namespace MachineMonitoring.Application.Production.Commands;

public sealed record FailMachineOperationCommand(Guid OperationId, string FailureReason);
