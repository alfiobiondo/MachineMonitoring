using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class PostgresOutboxTransactionTests
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresOutboxTransactionTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_PersistsProductionDataAndOutboxAtomically()
    {
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero));
        Guid productionLotId = Guid.NewGuid();
        Guid alarmId = Guid.NewGuid();
        DateTimeOffset occurredAt = new(2026, 7, 17, 11, 0, 0, TimeSpan.Zero);

        await using WebApplicationFactory<Program> factory = CreateFactory(timeProvider);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        IProductionTransactionManager transactionManager =
            scope.ServiceProvider.GetRequiredService<IProductionTransactionManager>();
        IProductionNotificationPublisher publisher =
            scope.ServiceProvider.GetRequiredService<IProductionNotificationPublisher>();
        IProductionNotificationCollector collector =
            scope.ServiceProvider.GetRequiredService<IProductionNotificationCollector>();
        IProductionLotRepository productionLotRepository =
            scope.ServiceProvider.GetRequiredService<IProductionLotRepository>();

        await transactionManager.ExecuteAsync(
            async ct =>
            {
                await productionLotRepository.AddAsync(
                    new ProductionLot(productionLotId, $"LOT-OUTBOX-{productionLotId:N}", 1, occurredAt),
                    ct
                );

                await publisher.PublishAsync(
                    new MachineAlarmAcknowledgedNotification(alarmId, "M-001", occurredAt),
                    ct
                );
            },
            CancellationToken.None
        );

        Assert.Empty(collector.GetPending());

        await using AsyncServiceScope verificationScope = _factory.Services.CreateAsyncScope();
        MachineMonitoringDbContext dbContext =
            verificationScope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        Assert.NotNull(
            await dbContext.ProductionLots.SingleOrDefaultAsync(
                item => item.Id == productionLotId,
                CancellationToken.None
            )
        );

        List<OutboxMessageRecord> persistedMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        OutboxMessageRecord outboxMessage = Assert.Single(
            persistedMessages,
            item => item.Payload.Contains(alarmId.ToString(), StringComparison.Ordinal)
        );

        Assert.Equal("machine-alarm-acknowledged.v1", outboxMessage.Type);
        Assert.Equal(occurredAt, outboxMessage.OccurredAt);
        Assert.Equal(timeProvider.GetUtcNow(), outboxMessage.CreatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationFails_RollsBackProductionDataAndOutboxAndClearsCollector()
    {
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 7, 17, 13, 0, 0, TimeSpan.Zero));
        Guid productionLotId = Guid.NewGuid();
        Guid alarmId = Guid.NewGuid();
        DateTimeOffset occurredAt = new(2026, 7, 17, 12, 30, 0, TimeSpan.Zero);

        await using WebApplicationFactory<Program> factory = CreateFactory(timeProvider);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        IProductionTransactionManager transactionManager =
            scope.ServiceProvider.GetRequiredService<IProductionTransactionManager>();
        IProductionNotificationPublisher publisher =
            scope.ServiceProvider.GetRequiredService<IProductionNotificationPublisher>();
        IProductionNotificationCollector collector =
            scope.ServiceProvider.GetRequiredService<IProductionNotificationCollector>();
        IProductionLotRepository productionLotRepository =
            scope.ServiceProvider.GetRequiredService<IProductionLotRepository>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transactionManager.ExecuteAsync(
                async ct =>
                {
                    await productionLotRepository.AddAsync(
                        new ProductionLot(
                            productionLotId,
                            $"LOT-ROLLBACK-{productionLotId:N}",
                            1,
                            occurredAt
                        ),
                        ct
                    );

                    await publisher.PublishAsync(
                        new MachineAlarmAcknowledgedNotification(alarmId, "M-001", occurredAt),
                        ct
                    );

                    throw new InvalidOperationException("boom");
                },
                CancellationToken.None
            )
        );

        Assert.Empty(collector.GetPending());

        await using AsyncServiceScope verificationScope = _factory.Services.CreateAsyncScope();
        MachineMonitoringDbContext dbContext =
            verificationScope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        Assert.Null(
            await dbContext.ProductionLots.SingleOrDefaultAsync(
                item => item.Id == productionLotId,
                CancellationToken.None
            )
        );
        Assert.DoesNotContain(
            await dbContext.OutboxMessages.AsNoTracking().ToListAsync(CancellationToken.None),
            item => item.Payload.Contains(alarmId.ToString(), StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransactionIsNested_PersistsAllNotificationsOnceFromOuterTransaction()
    {
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 7, 17, 14, 0, 0, TimeSpan.Zero));
        Guid outerProductionLotId = Guid.NewGuid();
        Guid innerProductionLotId = Guid.NewGuid();
        Guid outerAlarmId = Guid.NewGuid();
        Guid innerAlarmId = Guid.NewGuid();
        DateTimeOffset occurredAt = new(2026, 7, 17, 13, 30, 0, TimeSpan.Zero);

        await using WebApplicationFactory<Program> factory = CreateFactory(timeProvider);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        IProductionTransactionManager transactionManager =
            scope.ServiceProvider.GetRequiredService<IProductionTransactionManager>();
        IProductionNotificationPublisher publisher =
            scope.ServiceProvider.GetRequiredService<IProductionNotificationPublisher>();
        IProductionNotificationCollector collector =
            scope.ServiceProvider.GetRequiredService<IProductionNotificationCollector>();
        IProductionLotRepository productionLotRepository =
            scope.ServiceProvider.GetRequiredService<IProductionLotRepository>();

        await transactionManager.ExecuteAsync(
            async ct =>
            {
                await productionLotRepository.AddAsync(
                    new ProductionLot(
                        outerProductionLotId,
                        $"LOT-OUTER-{outerProductionLotId:N}",
                        1,
                        occurredAt
                    ),
                    ct
                );

                await publisher.PublishAsync(
                    new MachineAlarmAcknowledgedNotification(outerAlarmId, "M-001", occurredAt),
                    ct
                );

                await transactionManager.ExecuteAsync(
                    async nestedCt =>
                    {
                        await productionLotRepository.AddAsync(
                            new ProductionLot(
                                innerProductionLotId,
                                $"LOT-INNER-{innerProductionLotId:N}",
                                1,
                                occurredAt
                            ),
                            nestedCt
                        );

                        await publisher.PublishAsync(
                            new MachineAlarmAcknowledgedNotification(
                                innerAlarmId,
                                "M-002",
                                occurredAt
                            ),
                            nestedCt
                        );
                    },
                    ct
                );
            },
            CancellationToken.None
        );

        Assert.Empty(collector.GetPending());

        await using AsyncServiceScope verificationScope = _factory.Services.CreateAsyncScope();
        MachineMonitoringDbContext dbContext =
            verificationScope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        Assert.NotNull(
            await dbContext.ProductionLots.SingleOrDefaultAsync(
                item => item.Id == outerProductionLotId,
                CancellationToken.None
            )
        );
        Assert.NotNull(
            await dbContext.ProductionLots.SingleOrDefaultAsync(
                item => item.Id == innerProductionLotId,
                CancellationToken.None
            )
        );

        List<OutboxMessageRecord> outboxMessages = (
            await dbContext.OutboxMessages.AsNoTracking().ToListAsync(CancellationToken.None)
        )
            .Where(item =>
                item.Payload.Contains(outerAlarmId.ToString(), StringComparison.Ordinal)
                || item.Payload.Contains(innerAlarmId.ToString(), StringComparison.Ordinal)
            )
            .ToList();

        Assert.Equal(2, outboxMessages.Count);
        Assert.Single(outboxMessages, item => item.Payload.Contains(outerAlarmId.ToString()));
        Assert.Single(outboxMessages, item => item.Payload.Contains(innerAlarmId.ToString()));
    }

    private WebApplicationFactory<Program> CreateFactory(FixedTimeProvider timeProvider)
    {
        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(timeProvider);
            })
        );
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
