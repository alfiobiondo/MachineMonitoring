using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed record OperationFaultDecision(
    bool ShouldFault,
    string? AlarmCode,
    MachineAlarmSeverity? Severity,
    string? Message,
    string? Reason
)
{
    public static OperationFaultDecision None { get; } = new(false, null, null, null, null);
}
