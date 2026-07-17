using MachineMonitoring.Application.Production.Notifications;

namespace MachineMonitoring.Application.Production;

public interface IProductionNotificationPublisher
{
    Task PublishAsync(
        ProductionNotification notification,
        CancellationToken cancellationToken
    );
}
