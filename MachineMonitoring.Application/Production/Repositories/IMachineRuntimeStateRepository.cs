using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IMachineRuntimeStateRepository
{
    Task<MachineRuntimeState?> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyCollection<MachineRuntimeState>> GetAllAsync(CancellationToken cancellationToken);

    Task AddAsync(MachineRuntimeState state, CancellationToken cancellationToken);

    Task UpdateAsync(MachineRuntimeState state, CancellationToken cancellationToken);
}
