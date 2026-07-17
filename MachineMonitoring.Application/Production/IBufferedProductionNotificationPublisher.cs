namespace MachineMonitoring.Application.Production;

public interface IBufferedProductionNotificationPublisher : IProductionNotificationPublisher
{
    Task FlushAsync(CancellationToken cancellationToken);

    void Reset();
}
