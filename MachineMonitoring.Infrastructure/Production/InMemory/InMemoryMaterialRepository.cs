using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Production.InMemory;

public sealed class InMemoryMaterialRepository : IMaterialRepository
{
    private readonly Dictionary<Guid, Material> _materials;

    public InMemoryMaterialRepository(InMemoryProductionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _materials = catalog.Materials.ToDictionary(material => material.Id);
    }

    public Task<Material?> GetByIdAsync(Guid materialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _materials.TryGetValue(materialId, out Material? material);

        return Task.FromResult(material);
    }
}
