using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed class MachineOperationWarningApplicationService
{
    private readonly IMachineOperationRepository _operationRepository;
    private readonly IMachineRuntimeStateRepository _runtimeStateRepository;
    private readonly IMachineAlarmRepository _alarmRepository;
    private readonly IProductionTransactionManager _transactionManager;
    private readonly IProductionNotificationPublisher _notificationPublisher;
    private readonly TimeProvider _timeProvider;

    public MachineOperationWarningApplicationService(
        IMachineOperationRepository operationRepository,
        IMachineRuntimeStateRepository runtimeStateRepository,
        IMachineAlarmRepository alarmRepository,
        IProductionTransactionManager transactionManager,
        IProductionNotificationPublisher notificationPublisher,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(operationRepository);
        ArgumentNullException.ThrowIfNull(runtimeStateRepository);
        ArgumentNullException.ThrowIfNull(alarmRepository);
        ArgumentNullException.ThrowIfNull(transactionManager);
        ArgumentNullException.ThrowIfNull(notificationPublisher);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _operationRepository = operationRepository;
        _runtimeStateRepository = runtimeStateRepository;
        _alarmRepository = alarmRepository;
        _transactionManager = transactionManager;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
    }

    public Task<RaiseMachineOperationWarningResult> RaiseAsync(
        RaiseMachineOperationWarningCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        RaiseMachineOperationWarningResult result = new(
            RaiseMachineOperationWarningStatus.SkippedStateChanged,
            AlarmId: null
        );

        return ExecuteAndReturnAsync(command, result, cancellationToken);
    }

    private async Task<RaiseMachineOperationWarningResult> ExecuteAndReturnAsync(
        RaiseMachineOperationWarningCommand command,
        RaiseMachineOperationWarningResult result,
        CancellationToken cancellationToken
    )
    {
        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineOperation? operation = await _operationRepository.GetByIdAsync(
                    command.OperationId,
                    ct
                );

                if (
                    operation is null
                    || operation.Status != MachineOperationStatus.Running
                    || !string.Equals(operation.MachineId, command.MachineId, StringComparison.Ordinal)
                )
                {
                    result = new RaiseMachineOperationWarningResult(
                        RaiseMachineOperationWarningStatus.SkippedStateChanged,
                        AlarmId: null
                    );
                    return;
                }

                MachineRuntimeState? runtimeState =
                    await _runtimeStateRepository.GetByMachineIdAsync(command.MachineId, ct);

                if (
                    runtimeState is null
                    || runtimeState.Status != MachineRuntimeStatus.Running
                    || runtimeState.CurrentOperationId != operation.Id
                )
                {
                    result = new RaiseMachineOperationWarningResult(
                        RaiseMachineOperationWarningStatus.SkippedStateChanged,
                        AlarmId: null
                    );
                    return;
                }

                if (await HasActiveDuplicateAsync(command, ct))
                {
                    result = new RaiseMachineOperationWarningResult(
                        RaiseMachineOperationWarningStatus.SkippedDuplicate,
                        AlarmId: null
                    );
                    return;
                }

                DateTimeOffset raisedAt = _timeProvider.GetUtcNow();
                MachineAlarm alarm = new(
                    id: Guid.NewGuid(),
                    machineId: command.MachineId,
                    machineOperationId: operation.Id,
                    code: command.Code,
                    severity: MachineAlarmSeverity.Warning,
                    message: command.Message,
                    raisedAt: raisedAt
                );

                await _alarmRepository.AddAsync(alarm, ct);
                await _notificationPublisher.PublishAsync(
                    new MachineAlarmRaisedNotification(
                        AlarmId: alarm.Id,
                        MachineId: alarm.MachineId,
                        OperationId: alarm.MachineOperationId,
                        OccurredAt: alarm.RaisedAt
                    ),
                    ct
                );

                result = new RaiseMachineOperationWarningResult(
                    RaiseMachineOperationWarningStatus.Created,
                    AlarmId: alarm.Id
                );
            },
            cancellationToken
        );

        return result;
    }

    private async Task<bool> HasActiveDuplicateAsync(
        RaiseMachineOperationWarningCommand command,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineAlarm> operationAlarms =
            await _alarmRepository.GetByOperationIdAsync(command.OperationId, cancellationToken);

        return operationAlarms.Any(alarm =>
            string.Equals(alarm.MachineId, command.MachineId, StringComparison.Ordinal)
            && string.Equals(alarm.Code, command.Code, StringComparison.Ordinal)
            && alarm.Status is MachineAlarmStatus.Active or MachineAlarmStatus.Acknowledged
        );
    }
}
