using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Console.Production;

public sealed class MachineIncidentSimulatorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MachineIncidentSimulatorOptions _options;
    private readonly ILogger<MachineIncidentSimulatorWorker> _logger;

    public MachineIncidentSimulatorWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MachineIncidentSimulatorOptions> options,
        ILogger<MachineIncidentSimulatorWorker> logger
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
        _logger.LogInformation("Machine-incident simulator started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "An error occurred in the machine-incident simulator.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                    stoppingToken
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Machine-incident simulator stopped.");
    }

    private async Task ProcessOnceAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        MachineRuntimeAssignedOperationQuery assignedOperationQuery =
            scope.ServiceProvider.GetRequiredService<MachineRuntimeAssignedOperationQuery>();
        MachineIncidentSimulator incidentSimulator =
            scope.ServiceProvider.GetRequiredService<MachineIncidentSimulator>();

        IReadOnlyCollection<MachineMonitoring.Domain.Production.MachineOperation> runningOperations =
            await assignedOperationQuery.GetAssignedRunningOperationsAsync(stoppingToken);

        foreach (MachineMonitoring.Domain.Production.MachineOperation operation in runningOperations)
        {
            try
            {
                await incidentSimulator.ProcessCandidateAsync(operation, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Skipping incident simulation for operation {OperationId} because processing failed.",
                    operation.Id
                );
            }
        }
    }
}
