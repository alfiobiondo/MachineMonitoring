namespace MachineMonitoring.Application.Production.Results;

public enum RaiseMachineOperationWarningStatus
{
    Created,
    SkippedDuplicate,
    SkippedStateChanged,
}

public sealed record RaiseMachineOperationWarningResult(
    RaiseMachineOperationWarningStatus Status,
    Guid? AlarmId
);
