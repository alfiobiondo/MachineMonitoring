using MachineMonitoring.Application.Configuration;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application.Production;

public sealed class RandomOperationProgressStrategy : IOperationProgressStrategy
{
    private readonly OperationSimulatorOptions _options;
    private readonly Random _random;

    public RandomOperationProgressStrategy(IOptions<OperationSimulatorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _random = Random.Shared;
    }

    public int GetNextIncrement()
    {
        return _random.Next(
            _options.MinimumProgressIncrement,
            _options.MaximumProgressIncrement + 1
        );
    }
}
