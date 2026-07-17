using MachineMonitoring.Domain.Exceptions;

namespace MachineMonitoring.Domain.Production;

public sealed class MachineAlarm
{
    public Guid Id { get; }

    public string MachineId { get; }

    public Guid? MachineOperationId { get; }

    public string Code { get; }

    public MachineAlarmSeverity Severity { get; }

    public MachineAlarmStatus Status { get; private set; }

    public string Message { get; }

    public DateTimeOffset RaisedAt { get; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public MachineAlarm(
        Guid id,
        string machineId,
        Guid? machineOperationId,
        string code,
        MachineAlarmSeverity severity,
        string message,
        DateTimeOffset raisedAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The machine alarm ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Id = id;
        MachineId = machineId;
        MachineOperationId = machineOperationId;
        Code = code;
        Severity = severity;
        Message = message;
        RaisedAt = raisedAt;
        Status = MachineAlarmStatus.Active;
    }

    public void Acknowledge(DateTimeOffset acknowledgedAt)
    {
        if (Status != MachineAlarmStatus.Active)
        {
            throw new BusinessRuleViolationException(
                $"Machine alarm {Id} cannot be acknowledged from status {Status}."
            );
        }

        Status = MachineAlarmStatus.Acknowledged;
        AcknowledgedAt = acknowledgedAt;
    }

    public void Resolve(DateTimeOffset resolvedAt, string? resolutionNotes)
    {
        if (Status == MachineAlarmStatus.Resolved)
        {
            throw new BusinessRuleViolationException(
                $"Machine alarm {Id} cannot be resolved from status {Status}."
            );
        }

        Status = MachineAlarmStatus.Resolved;
        ResolvedAt = resolvedAt;
        ResolutionNotes = resolutionNotes;
    }

    public static MachineAlarm Restore(
        Guid id,
        string machineId,
        Guid? machineOperationId,
        string code,
        MachineAlarmSeverity severity,
        MachineAlarmStatus status,
        string message,
        DateTimeOffset raisedAt,
        DateTimeOffset? acknowledgedAt,
        DateTimeOffset? resolvedAt,
        string? resolutionNotes
    )
    {
        MachineAlarm alarm = new(
            id: id,
            machineId: machineId,
            machineOperationId: machineOperationId,
            code: code,
            severity: severity,
            message: message,
            raisedAt: raisedAt
        );

        alarm.Status = status;
        alarm.AcknowledgedAt = acknowledgedAt;
        alarm.ResolvedAt = resolvedAt;
        alarm.ResolutionNotes = resolutionNotes;

        return alarm;
    }
}
