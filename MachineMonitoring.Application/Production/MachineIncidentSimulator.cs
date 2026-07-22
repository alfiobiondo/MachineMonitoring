using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Exceptions;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application.Production;

public sealed class MachineIncidentSimulator
{
    public const string SimulatedWarningCode = "SIM-WARN-TEMP";
    public const string SimulatedBlockingAlarmCode = "SIM-ALARM-LASER";

    private const string SimulatedWarningMessage =
        "Simulated temperature warning during operation.";
    private const string SimulatedBlockingAlarmMessage =
        "Simulated laser blocking alarm during operation.";

    private readonly MachineIncidentSimulatorOptions _options;
    private readonly IIncidentRandomSource _randomSource;
    private readonly MachineIncidentCooldownTracker _cooldownTracker;
    private readonly MachineOperationWarningApplicationService _warningService;
    private readonly MachineOperationApplicationService _operationService;
    private readonly IMachineAlarmRepository _alarmRepository;
    private readonly ILogger<MachineIncidentSimulator> _logger;

    public MachineIncidentSimulator(
        IOptions<MachineIncidentSimulatorOptions> options,
        IIncidentRandomSource randomSource,
        MachineIncidentCooldownTracker cooldownTracker,
        MachineOperationWarningApplicationService warningService,
        MachineOperationApplicationService operationService,
        IMachineAlarmRepository alarmRepository,
        ILogger<MachineIncidentSimulator> logger
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(randomSource);
        ArgumentNullException.ThrowIfNull(cooldownTracker);
        ArgumentNullException.ThrowIfNull(warningService);
        ArgumentNullException.ThrowIfNull(operationService);
        ArgumentNullException.ThrowIfNull(alarmRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _randomSource = randomSource;
        _cooldownTracker = cooldownTracker;
        _warningService = warningService;
        _operationService = operationService;
        _alarmRepository = alarmRepository;
        _logger = logger;
    }

    public async Task<MachineIncidentSimulationResult> ProcessCandidateAsync(
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_options.Enabled)
        {
            return NoIncident(operation);
        }

        if (operation.Status != MachineOperationStatus.Running)
        {
            return SkippedStateChanged(operation);
        }

        if (_cooldownTracker.IsInCooldown(operation.MachineId))
        {
            _logger.LogDebug(
                "Skipping incident simulation for machine {MachineId} because it is in cooldown.",
                operation.MachineId
            );
            return new MachineIncidentSimulationResult(
                MachineIncidentSimulationStatus.SkippedCooldown,
                operation.MachineId,
                operation.Id,
                AlarmId: null
            );
        }

        double randomValue = _randomSource.NextPercentage();

        if (randomValue < _options.BlockingAlarmProbabilityPercentage)
        {
            return await RaiseBlockingAlarmAsync(operation, cancellationToken);
        }

        double warningThreshold =
            _options.BlockingAlarmProbabilityPercentage + _options.WarningProbabilityPercentage;

        if (randomValue < warningThreshold)
        {
            return await RaiseWarningAsync(operation, cancellationToken);
        }

        return NoIncident(operation);
    }

    private async Task<MachineIncidentSimulationResult> RaiseWarningAsync(
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        RaiseMachineOperationWarningResult result = await _warningService.RaiseAsync(
            new RaiseMachineOperationWarningCommand(
                MachineId: operation.MachineId,
                OperationId: operation.Id,
                Code: SimulatedWarningCode,
                Message: SimulatedWarningMessage
            ),
            cancellationToken
        );

        if (result.Status == RaiseMachineOperationWarningStatus.Created)
        {
            _cooldownTracker.RecordIncident(operation.MachineId);
            _logger.LogWarning(
                "Created simulated warning {AlarmCode} for operation {OperationId} on machine {MachineId}.",
                SimulatedWarningCode,
                operation.Id,
                operation.MachineId
            );
            return new MachineIncidentSimulationResult(
                MachineIncidentSimulationStatus.WarningCreated,
                operation.MachineId,
                operation.Id,
                result.AlarmId
            );
        }

        MachineIncidentSimulationStatus status =
            result.Status == RaiseMachineOperationWarningStatus.SkippedDuplicate
                ? MachineIncidentSimulationStatus.SkippedDuplicate
                : MachineIncidentSimulationStatus.SkippedStateChanged;

        _logger.LogDebug(
            "Skipped simulated warning for operation {OperationId} on machine {MachineId}. Reason: {Reason}.",
            operation.Id,
            operation.MachineId,
            status
        );

        return new MachineIncidentSimulationResult(
            status,
            operation.MachineId,
            operation.Id,
            AlarmId: null
        );
    }

    private async Task<MachineIncidentSimulationResult> RaiseBlockingAlarmAsync(
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        if (
            await HasActiveDuplicateAsync(
                operation,
                SimulatedBlockingAlarmCode,
                cancellationToken
            )
        )
        {
            _logger.LogDebug(
                "Skipped simulated blocking alarm for operation {OperationId} on machine {MachineId} because a duplicate is already active.",
                operation.Id,
                operation.MachineId
            );
            return new MachineIncidentSimulationResult(
                MachineIncidentSimulationStatus.SkippedDuplicate,
                operation.MachineId,
                operation.Id,
                AlarmId: null
            );
        }

        try
        {
            await _operationService.FaultAsync(
                new FaultMachineOperationCommand(
                    OperationId: operation.Id,
                    FailureReason: SimulatedBlockingAlarmMessage,
                    AlarmCode: SimulatedBlockingAlarmCode,
                    AlarmMessage: SimulatedBlockingAlarmMessage,
                    Severity: MachineAlarmSeverity.Error
                ),
                cancellationToken
            );
        }
        catch (BusinessRuleViolationException exception)
        {
            _logger.LogDebug(
                exception,
                "Skipped simulated blocking alarm for operation {OperationId} because state changed.",
                operation.Id
            );
            return SkippedStateChanged(operation);
        }

        _cooldownTracker.RecordIncident(operation.MachineId);
        _logger.LogWarning(
            "Created simulated blocking alarm {AlarmCode} for operation {OperationId} on machine {MachineId}.",
            SimulatedBlockingAlarmCode,
            operation.Id,
            operation.MachineId
        );

        return new MachineIncidentSimulationResult(
            MachineIncidentSimulationStatus.BlockingAlarmCreated,
            operation.MachineId,
            operation.Id,
            AlarmId: null
        );
    }

    private async Task<bool> HasActiveDuplicateAsync(
        MachineOperation operation,
        string code,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineAlarm> operationAlarms =
            await _alarmRepository.GetByOperationIdAsync(operation.Id, cancellationToken);

        return operationAlarms.Any(alarm =>
            string.Equals(alarm.MachineId, operation.MachineId, StringComparison.Ordinal)
            && string.Equals(alarm.Code, code, StringComparison.Ordinal)
            && alarm.Status is MachineAlarmStatus.Active or MachineAlarmStatus.Acknowledged
        );
    }

    private static MachineIncidentSimulationResult NoIncident(MachineOperation operation)
    {
        return new MachineIncidentSimulationResult(
            MachineIncidentSimulationStatus.None,
            operation.MachineId,
            operation.Id,
            AlarmId: null
        );
    }

    private static MachineIncidentSimulationResult SkippedStateChanged(MachineOperation operation)
    {
        return new MachineIncidentSimulationResult(
            MachineIncidentSimulationStatus.SkippedStateChanged,
            operation.MachineId,
            operation.Id,
            AlarmId: null
        );
    }
}
