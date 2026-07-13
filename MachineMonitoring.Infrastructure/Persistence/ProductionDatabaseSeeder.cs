using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence;

public sealed class ProductionDatabaseSeeder
{
    private readonly MachineMonitoringDbContext _dbContext;

    public ProductionDatabaseSeeder(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await _dbContext.Materials.AnyAsync(cancellationToken))
        {
            _dbContext.Materials.AddRange(InMemoryProductionData.CreateMaterials());
        }

        if (!await _dbContext.Nozzles.AnyAsync(cancellationToken))
        {
            _dbContext.Nozzles.AddRange(InMemoryProductionData.CreateNozzles());
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
