using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Infrastructure.Persistence.Outbox;

namespace MachineMonitoring.Tests.Production;

public sealed class ProductionNotificationCollectorTests
{
    [Fact]
    public async Task PublishAsync_ThenGetPending_ReturnsImmutableSnapshot()
    {
        ScopedProductionNotificationCollector collector = new();
        OperationStatusChangedNotification notification = new(
            Guid.NewGuid(),
            Domain.Production.MachineOperationStatus.Running,
            DateTimeOffset.UtcNow
        );

        await collector.PublishAsync(notification, CancellationToken.None);

        IReadOnlyCollection<ProductionNotification> snapshot = collector.GetPending();

        Assert.Single(snapshot);
        Assert.Same(notification, Assert.Single(snapshot));
        Assert.NotSame(snapshot, collector.GetPending());
    }

    [Fact]
    public async Task Clear_RemovesPendingNotifications()
    {
        ScopedProductionNotificationCollector collector = new();

        await collector.PublishAsync(
            new MachineAlarmResolvedNotification(Guid.NewGuid(), "M-001", DateTimeOffset.UtcNow),
            CancellationToken.None
        );

        collector.Clear();

        Assert.Empty(collector.GetPending());
    }
}
