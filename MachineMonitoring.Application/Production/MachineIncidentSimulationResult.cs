namespace MachineMonitoring.Application.Production;

public enum MachineIncidentSimulationStatus
{
    None,
    WarningCreated,
    BlockingAlarmCreated,
    SkippedCooldown,
    SkippedDuplicate,
    SkippedStateChanged,
}

public sealed record MachineIncidentSimulationResult(
    MachineIncidentSimulationStatus Status,
    string MachineId,
    Guid OperationId,
    Guid? AlarmId
);
