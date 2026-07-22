namespace MachineMonitoring.Api.Realtime;

public sealed record MachineRuntimeChangedEvent(
    Guid EventId,
    string MachineId,
    string Status,
    Guid? CurrentOperationId,
    DateTimeOffset LastChangedAt,
    string? FailureReason,
    Guid? ActiveAlarmId,
    int Version,
    DateTimeOffset OccurredAt
);
