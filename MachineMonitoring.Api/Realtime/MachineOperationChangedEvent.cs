namespace MachineMonitoring.Api.Realtime;

public sealed record MachineOperationChangedEvent(
    Guid EventId,
    string ChangeKind,
    Guid OperationId,
    Guid WorkpieceId,
    string MachineId,
    int SequenceNumber,
    string Type,
    string Status,
    int ProgressPercentage,
    string? CurrentPhase,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset OccurredAt
);
