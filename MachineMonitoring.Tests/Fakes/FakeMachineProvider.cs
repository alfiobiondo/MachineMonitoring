using MachineMonitoring.Application;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Fakes;

public class FakeMachineProvider : IMachineProvider
{
    private readonly IReadOnlyCollection<Machine> _machines;

    public FakeMachineProvider(IReadOnlyCollection<Machine> machines)
    {
        ArgumentNullException.ThrowIfNull(machines);

        _machines = machines;
    }

    public Task<IReadOnlyCollection<Machine>> GetMachinesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_machines);
    }
}
