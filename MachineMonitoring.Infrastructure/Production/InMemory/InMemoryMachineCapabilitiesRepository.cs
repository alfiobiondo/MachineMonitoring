using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Production.InMemory;

public sealed class InMemoryMachineCapabilitiesRepository : IMachineCapabilitiesRepository
{
    private readonly Dictionary<string, MachineCapabilities> _capabilitiesByMachineId;

    public InMemoryMachineCapabilitiesRepository(InMemoryProductionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _capabilitiesByMachineId = catalog.MachineCapabilities.ToDictionary(
            item => item.MachineId,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public Task<MachineCapabilities?> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        cancellationToken.ThrowIfCancellationRequested();

        _capabilitiesByMachineId.TryGetValue(machineId, out MachineCapabilities? capabilities);

        return Task.FromResult(capabilities);
    }
}
