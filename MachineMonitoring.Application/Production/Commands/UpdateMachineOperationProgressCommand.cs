namespace MachineMonitoring.Application.Production.Commands;

public sealed record UpdateMachineOperationProgressCommand(
    Guid OperationId,
    int ProgressPercentage,
    string CurrentPhase
);
