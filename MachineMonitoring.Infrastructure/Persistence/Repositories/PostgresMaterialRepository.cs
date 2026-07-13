using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresMaterialRepository : IMaterialRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresMaterialRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<Material?> GetByIdAsync(Guid materialId, CancellationToken cancellationToken)
    {
        return await _dbContext
            .Materials.AsNoTracking()
            .SingleOrDefaultAsync(material => material.Id == materialId, cancellationToken);
    }
}
