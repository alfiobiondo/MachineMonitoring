using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class TestProductionLotRepository : IProductionLotRepository
{
    private readonly Dictionary<Guid, ProductionLot> _productionLots = [];
    private readonly object _syncRoot = new();

    public void Seed(ProductionLot productionLot)
    {
        ArgumentNullException.ThrowIfNull(productionLot);

        lock (_syncRoot)
        {
            _productionLots[productionLot.Id] = productionLot;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _productionLots.Clear();
        }
    }

    public Task<ProductionLot?> GetByIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _productionLots.TryGetValue(productionLotId, out ProductionLot? productionLot);
            return Task.FromResult(productionLot);
        }
    }

    public Task AddAsync(ProductionLot productionLot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(productionLot);

        lock (_syncRoot)
        {
            _productionLots.Add(productionLot.Id, productionLot);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProductionLot productionLot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(productionLot);

        lock (_syncRoot)
        {
            _productionLots[productionLot.Id] = productionLot;
        }

        return Task.CompletedTask;
    }
}
