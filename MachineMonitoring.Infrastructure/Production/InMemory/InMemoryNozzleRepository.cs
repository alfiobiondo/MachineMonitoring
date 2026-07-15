using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Production.InMemory;

public sealed class InMemoryNozzleRepository : INozzleRepository
{
    private readonly Dictionary<Guid, Nozzle> _nozzles;

    public InMemoryNozzleRepository(InMemoryProductionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _nozzles = catalog.Nozzles.ToDictionary(nozzle => nozzle.Id);
    }

    public Task<Nozzle?> GetByIdAsync(Guid nozzleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _nozzles.TryGetValue(nozzleId, out Nozzle? nozzle);

        return Task.FromResult(nozzle);
    }

    public Task<IReadOnlyCollection<Nozzle>> GetAllAsync(
        bool availableOnly,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<Nozzle> query = _nozzles.Values;

        if (availableOnly)
        {
            query = query.Where(nozzle => nozzle.IsAvailable);
        }

        IReadOnlyCollection<Nozzle> nozzles = query.OrderBy(nozzle => nozzle.Code).ToArray();

        return Task.FromResult(nozzles);
    }
}
