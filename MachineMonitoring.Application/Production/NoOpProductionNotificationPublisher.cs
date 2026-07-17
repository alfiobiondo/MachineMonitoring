using MachineMonitoring.Application.Production.Notifications;

namespace MachineMonitoring.Application.Production;

public sealed class NoOpProductionNotificationPublisher : IProductionNotificationPublisher
{
    public Task PublishAsync(
        ProductionNotification notification,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(notification);

        return Task.CompletedTask;
    }
}
