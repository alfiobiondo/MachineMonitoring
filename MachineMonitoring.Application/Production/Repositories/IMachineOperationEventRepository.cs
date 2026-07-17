using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IMachineOperationEventRepository
{
    Task AddAsync(
        MachineOperationEvent machineOperationEvent,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyCollection<MachineOperationEvent>> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyCollection<MachineOperationEvent>> GetByWorkpieceIdAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyCollection<MachineOperationEvent>> GetByProductionLotIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    );
}
