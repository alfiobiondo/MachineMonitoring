using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MachineMonitoring.Domain;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application;

public class MachineFormatter
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    private readonly ILogger<MachineFormatter> _logger;

    public MachineFormatter(ILogger<MachineFormatter> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public string Format(Machine machine)
    {
        return $"[{machine.Id}] {machine.Name} - {machine.Status}";
    }

    public string FormatDetailed(Machine machine)
    {
        _logger.LogDebug("Formatting detailed description for machine {MachineId}.", machine.Id);

        return $"Machine {machine.Id} ({machine.SerialNumber}) named \"{machine.Name}\" "
            + $"is currently {machine.Status} in {machine.Location}.";
    }
}
