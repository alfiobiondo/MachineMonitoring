using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Reports;
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
            MachineReport report = await _machineManager.CreateReportAsync(cancellationToken);

            PrintReport(report);
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

    private static void PrintReport(MachineReport report)
    {
        Console.WriteLine(
            $"[{report.GeneratedAt:HH:mm:ss}] " + $"{report.Items.Count} machines retrieved."
        );

        foreach (MachineReportItem item in report.Items)
        {
            Console.WriteLine($"- {item.Description}");

            if (item.Diagnostic is not null)
            {
                Console.WriteLine($"  Diagnostic: {item.Diagnostic.Message}");
            }
            else
            {
                Console.WriteLine($"  Diagnostic error: {item.DiagnosticError}");
            }
        }

        MachineStatusSummary summary = report.StatusSummary;

        string statusText = string.Join(
            ", ",
            Enum.GetValues<MachineStatus>().Select(status => $"{status}={summary.GetCount(status)}")
        );

        Console.WriteLine($"Status summary ({summary.TotalCount} total): " + statusText);

        Console.WriteLine(
            $"Diagnostics: "
                + $"Successful={report.SuccessfulDiagnosticCount}, "
                + $"Failed={report.FailedDiagnosticCount}"
        );
    }
}
