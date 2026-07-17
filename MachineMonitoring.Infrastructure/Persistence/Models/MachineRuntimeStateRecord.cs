using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class MachineRuntimeStateRecord
{
    public string MachineId { get; set; } = string.Empty;

    public MachineRuntimeStatus Status { get; set; }

    public Guid? CurrentOperationId { get; set; }

    public DateTimeOffset LastChangedAt { get; set; }

    public string? FailureReason { get; set; }

    public Guid? ActiveAlarmId { get; set; }

    public int Version { get; set; }

    public MachineOperationRecord? CurrentOperation { get; set; }

    public MachineAlarmRecord? ActiveAlarm { get; set; }
}
