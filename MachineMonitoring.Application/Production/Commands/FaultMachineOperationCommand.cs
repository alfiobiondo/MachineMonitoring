using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Commands;

public sealed record FaultMachineOperationCommand(
    Guid OperationId,
    string AlarmCode,
    string FailureReason,
    string AlarmMessage,
    MachineAlarmSeverity Severity
);
