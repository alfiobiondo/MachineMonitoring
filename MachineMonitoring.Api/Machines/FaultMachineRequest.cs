namespace MachineMonitoring.Api.Machines;

public sealed record FaultMachineRequest(
    string Code,
    string Severity,
    string Message,
    Guid? OperationId
);
