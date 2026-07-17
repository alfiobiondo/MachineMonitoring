using System.Text.Json;
using MachineMonitoring.Application.Production.Notifications;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence.Outbox;

namespace MachineMonitoring.Tests.Production;

public sealed class ProductionNotificationOutboxSerializerTests
{
    private readonly ProductionNotificationOutboxSerializer _serializer = new();
    private readonly DateTimeOffset _createdAt = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _occurredAt = new(2026, 7, 17, 11, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Serialize_OperationStatusChanged_ProducesStableTypeAndPayload()
    {
        Guid operationId = Guid.NewGuid();
        OperationStatusChangedNotification notification = new(
            operationId,
            MachineOperationStatus.Running,
            _occurredAt
        );

        AssertSerialized(
            notification,
            "operation-status-changed.v1",
            payload =>
            {
                Assert.Equal(operationId, payload.GetProperty("operationId").GetGuid());
                Assert.Equal("Running", payload.GetProperty("status").GetString());
                Assert.Equal(_occurredAt, payload.GetProperty("occurredAt").GetDateTimeOffset());
            }
        );
    }

    [Fact]
    public void Serialize_OperationProgressChanged_ProducesStableTypeAndPayload()
    {
        Guid operationId = Guid.NewGuid();
        OperationProgressChangedNotification notification = new(
            operationId,
            42,
            "Laser cutting",
            _occurredAt
        );

        AssertSerialized(
            notification,
            "operation-progress-changed.v1",
            payload =>
            {
                Assert.Equal(operationId, payload.GetProperty("operationId").GetGuid());
                Assert.Equal(42, payload.GetProperty("progressPercentage").GetInt32());
                Assert.Equal("Laser cutting", payload.GetProperty("currentPhase").GetString());
                Assert.Equal(_occurredAt, payload.GetProperty("occurredAt").GetDateTimeOffset());
            }
        );
    }

    [Fact]
    public void Serialize_OperationEventAppended_ProducesStableTypeAndPayload()
    {
        Guid eventId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        OperationEventAppendedNotification notification = new(
            eventId,
            operationId,
            MachineOperationEventType.Completed,
            _occurredAt
        );

        AssertSerialized(
            notification,
            "operation-event-appended.v1",
            payload =>
            {
                Assert.Equal(eventId, payload.GetProperty("eventId").GetGuid());
                Assert.Equal(operationId, payload.GetProperty("operationId").GetGuid());
                Assert.Equal("Completed", payload.GetProperty("eventType").GetString());
                Assert.Equal(_occurredAt, payload.GetProperty("occurredAt").GetDateTimeOffset());
            }
        );
    }

    [Fact]
    public void Serialize_MachineAlarmRaised_ProducesStableTypeAndPayload()
    {
        Guid alarmId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        MachineAlarmRaisedNotification notification = new(
            alarmId,
            "M-001",
            operationId,
            _occurredAt
        );

        AssertSerialized(
            notification,
            "machine-alarm-raised.v1",
            payload =>
            {
                Assert.Equal(alarmId, payload.GetProperty("alarmId").GetGuid());
                Assert.Equal("M-001", payload.GetProperty("machineId").GetString());
                Assert.Equal(operationId, payload.GetProperty("operationId").GetGuid());
                Assert.Equal(_occurredAt, payload.GetProperty("occurredAt").GetDateTimeOffset());
            }
        );
    }

    [Fact]
    public void Serialize_MachineAlarmAcknowledged_ProducesStableTypeAndPayload()
    {
        Guid alarmId = Guid.NewGuid();
        MachineAlarmAcknowledgedNotification notification = new(alarmId, "M-001", _occurredAt);

        AssertSerialized(
            notification,
            "machine-alarm-acknowledged.v1",
            payload =>
            {
                Assert.Equal(alarmId, payload.GetProperty("alarmId").GetGuid());
                Assert.Equal("M-001", payload.GetProperty("machineId").GetString());
                Assert.Equal(_occurredAt, payload.GetProperty("occurredAt").GetDateTimeOffset());
            }
        );
    }

    [Fact]
    public void Serialize_MachineAlarmResolved_ProducesStableTypeAndPayload()
    {
        Guid alarmId = Guid.NewGuid();
        MachineAlarmResolvedNotification notification = new(alarmId, "M-001", _occurredAt);

        AssertSerialized(
            notification,
            "machine-alarm-resolved.v1",
            payload =>
            {
                Assert.Equal(alarmId, payload.GetProperty("alarmId").GetGuid());
                Assert.Equal("M-001", payload.GetProperty("machineId").GetString());
                Assert.Equal(_occurredAt, payload.GetProperty("occurredAt").GetDateTimeOffset());
            }
        );
    }

    [Fact]
    public void Serialize_MachineRuntimeStatusChanged_ProducesStableTypeAndPayload()
    {
        Guid operationId = Guid.NewGuid();
        MachineRuntimeStatusChangedNotification notification = new(
            "M-001",
            MachineRuntimeStatus.Running,
            operationId,
            _occurredAt
        );

        AssertSerialized(
            notification,
            "machine-runtime-status-changed.v1",
            payload =>
            {
                Assert.Equal("M-001", payload.GetProperty("machineId").GetString());
                Assert.Equal("Running", payload.GetProperty("status").GetString());
                Assert.Equal(operationId, payload.GetProperty("currentOperationId").GetGuid());
                Assert.Equal(_occurredAt, payload.GetProperty("occurredAt").GetDateTimeOffset());
            }
        );
    }

    [Fact]
    public void Serialize_UnsupportedNotification_ThrowsExplicitError()
    {
        UnsupportedNotification notification = new(_occurredAt);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.Serialize([notification], _createdAt)
        );

        Assert.Contains(nameof(UnsupportedNotification), exception.Message);
    }

    [Fact]
    public void Serialize_MultipleNotificationsInSameBatch_ProducesNonEmptyDistinctIds()
    {
        OperationStatusChangedNotification first = new(
            Guid.NewGuid(),
            MachineOperationStatus.Running,
            _occurredAt
        );
        MachineAlarmResolvedNotification second = new(
            Guid.NewGuid(),
            "M-001",
            _occurredAt.AddMinutes(1)
        );

        var records = _serializer.Serialize([first, second], _createdAt);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.NotEqual(Guid.Empty, record.Id));
        Assert.Equal(records.Count, records.Select(record => record.Id).Distinct().Count());
    }

    private void AssertSerialized(
        ProductionNotification notification,
        string expectedType,
        Action<JsonElement> assertPayload
    )
    {
        var result = Assert.Single(_serializer.Serialize([notification], _createdAt));

        Assert.Equal(expectedType, result.Type);
        Assert.Equal(notification.OccurredAt, result.OccurredAt);
        Assert.Equal(_createdAt, result.CreatedAt);

        using JsonDocument document = JsonDocument.Parse(result.Payload);
        assertPayload(document.RootElement);
    }

    private sealed record UnsupportedNotification(DateTimeOffset OccurredAt)
        : ProductionNotification(OccurredAt);
}
