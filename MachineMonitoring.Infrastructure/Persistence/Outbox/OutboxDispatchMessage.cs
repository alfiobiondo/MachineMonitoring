namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public sealed record OutboxDispatchMessage(
    Guid Id,
    string Type,
    string Payload,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt
);
