namespace MachineMonitoring.Api.Machines;

public sealed record MachineRuntimeStateResponse(
    string MachineId,
    string Status,
    Guid? CurrentOperationId,
    DateTimeOffset LastChangedAt,
    string? FailureReason,
    Guid? ActiveAlarmId,
    int ActiveAlarmsCount
);
