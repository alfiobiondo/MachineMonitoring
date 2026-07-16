using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Console.Production;

public sealed class MachineOperationSimulatorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly OperationSimulatorOptions _options;

    private readonly ILogger<MachineOperationSimulatorWorker> _logger;

    public MachineOperationSimulatorWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OperationSimulatorOptions> options,
        ILogger<MachineOperationSimulatorWorker> logger
    )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Machine-operation simulator started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

                MachineOperationSimulator simulator =
                    scope.ServiceProvider.GetRequiredService<MachineOperationSimulator>();

                bool operationProcessed = await simulator.TryProcessNextAsync(stoppingToken);

                if (operationProcessed)
                {
                    continue;
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                    stoppingToken
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "An error occurred in the " + "machine-operation simulator."
                );

                await Task.Delay(
                    TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                    stoppingToken
                );
            }
        }

        _logger.LogInformation("Machine-operation simulator stopped.");
    }
}
