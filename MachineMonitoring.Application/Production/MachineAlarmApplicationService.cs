using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed class MachineAlarmApplicationService
{
    private readonly IMachineAlarmRepository _machineAlarmRepository;
    private readonly IMachineOperationRepository _machineOperationRepository;
    private readonly IMachineOperationEventRepository _machineOperationEventRepository;
    private readonly IProductionTransactionManager _transactionManager;

    public MachineAlarmApplicationService(
        IMachineAlarmRepository machineAlarmRepository,
        IMachineOperationRepository machineOperationRepository,
        IMachineOperationEventRepository machineOperationEventRepository,
        IProductionTransactionManager transactionManager
    )
    {
        _machineAlarmRepository = machineAlarmRepository;
        _machineOperationRepository = machineOperationRepository;
        _machineOperationEventRepository = machineOperationEventRepository;
        _transactionManager = transactionManager;
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
        MachineAlarm alarm = await GetRequiredAlarmAsync(command.AlarmId, cancellationToken);
        alarm.Acknowledge(DateTimeOffset.UtcNow);
        await _machineAlarmRepository.UpdateAsync(alarm, cancellationToken);
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

                if (alarm.MachineOperationId is not Guid operationId)
                {
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
                    return;
                }

                operation.RecoverFromFault();
                await _machineOperationRepository.UpdateAsync(operation, ct);
                await _machineOperationEventRepository.AddAsync(
                    new MachineOperationEvent(
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
