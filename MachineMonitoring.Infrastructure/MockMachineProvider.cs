using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MachineMonitoring.Application;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Infrastructure;

public class MockMachineProvider : IMachineProvider
{
    public Task<Machine> GetMachineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Machine machine = new(
            id: "M-001",
            name: "Laser Cutter",
            status: MachineStatus.Running,
            location: "Mock Area",
            serialNumber: "SN-2026-001"
        );

        return Task.FromResult(machine);
    }
}
