namespace MachineMonitoring.Domain.Production;

public sealed class MachineOperationEvent
{
    public Guid Id { get; }

    public Guid MachineOperationId { get; }

    public MachineOperationEventType EventType { get; }

    public DateTimeOffset OccurredAt { get; }

    public MachineOperationStatus? PreviousStatus { get; }

    public MachineOperationStatus? NewStatus { get; }

    public int? ProgressPercentage { get; }

    public string? Phase { get; }

    public string? Reason { get; }

    public Guid? MachineAlarmId { get; }

    public string? Metadata { get; }

    public MachineOperationEvent(
        Guid id,
        Guid machineOperationId,
        MachineOperationEventType eventType,
        DateTimeOffset occurredAt,
        MachineOperationStatus? previousStatus,
        MachineOperationStatus? newStatus,
        int? progressPercentage,
        string? phase,
        string? reason,
        Guid? machineAlarmId,
        string? metadata
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The machine operation event ID cannot be empty.", nameof(id));
        }

        if (machineOperationId == Guid.Empty)
        {
            throw new ArgumentException(
                "The machine operation ID cannot be empty.",
                nameof(machineOperationId)
            );
        }

        Id = id;
        MachineOperationId = machineOperationId;
        EventType = eventType;
        OccurredAt = occurredAt;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ProgressPercentage = progressPercentage;
        Phase = phase;
        Reason = reason;
        MachineAlarmId = machineAlarmId;
        Metadata = metadata;
    }
}
