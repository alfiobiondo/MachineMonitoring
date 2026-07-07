using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MachineMonitoring.Application;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Infrastructure;

public class FixedMachineProvider : IMachineProvider
{
    public Task<IReadOnlyCollection<Machine>> GetMachinesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<Machine> machines = new List<Machine>
        {
            new(
                id: "M-002",
                name: "Tube Bender",
                status: MachineStatus.Idle,
                location: "Production Hall B",
                serialNumber: "SN-2026-002"
            ),
        };

        return Task.FromResult(machines);
    }
}
