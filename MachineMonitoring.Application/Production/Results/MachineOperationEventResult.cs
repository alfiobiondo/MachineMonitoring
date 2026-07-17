using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record MachineOperationEventResult(
    Guid Id,
    Guid MachineOperationId,
    Guid WorkpieceId,
    Guid ProductionLotId,
    int OperationSequenceNumber,
    int WorkpieceSequenceNumber,
    MachineOperationEventType EventType,
    DateTimeOffset OccurredAt,
    MachineOperationStatus? PreviousStatus,
    MachineOperationStatus? NewStatus,
    int? ProgressPercentage,
    string? Phase,
    string? Reason,
    Guid? MachineAlarmId,
    string? Metadata
);
