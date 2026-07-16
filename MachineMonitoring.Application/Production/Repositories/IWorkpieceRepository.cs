using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IWorkpieceRepository
{
    Task<Workpiece?> GetByIdAsync(Guid workpieceId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Workpiece>> GetByProductionLotIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    );

    Task AddAsync(Workpiece workpiece, CancellationToken cancellationToken);

    Task UpdateAsync(Workpiece workpiece, CancellationToken cancellationToken);
}
