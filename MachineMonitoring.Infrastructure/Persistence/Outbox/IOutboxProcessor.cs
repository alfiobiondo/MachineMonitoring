namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public interface IOutboxProcessor
{
    Task<OutboxProcessingResult> ProcessPendingAsync(
        int batchSize,
        CancellationToken cancellationToken
    );
}
