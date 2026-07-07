using MachineMonitoring.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Console;

public class MachinePollingWorker : BackgroundService
{
    private readonly MachinePollingService _pollingService;
    private readonly ILogger<MachinePollingWorker> _logger;

    public MachinePollingWorker(
        MachinePollingService pollingService,
        ILogger<MachinePollingWorker> logger
    )
    {
        ArgumentNullException.ThrowIfNull(pollingService);
        ArgumentNullException.ThrowIfNull(logger);

        _pollingService = pollingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Machine polling worker started.");

        try
        {
            await _pollingService.RunAsync(stoppingToken);
        }
        finally
        {
            _logger.LogInformation("Machine polling worker stopped.");
        }
    }
}
