using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MachineMonitoring.Application;
using MachineMonitoring.Domain;
using MachineMonitoring.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Infrastructure;

public class ConfigurationMachineProvider : IMachineProvider
{
    private readonly MachineOptions _options;

    private readonly ILogger<ConfigurationMachineProvider> _logger;

    public ConfigurationMachineProvider(
        IOptions<MachineOptions> options,
        ILogger<ConfigurationMachineProvider> logger
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    public async Task<Machine> GetMachineAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting asynchronous retrieval of machine {MachineId}.", _options.Id);

        await Task.Delay(millisecondsDelay: 500, cancellationToken);

        Machine machine = new Machine(
            id: _options.Id,
            name: _options.Name,
            status: _options.Status!.Value,
            location: _options.Location
        );

        _logger.LogInformation(
            "Machine {MachineId} named {MachineName} loaded asynchronously.",
            machine.Id,
            machine.Name
        );

        return machine;
    }
}
