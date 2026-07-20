using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Exceptions;

namespace MachineMonitoring.Tests.Production;

public sealed class MachineOperationDomainTests
{
    [Fact]
    public void Constructor_WhenSequenceNumberIsZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MachineOperation(
                id: Guid.NewGuid(),
                workpieceId: Guid.NewGuid(),
                sequenceNumber: 0,
                machineId: "M-001",
                type: MachineOperationType.LaserCutting,
                createdAt: DateTimeOffset.UtcNow
            )
        );
    }

    [Fact]
    public void UpdateProgress_WhenRunning_AllowsIncreasingProgress()
    {
        MachineOperation operation = CreateRunningOperation();

        operation.UpdateProgress(20, "Phase 1");
        operation.UpdateProgress(40, "Phase 2");

        Assert.Equal(40, operation.ProgressPercentage);
        Assert.Equal("Phase 2", operation.CurrentPhase);
    }

    [Fact]
    public void UpdateProgress_WhenRunning_AllowsEqualProgress()
    {
        MachineOperation operation = CreateRunningOperation();

        operation.UpdateProgress(20, "Phase 1");
        operation.UpdateProgress(20, "Phase 1 repeat");

        Assert.Equal(20, operation.ProgressPercentage);
        Assert.Equal("Phase 1 repeat", operation.CurrentPhase);
    }

    [Fact]
    public void UpdateProgress_WhenRunning_AllowsZeroToZero()
    {
        MachineOperation operation = CreateRunningOperation();

        operation.UpdateProgress(0, "Initial phase");

        Assert.Equal(0, operation.ProgressPercentage);
        Assert.Equal("Initial phase", operation.CurrentPhase);
    }

    [Fact]
    public void UpdateProgress_WhenProgressDecreases_ThrowsBusinessRuleViolation()
    {
        MachineOperation operation = CreateRunningOperation();
        operation.UpdateProgress(20, "Phase 1");

        BusinessRuleViolationException exception = Assert.Throws<BusinessRuleViolationException>(
            () => operation.UpdateProgress(10, "Phase 0")
        );

        Assert.Equal(20, operation.ProgressPercentage);
        Assert.Contains("cannot be reduced", exception.Message);
        Assert.Contains("20%", exception.Message);
        Assert.Contains("10%", exception.Message);
    }

    [Fact]
    public void UpdateProgress_WhenProgressIsAboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        MachineOperation operation = CreateRunningOperation();

        Assert.Throws<ArgumentOutOfRangeException>(() => operation.UpdateProgress(100, "Phase"));
    }

    [Fact]
    public void UpdateProgress_WhenOperationIsNotRunning_ThrowsBusinessRuleViolation()
    {
        MachineOperation operation = new(
            id: Guid.NewGuid(),
            workpieceId: Guid.NewGuid(),
            sequenceNumber: 1,
            machineId: "M-001",
            type: MachineOperationType.LaserCutting,
            createdAt: DateTimeOffset.UtcNow
        );

        Assert.Throws<BusinessRuleViolationException>(() => operation.UpdateProgress(10, "Phase"));
    }

    [Fact]
    public void Complete_WhenOperationIsRunning_SetsProgressToOneHundred()
    {
        MachineOperation operation = CreateRunningOperation();
        operation.UpdateProgress(75, "Finishing");

        operation.Complete(DateTimeOffset.UtcNow);

        Assert.Equal(MachineOperationStatus.Completed, operation.Status);
        Assert.Equal(100, operation.ProgressPercentage);
        Assert.Equal("Completed", operation.CurrentPhase);
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

        operation.Start(DateTimeOffset.UtcNow, "Preparing");

        return operation;
    }
}
