using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application.Production;

public sealed class MachineOperationSimulator
{
    private readonly MachineOperationApplicationService _operationService;

    private readonly OperationSimulatorOptions _options;

    private readonly ILogger<MachineOperationSimulator> _logger;

    public MachineOperationSimulator(
        MachineOperationApplicationService operationService,
        IOptions<OperationSimulatorOptions> options,
        ILogger<MachineOperationSimulator> logger
    )
    {
        ArgumentNullException.ThrowIfNull(operationService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _operationService = operationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessRunningOperationAsync(
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Status != MachineOperationStatus.Running)
        {
            return;
        }

        int updatedProgress = Math.Min(100, operation.ProgressPercentage + _options.ProgressIncrement);

        if (updatedProgress >= 100)
        {
            await _operationService.CompleteAsync(
                new CompleteMachineOperationCommand(OperationId: operation.Id),
                cancellationToken
            );

            _logger.LogInformation("The simulator completed operation {OperationId}.", operation.Id);

            return;
        }

        await _operationService.UpdateProgressAsync(
            new UpdateMachineOperationProgressCommand(
                OperationId: operation.Id,
                ProgressPercentage: updatedProgress,
                CurrentPhase: CreatePhaseDescription(updatedProgress)
            ),
            cancellationToken
        );
    }

    private static string CreatePhaseDescription(int progress)
    {
        return progress switch
        {
            < 25 => "Preparing machine",
            < 50 => "Starting laser cut",
            < 75 => "Laser cutting",
            _ => "Finishing cut",
        };
    }
}
