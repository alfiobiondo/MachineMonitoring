namespace MachineMonitoring.Application.Production;

public interface IOperationProgressStrategy
{
    int GetNextIncrement();
}
