using MachineMonitoring.Domain;

namespace MachineMonitoring.Application;

public interface IMachineProvider
{
    Task<IReadOnlyCollection<Machine>> GetMachinesAsync(CancellationToken cancellationToken);
}
