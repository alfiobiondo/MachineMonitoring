using MachineMonitoring.Application.Production;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class FakeProductionTransactionManager : IProductionTransactionManager
{
    private readonly IProductionNotificationCollector _notificationCollector;

    public FakeProductionTransactionManager(
        IProductionNotificationCollector notificationCollector
    )
    {
        ArgumentNullException.ThrowIfNull(notificationCollector);

        _notificationCollector = notificationCollector;
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
        }
        finally
        {
            _notificationCollector.Clear();
        }
    }
}
