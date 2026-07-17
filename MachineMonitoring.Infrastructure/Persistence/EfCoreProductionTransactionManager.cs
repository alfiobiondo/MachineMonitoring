using MachineMonitoring.Application.Production;
using Microsoft.EntityFrameworkCore.Storage;
using MachineMonitoring.Infrastructure.Persistence.Outbox;

namespace MachineMonitoring.Infrastructure.Persistence;

public sealed class EfCoreProductionTransactionManager : IProductionTransactionManager
{
    private readonly MachineMonitoringDbContext _dbContext;
    private readonly IProductionNotificationCollector _notificationCollector;
    private readonly ProductionNotificationOutboxSerializer _outboxSerializer;
    private readonly TimeProvider _timeProvider;

    public EfCoreProductionTransactionManager(
        MachineMonitoringDbContext dbContext,
        IProductionNotificationCollector notificationCollector,
        ProductionNotificationOutboxSerializer outboxSerializer,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(notificationCollector);
        ArgumentNullException.ThrowIfNull(outboxSerializer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _notificationCollector = notificationCollector;
        _outboxSerializer = outboxSerializer;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            await operation(cancellationToken);
            return;
        }

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
            IReadOnlyCollection<Application.Production.Notifications.ProductionNotification> pendingNotifications =
                _notificationCollector.GetPending();
            IReadOnlyCollection<Models.OutboxMessageRecord> outboxRecords = _outboxSerializer.Serialize(
                pendingNotifications,
                _timeProvider.GetUtcNow()
            );

            _dbContext.OutboxMessages.AddRange(outboxRecords);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _notificationCollector.Clear();
        }
    }
}
