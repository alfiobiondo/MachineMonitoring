namespace MachineMonitoring.Api.Realtime;

public sealed record MachineOperationEventAppendedEvent(
    Guid OutboxMessageId,
    Guid EventId,
    Guid OperationId,
    string MachineId,
    string EventType,
    DateTimeOffset OccurredAt
);
