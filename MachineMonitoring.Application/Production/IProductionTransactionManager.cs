namespace MachineMonitoring.Application.Production;

public interface IProductionTransactionManager
{
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken
    );
}
