using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class MachineOperationEventRecord
{
    public Guid Id { get; set; }

    public Guid MachineOperationId { get; set; }

    public MachineOperationEventType EventType { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public MachineOperationStatus? PreviousStatus { get; set; }

    public MachineOperationStatus? NewStatus { get; set; }

    public int? ProgressPercentage { get; set; }

    public string? Phase { get; set; }

    public string? Reason { get; set; }

    public Guid? MachineAlarmId { get; set; }

    public string? Metadata { get; set; }

    public MachineOperationRecord MachineOperation { get; set; } = null!;

    public MachineAlarmRecord? MachineAlarm { get; set; }
}
