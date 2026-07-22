using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application.Production;

public sealed class MachineOperationSimulator
{
    private readonly MachineOperationApplicationService _operationService;

    private readonly IOperationProgressStrategy _progressStrategy;

    private readonly ILogger<MachineOperationSimulator> _logger;

    public MachineOperationSimulator(
        MachineOperationApplicationService operationService,
        IOperationProgressStrategy progressStrategy,
        ILogger<MachineOperationSimulator> logger
    )
    {
        ArgumentNullException.ThrowIfNull(operationService);
        ArgumentNullException.ThrowIfNull(progressStrategy);
        ArgumentNullException.ThrowIfNull(logger);

        _operationService = operationService;
        _progressStrategy = progressStrategy;
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

        int updatedProgress = Math.Min(
            100,
            operation.ProgressPercentage + _progressStrategy.GetNextIncrement()
        );

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
