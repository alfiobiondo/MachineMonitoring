using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public sealed class NoOperationFaultStrategy : IOperationFaultStrategy
{
    public OperationFaultDecision Evaluate(MachineOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return OperationFaultDecision.None;
    }
}
