using System.Text.Json;
using MachineMonitoring.Api.Hubs;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence.Outbox;
using Microsoft.AspNetCore.SignalR;

namespace MachineMonitoring.Api.Realtime;

public sealed class SignalROutboxMessageDispatcher : IOutboxMessageDispatcher
{
    private const string MachineAlarmRaisedType = "machine-alarm-raised.v1";

    private const string MachineAlarmAcknowledgedType = "machine-alarm-acknowledged.v1";

    private const string MachineAlarmResolvedType = "machine-alarm-resolved.v1";

    private const string MachineRuntimeStatusChangedType = "machine-runtime-status-changed.v1";

    private const string MachineAlarmChangedClientMethod = "alarmChanged";

    private const string MachineRuntimeChangedClientMethod = "machineRuntimeChanged";

    private readonly IMachineRuntimeStateRepository _machineRuntimeStateRepository;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IMachineAlarmRepository _machineAlarmRepository;
    private readonly IHubContext<MachineMonitoringHub> _hubContext;

    public SignalROutboxMessageDispatcher(
        IMachineAlarmRepository machineAlarmRepository,
        IMachineRuntimeStateRepository machineRuntimeStateRepository,
        IHubContext<MachineMonitoringHub> hubContext
    )
    {
        ArgumentNullException.ThrowIfNull(machineAlarmRepository);
        ArgumentNullException.ThrowIfNull(machineRuntimeStateRepository);
        ArgumentNullException.ThrowIfNull(hubContext);

        _machineAlarmRepository = machineAlarmRepository;
        _machineRuntimeStateRepository = machineRuntimeStateRepository;
        _hubContext = hubContext;
    }

    public async Task DispatchAsync(
        OutboxDispatchMessage message,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(message);

        switch (message.Type)
        {
            case MachineAlarmRaisedType:
                await DispatchMachineAlarmRaisedAsync(message, cancellationToken);
                return;

            case MachineAlarmAcknowledgedType:
                await DispatchMachineAlarmChangedAsync(
                    message,
                    changeKind: "acknowledged",
                    cancellationToken
                );
                return;

            case MachineAlarmResolvedType:
                await DispatchMachineAlarmChangedAsync(
                    message,
                    changeKind: "resolved",
                    cancellationToken
                );
                return;

            case MachineRuntimeStatusChangedType:
                await DispatchMachineRuntimeChangedAsync(message, cancellationToken);
                return;

            default:
                throw new InvalidOperationException(
                    $"Outbox message type '{message.Type}' is not supported "
                        + "by the SignalR dispatcher."
                );
        }
    }

    private async Task DispatchMachineAlarmRaisedAsync(
        OutboxDispatchMessage message,
        CancellationToken cancellationToken
    )
    {
        MachineAlarmRaisedOutboxPayload payload =
            JsonSerializer.Deserialize<MachineAlarmRaisedOutboxPayload>(
                message.Payload,
                SerializerOptions
            )
            ?? throw new InvalidOperationException(
                $"Outbox message {message.Id} contains an invalid alarm payload."
            );

        MachineAlarm alarm =
            await _machineAlarmRepository.GetByIdAsync(payload.AlarmId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Machine alarm {payload.AlarmId} referenced by outbox message "
                    + $"{message.Id} was not found."
            );

        MachineAlarmChangedEvent realtimeEvent = new(
            EventId: message.Id,
            ChangeKind: "raised",
            AlarmId: alarm.Id,
            MachineId: alarm.MachineId,
            MachineOperationId: alarm.MachineOperationId,
            Code: alarm.Code,
            Severity: alarm.Severity.ToString(),
            Status: alarm.Status.ToString(),
            Message: alarm.Message,
            IsBlocking: MachineAlarmBlockingPolicy.IsBlocking(alarm),
            RaisedAt: alarm.RaisedAt,
            AcknowledgedAt: alarm.AcknowledgedAt,
            ResolvedAt: alarm.ResolvedAt,
            ResolutionNotes: alarm.ResolutionNotes,
            OccurredAt: message.OccurredAt
        );

        string groupName = MachineMonitoringHub.CreateMachineGroupName(alarm.MachineId);

        await _hubContext
            .Clients.Group(groupName)
            .SendAsync(MachineAlarmChangedClientMethod, realtimeEvent, cancellationToken);
    }

