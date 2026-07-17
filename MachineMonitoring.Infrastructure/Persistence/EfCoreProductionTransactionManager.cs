using MachineMonitoring.Application.Production;
using Microsoft.EntityFrameworkCore.Storage;

namespace MachineMonitoring.Infrastructure.Persistence;

public sealed class EfCoreProductionTransactionManager : IProductionTransactionManager
{
    private readonly MachineMonitoringDbContext _dbContext;
    private readonly IBufferedProductionNotificationPublisher _notificationPublisher;

    public EfCoreProductionTransactionManager(
        MachineMonitoringDbContext dbContext,
        IBufferedProductionNotificationPublisher notificationPublisher
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(notificationPublisher);

        _dbContext = dbContext;
        _notificationPublisher = notificationPublisher;
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

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );
        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await _notificationPublisher.FlushAsync(cancellationToken);
        }
        catch
        {
            _notificationPublisher.Reset();
            throw;
        }
    }
}
