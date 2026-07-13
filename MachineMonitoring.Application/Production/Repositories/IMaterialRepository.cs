using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IMaterialRepository
{
    Task<Material?> GetByIdAsync(Guid materialId, CancellationToken cancellationToken);
}
