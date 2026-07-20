namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public interface IOutboxMessageDispatcher
{
    Task DispatchAsync(OutboxDispatchMessage message, CancellationToken cancellationToken);
}
