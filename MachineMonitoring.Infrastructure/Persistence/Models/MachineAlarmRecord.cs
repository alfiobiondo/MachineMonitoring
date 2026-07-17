using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class MachineAlarmRecord
{
    public Guid Id { get; set; }

    public string MachineId { get; set; } = string.Empty;

    public Guid? MachineOperationId { get; set; }

    public string Code { get; set; } = string.Empty;

    public MachineAlarmSeverity Severity { get; set; }

    public MachineAlarmStatus Status { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset RaisedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? ResolutionNotes { get; set; }

    public MachineOperationRecord? MachineOperation { get; set; }

    public List<MachineOperationEventRecord> OperationEvents { get; set; } = [];
}
