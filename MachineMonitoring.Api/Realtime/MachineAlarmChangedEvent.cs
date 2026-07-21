namespace MachineMonitoring.Api.Realtime;

public sealed record MachineAlarmChangedEvent(
    Guid EventId,
    string ChangeKind,
    Guid AlarmId,
    string MachineId,
    Guid? MachineOperationId,
    string Code,
    string Severity,
    string Status,
    string Message,
    bool IsBlocking,
    DateTimeOffset RaisedAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNotes,
    DateTimeOffset OccurredAt
);
