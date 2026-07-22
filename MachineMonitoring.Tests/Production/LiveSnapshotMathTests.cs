using MachineMonitoring.Application.Production;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Tests.Production;

public sealed class LiveSnapshotMathTests
{
    [Theory]
    [InlineData(MachineOperationStatus.Queued, 37, 0)]
    [InlineData(MachineOperationStatus.Running, 37, 37)]
    [InlineData(MachineOperationStatus.Paused, 37, 37)]
    [InlineData(MachineOperationStatus.Faulted, 37, 37)]
    [InlineData(MachineOperationStatus.Failed, 37, 37)]
    [InlineData(MachineOperationStatus.Cancelled, 37, 37)]
    [InlineData(MachineOperationStatus.Completed, 37, 100)]
    [InlineData(MachineOperationStatus.Skipped, 37, 100)]
    [InlineData(MachineOperationStatus.Running, -5, 0)]
    [InlineData(MachineOperationStatus.Running, 150, 100)]
    public void GetOperationContributionPercentage_ReturnsExpectedContribution(
        MachineOperationStatus status,
        int progressPercentage,
        decimal expectedContribution
    )
    {
        decimal contribution = LiveSnapshotMath.GetOperationContributionPercentage(
            status,
            progressPercentage
        );

        Assert.Equal(expectedContribution, contribution);
    }

    [Fact]
    public void CalculateAggregateProgress_WhenThereAreNoOperations_ReturnsZero()
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress([]);

        Assert.Equal(0m, aggregate.ProgressPercentage);
        Assert.Equal(0, aggregate.CompletedOperations);
        Assert.Equal(0, aggregate.TotalOperations);
    }

    [Theory]
    [InlineData(MachineOperationStatus.Queued, 0, 0, 0)]
    [InlineData(MachineOperationStatus.Running, 40, 40, 0)]
    [InlineData(MachineOperationStatus.Running, 70, 70, 0)]
    [InlineData(MachineOperationStatus.Completed, 0, 100, 1)]
    public void CalculateAggregateProgress_ForSingleOperationMatchesOperationContribution(
        MachineOperationStatus status,
        int progressPercentage,
        decimal expectedProgress,
        int expectedCompletedOperations
    )
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [new LiveOperationAggregateInput(status, progressPercentage)]
        );

        Assert.Equal(expectedProgress, aggregate.ProgressPercentage);
        Assert.Equal(expectedCompletedOperations, aggregate.CompletedOperations);
        Assert.Equal(1, aggregate.TotalOperations);
    }

    [Fact]
    public void CalculateAggregateProgress_ForTwoByTwoWithOneRunningOperation_IsWeightedByOperationCount()
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Running, 50),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
            ]
        );

        Assert.Equal(12.50m, aggregate.ProgressPercentage);
        Assert.Equal(0, aggregate.CompletedOperations);
        Assert.Equal(4, aggregate.TotalOperations);
    }

    [Fact]
    public void CalculateAggregateProgress_ForTwoByTwoWithCompletedAndRunningOperations_IsWeightedByOperationCount()
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Running, 50),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
            ]
        );

        Assert.Equal(37.50m, aggregate.ProgressPercentage);
        Assert.Equal(1, aggregate.CompletedOperations);
        Assert.Equal(4, aggregate.TotalOperations);
    }

    [Fact]
    public void CalculateAggregateProgress_WhenCurrentWorkpieceCompletesAndNextWorkpieceStarts_RemainsWeightedByOperationCount()
    {
        LiveProgressAggregate lotAggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Running, 25),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
            ]
        );
        LiveProgressAggregate currentWorkpieceAggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Running, 25),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
            ]
        );

        Assert.Equal(56.25m, lotAggregate.ProgressPercentage);
        Assert.Equal(2, lotAggregate.CompletedOperations);
        Assert.Equal(4, lotAggregate.TotalOperations);
        Assert.Equal(12.50m, currentWorkpieceAggregate.ProgressPercentage);
        Assert.Equal(0, currentWorkpieceAggregate.CompletedOperations);
        Assert.Equal(2, currentWorkpieceAggregate.TotalOperations);
    }

    [Fact]
    public void CalculateAggregateProgress_WhenAllOperationsComplete_ReturnsOneHundredPercent()
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 40),
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 70),
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 100),
            ]
        );

        Assert.Equal(100m, aggregate.ProgressPercentage);
        Assert.Equal(4, aggregate.CompletedOperations);
        Assert.Equal(4, aggregate.TotalOperations);
    }

    [Fact]
    public void CalculateAggregateProgress_SkippedCountsAsCompleteAndFailedOrCancelledKeepTheirProgress()
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Skipped, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Failed, 70),
                new LiveOperationAggregateInput(MachineOperationStatus.Cancelled, 30),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
            ]
        );

        Assert.Equal(50.00m, aggregate.ProgressPercentage);
        Assert.Equal(1, aggregate.CompletedOperations);
        Assert.Equal(4, aggregate.TotalOperations);
    }

    [Fact]
    public void CalculateAggregateProgress_PreservesFractionalAverage()
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 100),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Queued, 0),
            ]
        );

        Assert.Equal(33.33m, aggregate.ProgressPercentage);
        Assert.Equal(1, aggregate.CompletedOperations);
        Assert.Equal(3, aggregate.TotalOperations);
    }

    [Fact]
    public void GetOperationContributionPercentage_WhenStatusIsUnknown_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LiveSnapshotMath.GetOperationContributionPercentage((MachineOperationStatus)999, 42)
        );
    }

    [Fact]
    public void CalculateAggregateProgress_CompletedCountIncludesCompletedAndSkipped()
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 100),
                new LiveOperationAggregateInput(MachineOperationStatus.Skipped, 0),
                new LiveOperationAggregateInput(MachineOperationStatus.Running, 20),
            ]
        );

        Assert.Equal(2, aggregate.CompletedOperations);
    }

    [Fact]
    public void CalculateAggregateProgress_FailedAndCancelledDoNotCountAsCompleted()
    {
        LiveProgressAggregate aggregate = LiveSnapshotMath.CalculateAggregateProgress(
            [
                new LiveOperationAggregateInput(MachineOperationStatus.Failed, 80),
                new LiveOperationAggregateInput(MachineOperationStatus.Cancelled, 30),
                new LiveOperationAggregateInput(MachineOperationStatus.Completed, 100),
            ]
        );

        Assert.Equal(1, aggregate.CompletedOperations);
        Assert.Equal(70.00m, aggregate.ProgressPercentage);
    }

    [Fact]
    public void GetPositionBySequence_IgnoresNonContiguousSequenceNumbers()
    {
        SequencedItem target = new(Guid.NewGuid(), 9);
        SequencedItem[] items =
        [
            new SequencedItem(Guid.NewGuid(), 3),
            target,
            new SequencedItem(Guid.NewGuid(), 20),
        ];

        int position = LiveSnapshotMath.GetPositionBySequence(
            items,
            item => item.SequenceNumber,
            item => item.Id,
            target.Id
        );

        Assert.Equal(2, position);
    }

    private sealed record SequencedItem(Guid Id, int SequenceNumber);
}
