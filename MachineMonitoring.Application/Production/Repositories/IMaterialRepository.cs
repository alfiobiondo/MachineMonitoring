using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IMaterialRepository
{
    Task<Material?> GetByIdAsync(Guid materialId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Material>> GetAllAsync(
        bool enabledOnly,
        CancellationToken cancellationToken
    );
}
