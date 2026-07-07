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

    public async Task<IReadOnlyCollection<Machine>> GetMachinesAsync(
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(millisecondsDelay: 500, cancellationToken);

        Machine machine = new(
            id: _options.Id,
            name: _options.Name,
            status: _options.Status!.Value,
            location: _options.Location,
            serialNumber: _options.SerialNumber
        );

        return new List<Machine> { machine };
    }
}