    private async Task DispatchMachineAlarmChangedAsync(
        OutboxDispatchMessage message,
        string changeKind,
        CancellationToken cancellationToken
    )
    {
        MachineAlarmChangedOutboxPayload payload =
            JsonSerializer.Deserialize<MachineAlarmChangedOutboxPayload>(
                message.Payload,
                SerializerOptions
            )
            ?? throw new InvalidOperationException(
                $"Outbox message {message.Id} contains an invalid " + "alarm-changed payload."
            );

        MachineAlarm alarm =
            await _machineAlarmRepository.GetByIdAsync(payload.AlarmId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Machine alarm {payload.AlarmId} referenced by "
                    + $"outbox message {message.Id} was not found."
            );

        MachineAlarmChangedEvent realtimeEvent = new(
            EventId: message.Id,
            ChangeKind: changeKind,
            AlarmId: alarm.Id,
            MachineId: alarm.MachineId,
            MachineOperationId: alarm.MachineOperationId,
            Code: alarm.Code,
            Severity: alarm.Severity.ToString(),
            Status: alarm.Status.ToString(),
            Message: alarm.Message,
            IsBlocking: MachineAlarmBlockingPolicy.IsBlocking(alarm),
            RaisedAt: alarm.RaisedAt,
            AcknowledgedAt: alarm.AcknowledgedAt,
            ResolvedAt: alarm.ResolvedAt,
            ResolutionNotes: alarm.ResolutionNotes,
            OccurredAt: message.OccurredAt
        );

        string groupName = MachineMonitoringHub.CreateMachineGroupName(alarm.MachineId);

        await _hubContext
            .Clients.Group(groupName)
            .SendAsync(MachineAlarmChangedClientMethod, realtimeEvent, cancellationToken);
    }

    private async Task DispatchMachineRuntimeChangedAsync(
        OutboxDispatchMessage message,
        CancellationToken cancellationToken
    )
    {
        MachineRuntimeChangedOutboxPayload payload =
            JsonSerializer.Deserialize<MachineRuntimeChangedOutboxPayload>(
                message.Payload,
                SerializerOptions
            )
            ?? throw new InvalidOperationException(
                $"Outbox message {message.Id} contains an invalid " + "runtime payload."
            );

        MachineRuntimeState runtime =
            await _machineRuntimeStateRepository.GetByMachineIdAsync(
                payload.MachineId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Machine runtime state {payload.MachineId} referenced "
                    + $"by outbox message {message.Id} was not found."
            );

        MachineRuntimeChangedEvent realtimeEvent = new(
            EventId: message.Id,
            MachineId: runtime.MachineId,
            Status: runtime.Status.ToString(),
            CurrentOperationId: runtime.CurrentOperationId,
            LastChangedAt: runtime.LastChangedAt,
            FailureReason: runtime.FailureReason,
            ActiveAlarmId: runtime.ActiveAlarmId,
            Version: runtime.Version,
            OccurredAt: message.OccurredAt
        );

        string groupName = MachineMonitoringHub.CreateMachineGroupName(runtime.MachineId);

        await _hubContext
            .Clients.Group(groupName)
            .SendAsync(MachineRuntimeChangedClientMethod, realtimeEvent, cancellationToken);
    }

    private sealed record MachineAlarmRaisedOutboxPayload(
        Guid AlarmId,
        string MachineId,
        Guid? OperationId,
        DateTimeOffset OccurredAt
    );

    private sealed record MachineAlarmChangedOutboxPayload(
        Guid AlarmId,
        string MachineId,
        DateTimeOffset OccurredAt
    );

    private sealed record MachineRuntimeChangedOutboxPayload(
        string MachineId,
        string Status,
        Guid? CurrentOperationId,
        DateTimeOffset OccurredAt
    );
}
