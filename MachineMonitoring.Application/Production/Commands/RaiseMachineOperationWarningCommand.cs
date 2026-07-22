namespace MachineMonitoring.Application.Production.Commands;

public sealed record RaiseMachineOperationWarningCommand(
    string MachineId,
    Guid OperationId,
    string Code,
    string Message
);
