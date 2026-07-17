namespace MachineMonitoring.Api.Operations;

public sealed record MachineOperationDetailsResponse(
    Guid Id,
    Guid WorkpieceId,
    int SequenceNumber,
    string MachineId,
    string Type,
    string Status,
    int ProgressPercentage,
    string? CurrentPhase,
    string? FailureReason,
    string MachineRuntimeStatus,
    MachineAlarmResponse? ActiveBlockingAlarm,
    bool CanResume,
    bool CanPause,
    bool CanFault,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    LaserCutConfigurationResponse Configuration
);
