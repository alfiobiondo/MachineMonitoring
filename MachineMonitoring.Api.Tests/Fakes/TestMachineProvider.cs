using MachineMonitoring.Application;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class TestMachineProvider : IMachineProvider
{
    private readonly Dictionary<string, Machine> _items = new(StringComparer.OrdinalIgnoreCase);

    public TestMachineProvider()
    {
        Seed(
            new Machine(
                id: "M-001",
                name: "Laser Cutter",
                status: MachineStatus.Running,
                location: "Production Hall A",
                serialNumber: "SN-2026-001"
            )
        );
        Seed(
            new Machine(
                id: "M-002",
                name: "Tube Bender",
                status: MachineStatus.Idle,
                location: "Production Hall B",
                serialNumber: "SN-2026-002"
            )
        );
    }

    public void Seed(Machine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _items[machine.Id] = machine;
    }

    public Task<IReadOnlyCollection<Machine>> GetMachinesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<Machine>>(_items.Values.OrderBy(item => item.Id).ToArray());
    }
}
