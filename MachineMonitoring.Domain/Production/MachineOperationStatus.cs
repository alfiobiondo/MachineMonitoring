namespace MachineMonitoring.Domain.Production;

public enum MachineOperationStatus
{
    Queued,
    Running,
    Paused,
    Faulted,
    Completed,
    Failed,
    Cancelled,
    Skipped,
}
