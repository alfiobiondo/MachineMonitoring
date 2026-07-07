using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MachineMonitoring.Domain;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application;

public class MachineManager
{
    private readonly IMachineProvider _machineProvider;
    private readonly MachineFormatter _machineFormatter;
    private readonly ILogger<MachineManager> _logger;

    public MachineManager(
        IMachineProvider machineProvider,
        MachineFormatter machineFormatter,
        ILogger<MachineManager> logger
    )
    {
        ArgumentNullException.ThrowIfNull(machineProvider);
        ArgumentNullException.ThrowIfNull(machineFormatter);
        ArgumentNullException.ThrowIfNull(logger);

        _machineProvider = machineProvider;
        _machineFormatter = machineFormatter;
        _logger = logger;
    }

    public Task<Machine> GetMachineAsync(CancellationToken cancellationToken)
    {
        return _machineProvider.GetMachineAsync(cancellationToken);
    }

    public async Task<string> GetMachineDescriptionAsync(CancellationToken cancellationToken)
    {
        Machine machine = await _machineProvider.GetMachineAsync(cancellationToken);

        return _machineFormatter.Format(machine);
    }

    public async Task<string> GetDetailedMachineDescriptionAsync(
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug("Retrieving machine asynchronously for detailed description.");

        Machine machine = await _machineProvider.GetMachineAsync(cancellationToken);

        if (machine.Status == MachineStatus.Alarm)
        {
            _logger.LogWarning("Machine {MachineId} is in alarm state.", machine.Id);

            throw new InvalidMachineStateException($"Machine {machine.Id} is in alarm state.");
        }
        else if (machine.Status == MachineStatus.Offline)
        {
            _logger.LogWarning("Machine {MachineId} is offline.", machine.Id);
        }

        return _machineFormatter.FormatDetailed(machine);
    }
}
