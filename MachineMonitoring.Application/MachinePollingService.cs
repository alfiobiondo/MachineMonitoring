using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;
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
            IReadOnlyCollection<Machine> machines = await _machineManager.GetMachinesAsync(
                cancellationToken
            );

            DateTimeOffset currentTime = DateTimeOffset.Now;

            Console.WriteLine(
                $"[{currentTime:HH:mm:ss}] " + $"{machines.Count} machines retrieved."
            );

            foreach (Machine machine in machines)
            {
                if (machine.Status == MachineStatus.Alarm)
                {
                    _logger.LogWarning("Machine {MachineId} is in alarm state.", machine.Id);
                }
                else if (machine.Status == MachineStatus.Offline)
                {
                    _logger.LogWarning("Machine {MachineId} is offline.", machine.Id);
                }

                string description = _machineManager.GetDetailedMachineDescription(machine);

                Console.WriteLine($"- {description}");
            }

            int runningCount = machines.Count(machine => machine.Status == MachineStatus.Running);

            int idleCount = machines.Count(machine => machine.Status == MachineStatus.Idle);

            int offlineCount = machines.Count(machine => machine.Status == MachineStatus.Offline);

            int alarmCount = machines.Count(machine => machine.Status == MachineStatus.Alarm);

            Console.WriteLine(
                $"Status summary: "
                    + $"Running={runningCount}, "
                    + $"Idle={idleCount}, "
                    + $"Offline={offlineCount}, "
                    + $"Alarm={alarmCount}"
            );
        }
        catch (MachineUnavailableException exception)
        {
            _logger.LogError(
                exception,
                "Machine polling could not retrieve the machines. "
                    + "The next polling cycle will retry."
            );
        }
    }
}
