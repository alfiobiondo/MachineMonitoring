using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Commands;

public sealed record FaultMachineCommand(
    string MachineId,
    string Code,
    MachineAlarmSeverity Severity,
    string Message,
    Guid? OperationId
);
