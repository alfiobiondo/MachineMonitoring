using MachineMonitoring.Application;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Infrastructure;

public class MockMachineProvider : IMachineProvider
{
    public Task<IReadOnlyCollection<Machine>> GetMachinesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<Machine> machines = new List<Machine>
        {
            new(
                id: "M-001",
                name: "Laser Cutter",
                status: MachineStatus.Running,
                location: "Mock Area",
                serialNumber: "SN-2026-001"
            ),
            new(
                id: "M-002",
                name: "Tube Bender",
                status: MachineStatus.Idle,
                location: "Mock Area",
                serialNumber: "SN-2026-002"
            ),
        };

        return Task.FromResult(machines);
    }
}
