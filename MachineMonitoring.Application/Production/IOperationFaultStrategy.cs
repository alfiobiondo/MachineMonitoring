using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public interface IOperationFaultStrategy
{
    OperationFaultDecision Evaluate(MachineOperation operation);
}
