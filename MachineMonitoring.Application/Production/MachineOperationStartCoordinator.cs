using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Exceptions;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed class MachineOperationStartCoordinator
{
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly IMachineOperationEventRepository _machineOperationEventRepository;
    private readonly IMachineAlarmRepository _machineAlarmRepository;
    private readonly IMachineRuntimeStateRepository _machineRuntimeStateRepository;
    private readonly IProductionNotificationPublisher _notificationPublisher;

    public MachineOperationStartCoordinator(
        IMachineOperationRepository machineOperationRepository,
        IMachineOperationEventRepository machineOperationEventRepository,
        IMachineAlarmRepository machineAlarmRepository,
        IMachineRuntimeStateRepository machineRuntimeStateRepository,
        IProductionNotificationPublisher notificationPublisher
    )
    {
        ArgumentNullException.ThrowIfNull(machineOperationRepository);
        ArgumentNullException.ThrowIfNull(machineOperationEventRepository);
        ArgumentNullException.ThrowIfNull(machineAlarmRepository);
        ArgumentNullException.ThrowIfNull(machineRuntimeStateRepository);
        ArgumentNullException.ThrowIfNull(notificationPublisher);

        _machineOperationRepository = machineOperationRepository;
        _machineOperationEventRepository = machineOperationEventRepository;
        _machineAlarmRepository = machineAlarmRepository;
        _machineRuntimeStateRepository = machineRuntimeStateRepository;
        _notificationPublisher = notificationPublisher;
    }

    public async Task StartAsync(
        MachineOperation operation,
        string initialPhase,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialPhase);

        MachineRuntimeState runtimeState = await GetOrCreateRuntimeStateAsync(
            operation.MachineId,
            startedAt,
            cancellationToken,
            operation
        );

        await EnsureMachineCanAcceptOperationAsync(runtimeState, operation, cancellationToken);

        int expectedVersion = runtimeState.Version;
        operation.Start(startedAt: startedAt, initialPhase: initialPhase);
        runtimeState.StartOperation(operation.Id, startedAt);

        await _machineOperationRepository.UpdateAsync(operation, cancellationToken);
        await _machineRuntimeStateRepository.UpdateAsync(
            runtimeState,
            expectedVersion,
            cancellationToken
        );
        await AppendStartedEventAsync(operation, startedAt, cancellationToken);
        await PublishOperationStatusNotificationAsync(operation, startedAt, cancellationToken);
        await PublishMachineRuntimeNotificationAsync(runtimeState, startedAt, cancellationToken);
    }

    private async Task<MachineRuntimeState> GetOrCreateRuntimeStateAsync(
        string machineId,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken,
        MachineOperation? operation = null
    )
    {
        MachineRuntimeState? state = await _machineRuntimeStateRepository.GetByMachineIdAsync(
            machineId,
            cancellationToken
        );

        if (state is not null)
        {
            if (
                operation is not null
                && state.CurrentOperationId is null
                && operation.Status
                    is MachineOperationStatus.Running
                        or MachineOperationStatus.Paused
                        or MachineOperationStatus.Faulted
            )
            {
                int expectedVersion = state.Version;
                SynchronizeRuntimeStateWithOperation(state, operation, changedAt);
                await _machineRuntimeStateRepository.UpdateAsync(
                    state,
                    expectedVersion,
                    cancellationToken
                );
            }

            return state;
        }

        MachineRuntimeState created =
            operation is not null
            && operation.Status
                is MachineOperationStatus.Running
                    or MachineOperationStatus.Paused
                    or MachineOperationStatus.Faulted
                ? CreateRuntimeStateFromOperation(operation, changedAt)
                : MachineRuntimeState.CreateAvailable(machineId, changedAt);

        await _machineRuntimeStateRepository.AddAsync(created, cancellationToken);
        return created;
    }

    private async Task EnsureMachineCanAcceptOperationAsync(
        MachineRuntimeState runtimeState,
        MachineOperation operation,
        CancellationToken cancellationToken
    )
    {
        switch (runtimeState.Status)
        {
            case MachineRuntimeStatus.Available:
                if (runtimeState.CurrentOperationId is not null)
                {
                    throw new BusinessRuleViolationException(
                        $"Machine {runtimeState.MachineId} is Available but is still assigned to operation {runtimeState.CurrentOperationId}."
                    );
                }

                break;

            case MachineRuntimeStatus.Paused:
                if (runtimeState.CurrentOperationId is not Guid pausedOperationId)
                {
                    throw new BusinessRuleViolationException(
                        $"Machine {runtimeState.MachineId} is Paused without a current operation assignment and cannot start operation {operation.Id}."
                    );
                }

                if (pausedOperationId != operation.Id)
                {
                    throw new BusinessRuleViolationException(
                        $"Machine {runtimeState.MachineId} is paused on operation {pausedOperationId} and cannot start operation {operation.Id}."
                    );
                }

                break;

            case MachineRuntimeStatus.Running:
                if (runtimeState.CurrentOperationId is not Guid runningOperationId)
                {
                    throw new BusinessRuleViolationException(
                        $"Machine {runtimeState.MachineId} is Running without a current operation assignment and cannot start operation {operation.Id}."
                    );
                }

                if (runningOperationId == operation.Id)
                {
                    throw new BusinessRuleViolationException(
                        $"Machine {runtimeState.MachineId} is already running operation {operation.Id}."
                    );
                }

                throw new BusinessRuleViolationException(
                    $"Machine {runtimeState.MachineId} is already assigned to operation {runningOperationId}."
                );

            case MachineRuntimeStatus.Faulted:
            case MachineRuntimeStatus.Maintenance:
            case MachineRuntimeStatus.Offline:
                throw new BusinessRuleViolationException(
                    $"Machine {runtimeState.MachineId} is {runtimeState.Status} and cannot start operation {operation.Id}."
                );
        }

        IReadOnlyCollection<MachineAlarm> alarms =
            await _machineAlarmRepository.GetByMachineIdAsync(
                runtimeState.MachineId,
                activeOnly: true,
                cancellationToken
            );

        if (alarms.Any(MachineAlarmBlockingPolicy.IsBlocking))
        {
            throw new BusinessRuleViolationException(
                $"Machine {runtimeState.MachineId} has active blocking alarms and cannot start operation {operation.Id}."
            );
        }
    }

    private async Task AppendStartedEventAsync(
        MachineOperation operation,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken
    )
    {
        MachineOperationEvent machineOperationEvent = new(
            id: Guid.NewGuid(),
            machineOperationId: operation.Id,
            eventType: MachineOperationEventType.Started,
            occurredAt: occurredAt,
            previousStatus: MachineOperationStatus.Queued,
            newStatus: operation.Status,
            progressPercentage: operation.ProgressPercentage,
            phase: operation.CurrentPhase,
            reason: null,
            machineAlarmId: null,
            metadata: null
        );

        await _machineOperationEventRepository.AddAsync(machineOperationEvent, cancellationToken);
        await _notificationPublisher.PublishAsync(
            new OperationEventAppendedNotification(
                EventId: machineOperationEvent.Id,
                OperationId: machineOperationEvent.MachineOperationId,
                EventType: machineOperationEvent.EventType,
                OccurredAt: machineOperationEvent.OccurredAt
            ),
            cancellationToken
        );
    }

    private Task PublishOperationStatusNotificationAsync(
        MachineOperation operation,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken
    )
    {
        return _notificationPublisher.PublishAsync(
            new OperationStatusChangedNotification(
                OperationId: operation.Id,
                Status: operation.Status,
                OccurredAt: occurredAt
            ),
            cancellationToken
        );
    }

    private Task PublishMachineRuntimeNotificationAsync(
        MachineRuntimeState runtimeState,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken
    )
    {
        return _notificationPublisher.PublishAsync(
            new MachineRuntimeStatusChangedNotification(
                MachineId: runtimeState.MachineId,
                Status: runtimeState.Status,
                CurrentOperationId: runtimeState.CurrentOperationId,
                OccurredAt: occurredAt
            ),
            cancellationToken
        );
    }

    private static MachineRuntimeState CreateRuntimeStateFromOperation(
        MachineOperation operation,
        DateTimeOffset changedAt
    )
    {
        MachineRuntimeStatus status = operation.Status switch
        {
            MachineOperationStatus.Running => MachineRuntimeStatus.Running,
            MachineOperationStatus.Paused => MachineRuntimeStatus.Paused,
            MachineOperationStatus.Faulted => MachineRuntimeStatus.Faulted,
            _ => MachineRuntimeStatus.Available,
        };

        return MachineRuntimeState.Restore(
            machineId: operation.MachineId,
            status: status,
            currentOperationId: status == MachineRuntimeStatus.Available ? null : operation.Id,
            lastChangedAt: changedAt,
            failureReason: operation.FailureReason,
            activeAlarmId: null,
            version: 1
        );
    }

    private static void SynchronizeRuntimeStateWithOperation(
        MachineRuntimeState runtimeState,
        MachineOperation operation,
        DateTimeOffset changedAt
    )
    {
        if (operation.Status == MachineOperationStatus.Running)
        {
            runtimeState.StartOperation(operation.Id, changedAt);
            return;
        }

        if (operation.Status == MachineOperationStatus.Paused)
        {
            runtimeState.StartOperation(operation.Id, changedAt);
            runtimeState.PauseOperation(operation.Id, changedAt);
            return;
        }

        if (operation.Status == MachineOperationStatus.Faulted)
        {
            runtimeState.StartOperation(operation.Id, changedAt);
            runtimeState.Fault(
                operation.Id,
                Guid.NewGuid(),
                operation.FailureReason ?? "Faulted",
                changedAt
            );
        }
    }
}
