namespace MachineMonitoring.Api.Operations;

public sealed record FaultMachineOperationRequest(
    string FailureReason,
    string AlarmCode,
    string AlarmMessage,
    string Severity
);
