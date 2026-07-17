using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain;
using MachineMonitoring.Domain.Exceptions;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed class MachineRuntimeApplicationService
{
    private readonly IMachineProvider _machineProvider;
    private readonly IMachineRuntimeStateRepository _runtimeStateRepository;
    private readonly IMachineOperationRepository _operationRepository;
    private readonly IMachineAlarmRepository _alarmRepository;
    private readonly IProductionTransactionManager _transactionManager;
    private readonly IProductionNotificationPublisher _notificationPublisher;

    public MachineRuntimeApplicationService(
        IMachineProvider machineProvider,
        IMachineRuntimeStateRepository runtimeStateRepository,
        IMachineOperationRepository operationRepository,
        IMachineAlarmRepository alarmRepository,
        IProductionTransactionManager transactionManager,
        IProductionNotificationPublisher notificationPublisher
    )
    {
        _machineProvider = machineProvider;
        _runtimeStateRepository = runtimeStateRepository;
        _operationRepository = operationRepository;
        _alarmRepository = alarmRepository;
        _transactionManager = transactionManager;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<IReadOnlyCollection<MachineDetailsResult>> GetAllAsync(
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<Machine> machines = await _machineProvider.GetMachinesAsync(cancellationToken);
        List<MachineDetailsResult> results = [];

        foreach (Machine machine in machines.OrderBy(item => item.Id))
        {
            results.Add(await GetDetailsInternalAsync(machine, cancellationToken));
        }

        return results;
    }

    public async Task<MachineDetailsResult> GetByIdAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        Machine machine = await GetRequiredMachineAsync(machineId, cancellationToken);
        return await GetDetailsInternalAsync(machine, cancellationToken);
    }

    public async Task<MachineRuntimeStateResult> GetStateAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        MachineRuntimeState state = await GetOrCreateRuntimeStateAsync(machineId, cancellationToken);
        IReadOnlyCollection<MachineAlarm> alarms = await _alarmRepository.GetByMachineIdAsync(
            machineId,
            activeOnly: true,
            cancellationToken
        );

        return ToStateResult(state, alarms.Count);
    }

    public Task FaultAsync(FaultMachineCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _transactionManager.ExecuteAsync(
            async ct =>
            {
                await GetRequiredMachineAsync(command.MachineId, ct);

                MachineRuntimeState state = await GetOrCreateRuntimeStateAsync(command.MachineId, ct);
                MachineOperation? operation = null;

                if (command.OperationId is Guid operationId)
                {
                    operation = await _operationRepository.GetByIdAsync(operationId, ct)
                        ?? throw new ResourceNotFoundException(
                            "Machine operation",
                            operationId.ToString()
                        );

                    if (!string.Equals(operation.MachineId, command.MachineId, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new BusinessRuleViolationException(
                            $"Operation {operation.Id} does not belong to machine {command.MachineId}."
                        );
                    }
                }

                DateTimeOffset raisedAt = DateTimeOffset.UtcNow;
                MachineAlarm alarm = new(
                    id: Guid.NewGuid(),
                    machineId: command.MachineId,
                    machineOperationId: command.OperationId,
                    code: command.Code,
                    severity: command.Severity,
                    message: command.Message,
                    raisedAt: raisedAt
                );

                if (operation is not null && operation.Status == MachineOperationStatus.Running)
                {
                    operation.Fault(command.Message);
                    await _operationRepository.UpdateAsync(operation, ct);
                }

                state.Fault(command.OperationId, alarm.Id, command.Message, raisedAt);

                await _alarmRepository.AddAsync(alarm, ct);
                await SaveRuntimeStateAsync(state, ct);

                if (operation is not null)
                {
                    await _notificationPublisher.PublishAsync(
                        new OperationStatusChangedNotification(
                            OperationId: operation.Id,
                            Status: operation.Status,
                            OccurredAt: raisedAt
                        ),
                        ct
                    );
                }

                await _notificationPublisher.PublishAsync(
                    new MachineAlarmRaisedNotification(
                        AlarmId: alarm.Id,
                        MachineId: alarm.MachineId,
                        OperationId: alarm.MachineOperationId,
                        OccurredAt: raisedAt
                    ),
                    ct
                );
                await _notificationPublisher.PublishAsync(
                    new MachineRuntimeStatusChangedNotification(
                        MachineId: state.MachineId,
                        Status: state.Status,
                        CurrentOperationId: state.CurrentOperationId,
                        OccurredAt: state.LastChangedAt
                    ),
                    ct
                );
            },
            cancellationToken
        );
    }

    public Task StartMaintenanceAsync(
        StartMachineMaintenanceCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        return ChangeAvailabilityAsync(
            command.MachineId,
            state => state.SetMaintenance(DateTimeOffset.UtcNow, command.Reason),
            cancellationToken
        );
    }

    public Task CompleteMaintenanceAsync(
        CompleteMachineMaintenanceCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        return ChangeAvailabilityAsync(
            command.MachineId,
            state => state.SetAvailable(DateTimeOffset.UtcNow),
            cancellationToken
        );
    }

    public Task SetOfflineAsync(
        SetMachineOfflineCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        return ChangeAvailabilityAsync(
            command.MachineId,
            state => state.SetOffline(DateTimeOffset.UtcNow, command.Reason),
            cancellationToken
        );
    }

    public Task SetOnlineAsync(SetMachineOnlineCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ChangeAvailabilityAsync(
            command.MachineId,
            state => state.SetAvailable(DateTimeOffset.UtcNow),
            cancellationToken
        );
    }

    private Task ChangeAvailabilityAsync(
        string machineId,
        Action<MachineRuntimeState> change,
        CancellationToken cancellationToken
    )
    {
        return _transactionManager.ExecuteAsync(
            async ct =>
            {
                await GetRequiredMachineAsync(machineId, ct);
                MachineRuntimeState state = await GetOrCreateRuntimeStateAsync(machineId, ct);
                change(state);
                await SaveRuntimeStateAsync(state, ct);
                await _notificationPublisher.PublishAsync(
                    new MachineRuntimeStatusChangedNotification(
                        MachineId: state.MachineId,
                        Status: state.Status,
                        CurrentOperationId: state.CurrentOperationId,
                        OccurredAt: state.LastChangedAt
                    ),
                    ct
                );
            },
            cancellationToken
        );
    }

    private async Task<MachineDetailsResult> GetDetailsInternalAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        MachineRuntimeState state = await GetOrCreateRuntimeStateAsync(machine.Id, cancellationToken);
        IReadOnlyCollection<MachineAlarm> alarms = await _alarmRepository.GetByMachineIdAsync(
            machine.Id,
            activeOnly: true,
            cancellationToken
        );

        return new MachineDetailsResult(
            Id: machine.Id,
            Name: machine.Name,
            Location: machine.Location,
            SerialNumber: machine.SerialNumber,
            CatalogStatus: machine.Status,
            Runtime: ToStateResult(state, alarms.Count)
        );
    }

    private async Task<Machine> GetRequiredMachineAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        return (await _machineProvider.GetMachinesAsync(cancellationToken)).SingleOrDefault(item =>
            string.Equals(item.Id, machineId, StringComparison.OrdinalIgnoreCase)
        ) ?? throw new ResourceNotFoundException("Machine", machineId);
    }

    private async Task<MachineRuntimeState> GetOrCreateRuntimeStateAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        MachineRuntimeState? state = await _runtimeStateRepository.GetByMachineIdAsync(
            machineId,
            cancellationToken
        );

        if (state is not null)
        {
            return state;
        }

        MachineRuntimeState created = MachineRuntimeState.CreateAvailable(
            machineId,
            DateTimeOffset.UtcNow
        );

        await _runtimeStateRepository.AddAsync(created, cancellationToken);
        return created;
    }

    private async Task SaveRuntimeStateAsync(
        MachineRuntimeState state,
        CancellationToken cancellationToken
    )
    {
        await _runtimeStateRepository.UpdateAsync(state, cancellationToken);
    }

    private static MachineRuntimeStateResult ToStateResult(
        MachineRuntimeState state,
        int activeAlarmsCount
    )
    {
        return new MachineRuntimeStateResult(
            MachineId: state.MachineId,
            Status: state.Status,
            CurrentOperationId: state.CurrentOperationId,
            LastChangedAt: state.LastChangedAt,
            FailureReason: state.FailureReason,
            ActiveAlarmId: state.ActiveAlarmId,
            ActiveAlarmsCount: activeAlarmsCount
        );
    }
}
