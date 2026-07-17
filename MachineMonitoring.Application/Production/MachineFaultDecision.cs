using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed record MachineFaultDecision(
    bool ShouldFault,
    string? AlarmCode,
    MachineAlarmSeverity? Severity,
    string? Message,
    string? Reason
)
{
    public static MachineFaultDecision None { get; } = new(false, null, null, null, null);
}
