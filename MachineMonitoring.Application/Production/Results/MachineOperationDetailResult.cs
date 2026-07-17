using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record MachineOperationDetailsResult(
    Guid Id,
    Guid WorkpieceId,
    int SequenceNumber,
    string MachineId,
    MachineOperationType Type,
    MachineOperationStatus Status,
    int ProgressPercentage,
    string? CurrentPhase,
    string? FailureReason,
    MachineRuntimeStatus MachineRuntimeStatus,
    MachineAlarmResult? ActiveBlockingAlarm,
    bool CanResume,
    bool CanPause,
    bool CanFault,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    LaserCutConfigurationDetailsResult Configuration
);
