using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record MachineRuntimeStateResult(
    string MachineId,
    MachineRuntimeStatus Status,
    Guid? CurrentOperationId,
    DateTimeOffset LastChangedAt,
    string? FailureReason,
    Guid? ActiveAlarmId,
    int ActiveAlarmsCount
);
