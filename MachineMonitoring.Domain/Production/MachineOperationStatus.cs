namespace MachineMonitoring.Domain.Production;

public enum MachineOperationStatus
{
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled,
}
