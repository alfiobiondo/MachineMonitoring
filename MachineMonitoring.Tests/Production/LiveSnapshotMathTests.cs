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
