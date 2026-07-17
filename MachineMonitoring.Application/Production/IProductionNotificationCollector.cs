using MachineMonitoring.Application.Production.Notifications;

namespace MachineMonitoring.Application.Production;

public interface IProductionNotificationCollector : IProductionNotificationPublisher
{
    IReadOnlyCollection<ProductionNotification> GetPending();

    void Clear();
}
