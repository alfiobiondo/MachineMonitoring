namespace MachineMonitoring.Api.Operations;

public sealed record MachineOperationEventResponse(
    Guid Id,
    Guid MachineOperationId,
    Guid WorkpieceId,
    Guid ProductionLotId,
    int OperationSequenceNumber,
    int WorkpieceSequenceNumber,
    string EventType,
    DateTimeOffset OccurredAt,
    string? PreviousStatus,
    string? NewStatus,
    int? ProgressPercentage,
    string? Phase,
    string? Reason,
    Guid? MachineAlarmId,
    string? Metadata
);
