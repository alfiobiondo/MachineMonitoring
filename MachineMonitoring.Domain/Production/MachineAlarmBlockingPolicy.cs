namespace MachineMonitoring.Domain.Production;

public static class MachineAlarmBlockingPolicy
{
    public static bool IsBlockingSeverity(MachineAlarmSeverity severity)
    {
        return severity is MachineAlarmSeverity.Error or MachineAlarmSeverity.Critical;
    }

    public static bool IsBlocking(MachineAlarm alarm)
    {
        ArgumentNullException.ThrowIfNull(alarm);

        return alarm.Status != MachineAlarmStatus.Resolved && IsBlockingSeverity(alarm.Severity);
    }
}
