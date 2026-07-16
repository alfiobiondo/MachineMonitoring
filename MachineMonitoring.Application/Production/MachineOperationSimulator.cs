using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application.Production;

public sealed class MachineOperationSimulator
{
    private readonly IMachineOperationRepository _operationRepository;

    private readonly MachineOperationApplicationService _operationService;

    private readonly OperationSimulatorOptions _options;

    private readonly ILogger<MachineOperationSimulator> _logger;

    public MachineOperationSimulator(
        IMachineOperationRepository operationRepository,
        MachineOperationApplicationService operationService,
        IOptions<OperationSimulatorOptions> options,
        ILogger<MachineOperationSimulator> logger
    )
    {
        ArgumentNullException.ThrowIfNull(operationRepository);

        ArgumentNullException.ThrowIfNull(operationService);

        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _operationRepository = operationRepository;
        _operationService = operationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> TryProcessNextAsync(CancellationToken cancellationToken)
    {
        MachineOperation? operation = await _operationRepository.GetNextQueuedAsync(
            cancellationToken
        );

        if (operation is null)
        {
            return false;
        }

        _logger.LogInformation(
            "The simulator selected queued operation {OperationId}.",
            operation.Id
        );

        await _operationService.StartAsync(
            new StartMachineOperationCommand(
                OperationId: operation.Id,
                InitialPhase: _options.InitialPhase
            ),
            cancellationToken
        );

        await SimulateProgressAsync(operation.Id, cancellationToken);

        return true;
    }

    private async Task SimulateProgressAsync(Guid operationId, CancellationToken cancellationToken)
    {
        int progress = _options.ProgressIncrement;

        while (progress < 100)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(_options.ProgressIntervalSeconds),
                cancellationToken
            );

            await _operationService.UpdateProgressAsync(
                new UpdateMachineOperationProgressCommand(
                    OperationId: operationId,
                    ProgressPercentage: progress,
                    CurrentPhase: CreatePhaseDescription(progress)
                ),
                cancellationToken
            );

            progress += _options.ProgressIncrement;
        }

        await Task.Delay(TimeSpan.FromSeconds(_options.ProgressIntervalSeconds), cancellationToken);

        await _operationService.CompleteAsync(
            new CompleteMachineOperationCommand(OperationId: operationId),
            cancellationToken
        );

        _logger.LogInformation("The simulator completed operation {OperationId}.", operationId);
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
