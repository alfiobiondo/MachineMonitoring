using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests.Production;

public sealed class RandomOperationFaultStrategyTests
{
    [Fact]
    public void Evaluate_WhenSimulationDisabled_NeverFaults()
    {
        RandomOperationFaultStrategy strategy = new(
            Options.Create(
                new OperationSimulatorOptions
                {
                    OperationFaultSimulationEnabled = false,
                    OperationFaultProbabilityPercentage = 100,
                }
            )
        );

        OperationFaultDecision decision = strategy.Evaluate(CreateRunningOperation());

        Assert.False(decision.ShouldFault);
    }

    [Fact]
    public void Evaluate_WhenProbabilityIsZero_NeverFaults()
    {
        RandomOperationFaultStrategy strategy = new(
            Options.Create(
                new OperationSimulatorOptions
                {
                    OperationFaultSimulationEnabled = true,
                    OperationFaultProbabilityPercentage = 0,
                }
            )
        );

        OperationFaultDecision decision = strategy.Evaluate(CreateRunningOperation());

        Assert.False(decision.ShouldFault);
    }

    [Fact]
    public void Evaluate_WhenProbabilityIsHundred_AlwaysFaults()
    {
        RandomOperationFaultStrategy strategy = new(
            Options.Create(
                new OperationSimulatorOptions
                {
                    OperationFaultSimulationEnabled = true,
                    OperationFaultProbabilityPercentage = 100,
                }
            )
        );

        OperationFaultDecision decision = strategy.Evaluate(CreateRunningOperation());

        Assert.True(decision.ShouldFault);
        Assert.NotNull(decision.AlarmCode);
        Assert.NotNull(decision.Severity);
        Assert.NotNull(decision.Message);
        Assert.NotNull(decision.Reason);
    }

    private static MachineOperation CreateRunningOperation()
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: Guid.NewGuid(),
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        operation.Start(DateTimeOffset.UtcNow, "Preparing laser");
        return operation;
    }
}
