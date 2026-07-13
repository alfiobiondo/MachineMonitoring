using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IMachineOperationRepository
{
    Task<MachineOperation?> GetByIdAsync(Guid operationId, CancellationToken cancellationToken);

    Task<LaserCutConfiguration?> GetConfigurationByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    );

    Task AddAsync(
        MachineOperation operation,
        LaserCutConfiguration configuration,
        CancellationToken cancellationToken
    );

    Task UpdateAsync(MachineOperation operation, CancellationToken cancellationToken);
}
