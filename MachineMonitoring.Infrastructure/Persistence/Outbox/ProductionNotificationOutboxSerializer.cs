using System.Text.Json;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Infrastructure.Persistence.Models;

namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public sealed class ProductionNotificationOutboxSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public IReadOnlyCollection<OutboxMessageRecord> Serialize(
        IReadOnlyCollection<ProductionNotification> notifications,
        DateTimeOffset createdAt
    )
    {
        ArgumentNullException.ThrowIfNull(notifications);

        return notifications.Select(notification => SerializeSingle(notification, createdAt)).ToArray();
    }

    private static OutboxMessageRecord SerializeSingle(
        ProductionNotification notification,
        DateTimeOffset createdAt
    )
    {
        ArgumentNullException.ThrowIfNull(notification);

        return notification switch
        {
            OperationStatusChangedNotification statusChanged => CreateRecord(
                type: "operation-status-changed.v1",
                payload: new OperationStatusChangedPayload(
                    statusChanged.OperationId,
                    statusChanged.Status.ToString(),
                    statusChanged.OccurredAt
                ),
                occurredAt: statusChanged.OccurredAt,
                createdAt: createdAt
            ),
            OperationProgressChangedNotification progressChanged => CreateRecord(
                type: "operation-progress-changed.v1",
                payload: new OperationProgressChangedPayload(
                    progressChanged.OperationId,
                    progressChanged.ProgressPercentage,
                    progressChanged.CurrentPhase,
                    progressChanged.OccurredAt
                ),
                occurredAt: progressChanged.OccurredAt,
                createdAt: createdAt
            ),
            OperationEventAppendedNotification eventAppended => CreateRecord(
                type: "operation-event-appended.v1",
                payload: new OperationEventAppendedPayload(
                    eventAppended.EventId,
                    eventAppended.OperationId,
                    eventAppended.EventType.ToString(),
                    eventAppended.OccurredAt
                ),
                occurredAt: eventAppended.OccurredAt,
                createdAt: createdAt
            ),
            MachineAlarmRaisedNotification alarmRaised => CreateRecord(
                type: "machine-alarm-raised.v1",
                payload: new MachineAlarmRaisedPayload(
                    alarmRaised.AlarmId,
                    alarmRaised.MachineId,
                    alarmRaised.OperationId,
                    alarmRaised.OccurredAt
                ),
                occurredAt: alarmRaised.OccurredAt,
                createdAt: createdAt
            ),
            MachineAlarmAcknowledgedNotification alarmAcknowledged => CreateRecord(
                type: "machine-alarm-acknowledged.v1",
                payload: new MachineAlarmAcknowledgedPayload(
                    alarmAcknowledged.AlarmId,
                    alarmAcknowledged.MachineId,
                    alarmAcknowledged.OccurredAt
                ),
                occurredAt: alarmAcknowledged.OccurredAt,
                createdAt: createdAt
            ),
            MachineAlarmResolvedNotification alarmResolved => CreateRecord(
                type: "machine-alarm-resolved.v1",
                payload: new MachineAlarmResolvedPayload(
                    alarmResolved.AlarmId,
                    alarmResolved.MachineId,
                    alarmResolved.OccurredAt
                ),
                occurredAt: alarmResolved.OccurredAt,
                createdAt: createdAt
            ),
            MachineRuntimeStatusChangedNotification runtimeStatusChanged => CreateRecord(
                type: "machine-runtime-status-changed.v1",
                payload: new MachineRuntimeStatusChangedPayload(
                    runtimeStatusChanged.MachineId,
                    runtimeStatusChanged.Status.ToString(),
                    runtimeStatusChanged.CurrentOperationId,
                    runtimeStatusChanged.OccurredAt
                ),
                occurredAt: runtimeStatusChanged.OccurredAt,
                createdAt: createdAt
            ),
            _ => throw new InvalidOperationException(
                $"Production notification type {notification.GetType().Name} is not supported by the outbox serializer."
            ),
        };
    }

    private static OutboxMessageRecord CreateRecord(
        string type,
        object payload,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt
    )
    {
        return new OutboxMessageRecord
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = JsonSerializer.Serialize(payload, SerializerOptions),
            OccurredAt = occurredAt,
            CreatedAt = createdAt,
        };
    }

    private sealed record OperationStatusChangedPayload(
        Guid OperationId,
        string Status,
        DateTimeOffset OccurredAt
    );

    private sealed record OperationProgressChangedPayload(
        Guid OperationId,
        int ProgressPercentage,
        string? CurrentPhase,
        DateTimeOffset OccurredAt
    );

    private sealed record OperationEventAppendedPayload(
        Guid EventId,
        Guid OperationId,
        string EventType,
        DateTimeOffset OccurredAt
    );

    private sealed record MachineAlarmRaisedPayload(
        Guid AlarmId,
        string MachineId,
        Guid? OperationId,
        DateTimeOffset OccurredAt
    );

    private sealed record MachineAlarmAcknowledgedPayload(
        Guid AlarmId,
        string MachineId,
        DateTimeOffset OccurredAt
    );

    private sealed record MachineAlarmResolvedPayload(
        Guid AlarmId,
        string MachineId,
        DateTimeOffset OccurredAt
    );

    private sealed record MachineRuntimeStatusChangedPayload(
        string MachineId,
        string Status,
        Guid? CurrentOperationId,
        DateTimeOffset OccurredAt
    );
}
