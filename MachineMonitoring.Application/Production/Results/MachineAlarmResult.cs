using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record MachineAlarmResult(
    Guid Id,
    string MachineId,
    Guid? MachineOperationId,
    string Code,
    MachineAlarmSeverity Severity,
    MachineAlarmStatus Status,
    string Message,
    DateTimeOffset RaisedAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNotes
);
