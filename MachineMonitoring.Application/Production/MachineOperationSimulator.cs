using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application.Production;

public sealed class MachineOperationSimulator
{
    private readonly MachineOperationApplicationService _operationService;
    private readonly MachineRuntimeApplicationService _machineRuntimeService;

    private readonly IOperationProgressStrategy _progressStrategy;

    private readonly IOperationFaultStrategy _operationFaultStrategy;

    private readonly IMachineFaultStrategy _machineFaultStrategy;

    private readonly ILogger<MachineOperationSimulator> _logger;

    public MachineOperationSimulator(
        MachineOperationApplicationService operationService,
        MachineRuntimeApplicationService machineRuntimeService,
        IOperationProgressStrategy progressStrategy,
        IOperationFaultStrategy operationFaultStrategy,
        IMachineFaultStrategy machineFaultStrategy,
        ILogger<MachineOperationSimulator> logger
    )
    {
        ArgumentNullException.ThrowIfNull(operationService);
        ArgumentNullException.ThrowIfNull(machineRuntimeService);
        ArgumentNullException.ThrowIfNull(progressStrategy);
        ArgumentNullException.ThrowIfNull(operationFaultStrategy);
        ArgumentNullException.ThrowIfNull(machineFaultStrategy);
        ArgumentNullException.ThrowIfNull(logger);

        _operationService = operationService;
        _machineRuntimeService = machineRuntimeService;
        _progressStrategy = progressStrategy;
        _operationFaultStrategy = operationFaultStrategy;
        _machineFaultStrategy = machineFaultStrategy;
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

        MachineFaultDecision machineFault = _machineFaultStrategy.Evaluate(
            operation.MachineId,
            operation.Id
        );

        if (machineFault.ShouldFault)
        {
            await _operationService.FaultAsync(
                new FaultMachineOperationCommand(
                    OperationId: operation.Id,
                    AlarmCode: machineFault.AlarmCode ?? "MACHINE_FAULT",
                    FailureReason: machineFault.Reason ?? "Machine fault.",
                    AlarmMessage: machineFault.Message ?? "Machine fault.",
                    Severity: machineFault.Severity ?? MachineAlarmSeverity.Error
                ),
                cancellationToken
            );

            _logger.LogWarning(
                "The simulator faulted machine {MachineId} while processing operation {OperationId}.",
                operation.MachineId,
                operation.Id
            );

            return;
        }

        OperationFaultDecision operationFault = _operationFaultStrategy.Evaluate(operation);

        if (operationFault.ShouldFault)
        {
            await _operationService.FaultAsync(
                new FaultMachineOperationCommand(
                    OperationId: operation.Id,
                    AlarmCode: operationFault.AlarmCode ?? "OPERATION_FAULT",
                    FailureReason: operationFault.Reason ?? "Operation fault.",
                    AlarmMessage: operationFault.Message ?? "Operation fault.",
                    Severity: operationFault.Severity ?? MachineAlarmSeverity.Warning
                ),
                cancellationToken
            );

            _logger.LogWarning(
                "The simulator faulted operation {OperationId}.",
                operation.Id
            );

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

    public async Task ProcessMachineRuntimeAsync(
        MachineRuntimeState runtimeState,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(runtimeState);

        if (runtimeState.CurrentOperationId is not null)
        {
            return;
        }

        if (runtimeState.Status is not MachineRuntimeStatus.Available)
        {
            return;
        }

        MachineFaultDecision machineFault = _machineFaultStrategy.Evaluate(
            runtimeState.MachineId,
            currentOperationId: null
        );

        if (!machineFault.ShouldFault)
        {
            return;
        }

        await _machineRuntimeService.FaultAsync(
            new FaultMachineCommand(
                MachineId: runtimeState.MachineId,
                Code: machineFault.AlarmCode ?? "MACHINE_FAULT",
                Severity: machineFault.Severity ?? MachineAlarmSeverity.Error,
                Message: machineFault.Message ?? "Machine fault.",
                OperationId: null
            ),
            cancellationToken
        );

        _logger.LogWarning(
            "The simulator faulted idle machine {MachineId}.",
            runtimeState.MachineId
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
