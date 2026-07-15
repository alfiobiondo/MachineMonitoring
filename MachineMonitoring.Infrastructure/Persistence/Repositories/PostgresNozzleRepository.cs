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
}
