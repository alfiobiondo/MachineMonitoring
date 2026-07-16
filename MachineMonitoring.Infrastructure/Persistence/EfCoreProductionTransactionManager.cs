using MachineMonitoring.Application.Production;
using Microsoft.EntityFrameworkCore.Storage;

namespace MachineMonitoring.Infrastructure.Persistence;

public sealed class EfCoreProductionTransactionManager : IProductionTransactionManager
{
    private readonly MachineMonitoringDbContext _dbContext;

    public EfCoreProductionTransactionManager(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
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

        await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
