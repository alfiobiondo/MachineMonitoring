using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public interface IMachineFaultStrategy
{
    MachineFaultDecision Evaluate(string machineId, Guid? currentOperationId);
}
