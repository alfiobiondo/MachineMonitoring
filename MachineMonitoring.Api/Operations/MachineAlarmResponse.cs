namespace MachineMonitoring.Api.Operations;

public sealed record MachineAlarmResponse(
    Guid Id,
    string MachineId,
    Guid? MachineOperationId,
    string Code,
    string Severity,
    string Status,
    string Message,
    DateTimeOffset RaisedAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNotes
);
