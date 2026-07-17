using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Notifications;
using System.Reflection;

namespace MachineMonitoring.Tests.Production;

public sealed class ProductionNotificationTransactionTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_FlushesBufferedNotifications()
    {
        RecordingBufferedPublisher publisher = new();
        TestProductionTransactionManager transactionManager = new(publisher);

        await transactionManager.ExecuteAsync(
            async ct =>
            {
                await publisher.PublishAsync(
                    new OperationStatusChangedNotification(
                        Guid.NewGuid(),
                        Domain.Production.MachineOperationStatus.Running,
                        DateTimeOffset.UtcNow
                    ),
                    ct
                );

                Assert.Empty(publisher.Published);
                Assert.Single(publisher.Pending);
            },
            CancellationToken.None
        );

        Assert.Single(publisher.Published);
        Assert.Empty(publisher.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationFails_DoesNotPublishNotifications()
    {
        RecordingBufferedPublisher publisher = new();
        TestProductionTransactionManager transactionManager = new(publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transactionManager.ExecuteAsync(
                async ct =>
                {
                    await publisher.PublishAsync(
                        new OperationStatusChangedNotification(
                            Guid.NewGuid(),
                            Domain.Production.MachineOperationStatus.Running,
                            DateTimeOffset.UtcNow
                        ),
                        ct
                    );

                    throw new InvalidOperationException("boom");
                },
                CancellationToken.None
            )
        );

        Assert.Empty(publisher.Published);
        Assert.Empty(publisher.Pending);
    }

    [Fact]
    public void NotificationDtos_DoNotExposeDomainEntitiesOrEfRecords()
    {
        Type[] notificationTypes =
        [
            typeof(OperationStatusChangedNotification),
            typeof(OperationProgressChangedNotification),
            typeof(OperationEventAppendedNotification),
            typeof(MachineAlarmRaisedNotification),
            typeof(MachineAlarmAcknowledgedNotification),
            typeof(MachineAlarmResolvedNotification),
            typeof(MachineRuntimeStatusChangedNotification),
        ];

        foreach (Type notificationType in notificationTypes)
        {
            PropertyInfo[] properties = notificationType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public
            );

            Assert.DoesNotContain(
                properties,
                property =>
                    property.PropertyType.Namespace?.StartsWith(
                        "MachineMonitoring.Infrastructure.Persistence.Models",
                        StringComparison.Ordinal
                    ) == true
            );

            Assert.DoesNotContain(
                properties,
                property => IsDomainEntityType(property.PropertyType)
            );
        }
    }

    private static bool IsDomainEntityType(Type propertyType)
    {
        Type candidate = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        return candidate.Namespace == "MachineMonitoring.Domain.Production"
            && candidate.IsClass
            && candidate != typeof(string);
    }

    private sealed class TestProductionTransactionManager : IProductionTransactionManager
    {
        private readonly IBufferedProductionNotificationPublisher _publisher;

        public TestProductionTransactionManager(IBufferedProductionNotificationPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await operation(cancellationToken);
                await _publisher.FlushAsync(cancellationToken);
            }
            catch
            {
                _publisher.Reset();
                throw;
            }
        }
    }

    private sealed class RecordingBufferedPublisher : IBufferedProductionNotificationPublisher
    {
        public List<ProductionNotification> Pending { get; } = [];

        public List<ProductionNotification> Published { get; } = [];

        public Task PublishAsync(
            ProductionNotification notification,
            CancellationToken cancellationToken
        )
        {
            Pending.Add(notification);
            return Task.CompletedTask;
        }

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            Published.AddRange(Pending);
            Pending.Clear();
            return Task.CompletedTask;
        }

        public void Reset()
        {
            Pending.Clear();
        }
    }
}
