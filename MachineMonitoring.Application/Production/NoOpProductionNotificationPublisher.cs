using MachineMonitoring.Application.Production.Notifications;

namespace MachineMonitoring.Application.Production;

public sealed class NoOpProductionNotificationPublisher : IBufferedProductionNotificationPublisher
{
    public Task PublishAsync(
        ProductionNotification notification,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(notification);

        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Reset() { }
}
