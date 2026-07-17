using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests.Production;

public sealed class RandomMachineFaultStrategyTests
{
    [Fact]
    public void Evaluate_WhenSimulationDisabled_NeverFaults()
    {
        RandomMachineFaultStrategy strategy = new(
            Options.Create(
                new OperationSimulatorOptions
                {
                    MachineFaultSimulationEnabled = false,
                    MachineFaultProbabilityPercentage = 100,
                }
            )
        );

        MachineFaultDecision decision = strategy.Evaluate("M-001", Guid.NewGuid());

        Assert.False(decision.ShouldFault);
    }

    [Fact]
    public void Evaluate_WhenProbabilityIsZero_NeverFaults()
    {
        RandomMachineFaultStrategy strategy = new(
            Options.Create(
                new OperationSimulatorOptions
                {
                    MachineFaultSimulationEnabled = true,
                    MachineFaultProbabilityPercentage = 0,
                }
            )
        );

        MachineFaultDecision decision = strategy.Evaluate("M-001", null);

        Assert.False(decision.ShouldFault);
    }

    [Fact]
    public void Evaluate_WhenProbabilityIsHundred_AlwaysFaults()
    {
        RandomMachineFaultStrategy strategy = new(
            Options.Create(
                new OperationSimulatorOptions
                {
                    MachineFaultSimulationEnabled = true,
                    MachineFaultProbabilityPercentage = 100,
                }
            )
        );

        MachineFaultDecision decision = strategy.Evaluate("M-001", null);

        Assert.True(decision.ShouldFault);
        Assert.Equal("SIM_MACHINE_FAULT", decision.AlarmCode);
    }
}
