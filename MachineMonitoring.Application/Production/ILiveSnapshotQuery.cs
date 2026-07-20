using MachineMonitoring.Application.Production.Results;

namespace MachineMonitoring.Application.Production;

public interface ILiveSnapshotQuery
{
    Task<LiveSnapshotResult> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    );
}
