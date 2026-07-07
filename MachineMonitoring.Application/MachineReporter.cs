using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application;

public class MachineReporter
{
    private readonly MachineManager _machineManager;
    private readonly ILogger<MachineReporter> _logger;

    public MachineReporter(MachineManager machineManager, ILogger<MachineReporter> logger)
    {
        ArgumentNullException.ThrowIfNull(machineManager);
        ArgumentNullException.ThrowIfNull(logger);

        _machineManager = machineManager;
        _logger = logger;
    }

    public async Task PrintReportAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Machine report generation started.");

        string description = await _machineManager.GetDetailedMachineDescriptionAsync(
            cancellationToken
        );

        Console.WriteLine($"=== MACHINE REPORT === \n{description} \n======================");

        _logger.LogInformation("Machine report generation completed.");
    }
}
