using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Notifications;

namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public sealed class ScopedProductionNotificationCollector : IProductionNotificationCollector
{
    private readonly List<ProductionNotification> _pending = [];

    public Task PublishAsync(
        ProductionNotification notification,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(notification);

        cancellationToken.ThrowIfCancellationRequested();
        _pending.Add(notification);
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<ProductionNotification> GetPending()
    {
        return _pending.ToArray();
    }

    public void Clear()
    {
        _pending.Clear();
    }
}
