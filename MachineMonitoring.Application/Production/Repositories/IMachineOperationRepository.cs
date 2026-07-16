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

    Task<IReadOnlyCollection<MachineOperation>> GetOrderedByWorkpieceIdAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    );

    Task<bool> ExistsIncompletePredecessorAsync(
        Guid workpieceId,
        int sequenceNumber,
        CancellationToken cancellationToken
    );

    Task<MachineOperation?> GetFirstExecutableQueuedByWorkpieceIdAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyCollection<MachineOperation>> GetRunningOperationsAsync(
        CancellationToken cancellationToken
    );

    Task AddAsync(
        MachineOperation operation,
        LaserCutConfiguration configuration,
        CancellationToken cancellationToken
    );

    Task UpdateAsync(MachineOperation operation, CancellationToken cancellationToken);
}
