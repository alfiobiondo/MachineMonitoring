using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresNozzleRepository : INozzleRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresNozzleRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<Nozzle?> GetByIdAsync(Guid nozzleId, CancellationToken cancellationToken)
    {
        return await _dbContext
            .Nozzles.AsNoTracking()
            .SingleOrDefaultAsync(nozzle => nozzle.Id == nozzleId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Nozzle>> GetAllAsync(
        bool availableOnly,
        CancellationToken cancellationToken
    )
    {
        IQueryable<Nozzle> query = _dbContext.Nozzles.AsNoTracking();

        if (availableOnly)
        {
            query = query.Where(nozzle => nozzle.IsAvailable);
        }

        return await query.OrderBy(nozzle => nozzle.Code).ToArrayAsync(cancellationToken);
    }
}
