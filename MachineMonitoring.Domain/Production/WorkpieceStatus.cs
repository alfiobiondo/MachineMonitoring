namespace MachineMonitoring.Domain.Production;

public enum WorkpieceStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Skipped,
}
