using MachineMonitoring.Application.Production;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class FakeProductionTransactionManager : IProductionTransactionManager
{
    public Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation(cancellationToken);
    }
}
