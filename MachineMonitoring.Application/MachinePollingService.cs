using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application;

public class MachinePollingService
{
    private readonly MachineManager _machineManager;
    private readonly PollingOptions _options;
    private readonly ILogger<MachinePollingService> _logger;

    public MachinePollingService(
        MachineManager machineManager,
        IOptions<PollingOptions> options,
        ILogger<MachinePollingService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(machineManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _machineManager = machineManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(_options.IntervalSeconds);

        _logger.LogInformation(
            "Machine polling started with interval {PollingIntervalSeconds} seconds.",
            _options.IntervalSeconds
        );

        _logger.LogInformation(
            "Machine polling will start after {InitialDelaySeconds} seconds.",
            _options.InitialDelaySeconds
        );

        using PeriodicTimer timer = new(interval);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), cancellationToken);

            await PollOnceAsync(cancellationToken);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PollOnceAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Machine polling stopped.");
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            string description = await _machineManager.GetDetailedMachineDescriptionAsync(
                cancellationToken
            );

            Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] {description}");
        }
        catch (InvalidMachineStateException exception)
        {
            _logger.LogWarning(exception, "Machine polling detected an invalid machine state.");
        }
        catch (MachineUnavailableException exception)
        {
            _logger.LogError(
                exception,
                "Machine polling could not retrieve the machine. The next polling cycle will retry."
            );
        }
    }
}
