using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests.Production;

public sealed class RandomOperationProgressStrategyTests
{
    [Fact]
    public void GetNextIncrement_ReturnsValueWithinConfiguredRange()
    {
        RandomOperationProgressStrategy strategy = new(
            Options.Create(
                new OperationSimulatorOptions
                {
                    MinimumProgressIncrement = 7,
                    MaximumProgressIncrement = 9,
                }
            )
        );

        for (int index = 0; index < 50; index++)
        {
            int increment = strategy.GetNextIncrement();

            Assert.InRange(increment, 7, 9);
        }
    }
}
