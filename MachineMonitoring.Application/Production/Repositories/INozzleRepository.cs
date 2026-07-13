using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Repositories;

public interface INozzleRepository
{
    Task<Nozzle?> GetByIdAsync(Guid nozzleId, CancellationToken cancellationToken);
}
