using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed class MachineAlarmApplicationService
{
    private readonly IMachineAlarmRepository _machineAlarmRepository;
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly IMachineOperationEventRepository _machineOperationEventRepository;
    private readonly IMachineRuntimeStateRepository _machineRuntimeStateRepository;
    private readonly IProductionTransactionManager _transactionManager;
    private readonly IProductionNotificationPublisher _notificationPublisher;

    public MachineAlarmApplicationService(
        IMachineAlarmRepository machineAlarmRepository,
        IMachineOperationRepository machineOperationRepository,
        IMachineOperationEventRepository machineOperationEventRepository,
        IMachineRuntimeStateRepository machineRuntimeStateRepository,
        IProductionTransactionManager transactionManager,
        IProductionNotificationPublisher notificationPublisher
    )
    {
        _machineAlarmRepository = machineAlarmRepository;
        _machineOperationRepository = machineOperationRepository;
        _machineOperationEventRepository = machineOperationEventRepository;
        _machineRuntimeStateRepository = machineRuntimeStateRepository;
        _transactionManager = transactionManager;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<IReadOnlyCollection<MachineAlarmResult>> GetByMachineIdAsync(
        string machineId,
        bool activeOnly,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineAlarm> alarms = await _machineAlarmRepository.GetByMachineIdAsync(
            machineId,
            activeOnly,
            cancellationToken
        );

        return alarms.Select(ToResult).ToArray();
    }

    public async Task<IReadOnlyCollection<MachineAlarmResult>> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<MachineAlarm> alarms = await _machineAlarmRepository.GetByOperationIdAsync(
            operationId,
            cancellationToken
        );

        return alarms.Select(ToResult).ToArray();
    }

    public async Task AcknowledgeAsync(
        AcknowledgeMachineAlarmCommand command,
        CancellationToken cancellationToken
    )
    {
        await _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineAlarm alarm = await GetRequiredAlarmAsync(command.AlarmId, ct);
                DateTimeOffset acknowledgedAt = DateTimeOffset.UtcNow;
                alarm.Acknowledge(acknowledgedAt);
                await _machineAlarmRepository.UpdateAsync(alarm, ct);
                await _notificationPublisher.PublishAsync(
                    new MachineAlarmAcknowledgedNotification(
                        AlarmId: alarm.Id,
                        MachineId: alarm.MachineId,
                        OccurredAt: acknowledgedAt
                    ),
                    ct
                );
            },
            cancellationToken
        );
    }

    public Task ResolveAsync(
        ResolveMachineAlarmCommand command,
        CancellationToken cancellationToken
    )
    {
        return _transactionManager.ExecuteAsync(
            async ct =>
            {
                MachineAlarm alarm = await GetRequiredAlarmAsync(command.AlarmId, ct);
                alarm.Resolve(DateTimeOffset.UtcNow, command.ResolutionNotes);
                await _machineAlarmRepository.UpdateAsync(alarm, ct);

                MachineRuntimeState runtimeState = await GetRequiredRuntimeStateAsync(
                    alarm.MachineId,
                    ct
                );

                if (alarm.MachineOperationId is not Guid operationId)
                {
                    IReadOnlyCollection<MachineAlarm> activeMachineAlarms =
                        await _machineAlarmRepository.GetByMachineIdAsync(
                            alarm.MachineId,
                            activeOnly: true,
                            ct
                        );

                    if (!activeMachineAlarms.Any(MachineAlarmBlockingPolicy.IsBlocking))
                    {
                        int expectedVersion = runtimeState.Version;
                        runtimeState.ResolveFault(operationId: null, DateTimeOffset.UtcNow);
                        await _machineRuntimeStateRepository.UpdateAsync(
                            runtimeState,
                            expectedVersion,
                            ct
                        );
                    }

                    await _notificationPublisher.PublishAsync(
                        new MachineAlarmResolvedNotification(
                            AlarmId: alarm.Id,
                            MachineId: alarm.MachineId,
                            OccurredAt: DateTimeOffset.UtcNow
                        ),
                        ct
                    );
                    return;
                }

                MachineOperation operation = await _machineOperationRepository.GetByIdAsync(
                    operationId,
                    ct
                ) ?? throw new ResourceNotFoundException(
                    "Machine operation",
                    operationId.ToString()
                );

                if (operation.Status != MachineOperationStatus.Faulted)
                {
                    await _notificationPublisher.PublishAsync(
                        new MachineAlarmResolvedNotification(
                            AlarmId: alarm.Id,
                            MachineId: alarm.MachineId,
                            OccurredAt: DateTimeOffset.UtcNow
                        ),
                        ct
                    );
                    return;
                }

                operation.RecoverFromFault();
                int operationRuntimeExpectedVersion = runtimeState.Version;
                runtimeState.ResolveFault(operationId: operation.Id, DateTimeOffset.UtcNow);
                await _machineOperationRepository.UpdateAsync(operation, ct);
                await _machineRuntimeStateRepository.UpdateAsync(
                    runtimeState,
                    operationRuntimeExpectedVersion,
                    ct
                );
                MachineOperationEvent recoveredEvent = new(
                    id: Guid.NewGuid(),
                    machineOperationId: operation.Id,
                    eventType: MachineOperationEventType.Recovered,
                    occurredAt: DateTimeOffset.UtcNow,
                    previousStatus: MachineOperationStatus.Faulted,
                    newStatus: operation.Status,
                    progressPercentage: operation.ProgressPercentage,
                    phase: operation.CurrentPhase,
                    reason: command.ResolutionNotes,
                    machineAlarmId: alarm.Id,
                    metadata: null
                );
                await _machineOperationEventRepository.AddAsync(
                    recoveredEvent,
                    ct
                );
                await _notificationPublisher.PublishAsync(
                    new OperationEventAppendedNotification(
                        EventId: recoveredEvent.Id,
                        OperationId: recoveredEvent.MachineOperationId,
                        EventType: recoveredEvent.EventType,
                        OccurredAt: recoveredEvent.OccurredAt
                    ),
                    ct
                );
                await _notificationPublisher.PublishAsync(
                    new MachineAlarmResolvedNotification(
                        AlarmId: alarm.Id,
                        MachineId: alarm.MachineId,
                        OccurredAt: DateTimeOffset.UtcNow
                    ),
                    ct
                );
                await _notificationPublisher.PublishAsync(
                    new MachineRuntimeStatusChangedNotification(
                        MachineId: runtimeState.MachineId,
                        Status: runtimeState.Status,
                        CurrentOperationId: runtimeState.CurrentOperationId,
                        OccurredAt: DateTimeOffset.UtcNow
                    ),
                    ct
                );
            },
            cancellationToken
        );
    }

    private async Task<MachineAlarm> GetRequiredAlarmAsync(
        Guid alarmId,
        CancellationToken cancellationToken
    )
    {
        return await _machineAlarmRepository.GetByIdAsync(alarmId, cancellationToken)
            ?? throw new ResourceNotFoundException("Machine alarm", alarmId.ToString());
    }

    private async Task<MachineRuntimeState> GetRequiredRuntimeStateAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        return await _machineRuntimeStateRepository.GetByMachineIdAsync(machineId, cancellationToken)
            ?? throw new ResourceNotFoundException("Machine runtime state", machineId);
    }

    private static MachineAlarmResult ToResult(MachineAlarm alarm)
    {
        return new MachineAlarmResult(
            Id: alarm.Id,
            MachineId: alarm.MachineId,
            MachineOperationId: alarm.MachineOperationId,
            Code: alarm.Code,
            Severity: alarm.Severity,
            Status: alarm.Status,
            Message: alarm.Message,
            RaisedAt: alarm.RaisedAt,
            AcknowledgedAt: alarm.AcknowledgedAt,
            ResolvedAt: alarm.ResolvedAt,
            ResolutionNotes: alarm.ResolutionNotes
        );
    }
}
