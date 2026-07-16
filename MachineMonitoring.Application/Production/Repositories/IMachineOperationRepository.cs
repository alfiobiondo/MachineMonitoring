using MachineMonitoring.Application.Common;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IMachineOperationRepository
{
    Task<MachineOperation?> GetByIdAsync(Guid operationId, CancellationToken cancellationToken);

    Task<PagedResult<MachineOperation>> GetAllAsync(
        string? machineId,
        MachineOperationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

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

    Task<MachineOperation?> GetNextQueuedAsync(CancellationToken cancellationToken);
}
