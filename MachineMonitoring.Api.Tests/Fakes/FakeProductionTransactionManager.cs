using MachineMonitoring.Application.Production;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class FakeProductionTransactionManager : IProductionTransactionManager
{
    private readonly IBufferedProductionNotificationPublisher _notificationPublisher;

    public FakeProductionTransactionManager(
        IBufferedProductionNotificationPublisher notificationPublisher
    )
    {
        ArgumentNullException.ThrowIfNull(notificationPublisher);

        _notificationPublisher = notificationPublisher;
    }

    public Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteInternalAsync(operation, cancellationToken);
    }

    private async Task ExecuteInternalAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await operation(cancellationToken);
            await _notificationPublisher.FlushAsync(cancellationToken);
        }
        catch
        {
            _notificationPublisher.Reset();
            throw;
        }
    }
}
