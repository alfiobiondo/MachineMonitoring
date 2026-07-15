namespace MachineMonitoring.Api.Operations;

public sealed record UpdateMachineOperationProgressRequest(
    int ProgressPercentage,
    string CurrentPhase
);
