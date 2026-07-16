using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IProductionLotRepository
{
    Task<ProductionLot?> GetByIdAsync(Guid productionLotId, CancellationToken cancellationToken);

    Task AddAsync(ProductionLot productionLot, CancellationToken cancellationToken);

    Task UpdateAsync(ProductionLot productionLot, CancellationToken cancellationToken);
}
