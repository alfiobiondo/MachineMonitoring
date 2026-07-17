namespace MachineMonitoring.Application.Production;

public sealed class NoMachineFaultStrategy : IMachineFaultStrategy
{
    public MachineFaultDecision Evaluate(string machineId, Guid? currentOperationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        return MachineFaultDecision.None;
    }
}
