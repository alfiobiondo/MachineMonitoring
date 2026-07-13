using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IMachineCapabilitiesRepository
{
    Task<MachineCapabilities?> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    );
}
