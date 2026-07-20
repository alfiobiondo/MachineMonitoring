namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public sealed record OutboxProcessingResult(int AttemptedCount, int SucceededCount, int FailedCount);
