using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Application;

public interface IMachineProvider
{
    Task<Machine> GetMachineAsync(CancellationToken cancellationToken);
}
