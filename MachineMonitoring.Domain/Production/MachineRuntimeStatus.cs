namespace MachineMonitoring.Domain.Production;

public enum MachineRuntimeStatus
{
    Available,
    Running,
    Paused,
    Faulted,
    Maintenance,
    Offline,
}
