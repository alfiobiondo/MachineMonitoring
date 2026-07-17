namespace MachineMonitoring.Domain.Production;

public enum MachineOperationEventType
{
    Created,
    Started,
    Paused,
    Resumed,
    Faulted,
    Recovered,
    Completed,
    Failed,
    Cancelled,
    Skipped,
}
