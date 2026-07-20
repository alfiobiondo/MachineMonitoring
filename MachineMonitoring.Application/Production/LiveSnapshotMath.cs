using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

public static class LiveSnapshotMath
{
    public static decimal GetOperationContributionPercentage(MachineOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return GetOperationContributionPercentage(operation.Status, operation.ProgressPercentage);
    }

    public static decimal GetOperationContributionPercentage(
        MachineOperationStatus status,
        int progressPercentage
    )
    {
        return status switch
        {
            MachineOperationStatus.Queued => 0m,
            MachineOperationStatus.Running => ClampPercentage(progressPercentage),
            MachineOperationStatus.Paused => ClampPercentage(progressPercentage),
            MachineOperationStatus.Faulted => ClampPercentage(progressPercentage),
            MachineOperationStatus.Failed => ClampPercentage(progressPercentage),
            MachineOperationStatus.Cancelled => ClampPercentage(progressPercentage),
            MachineOperationStatus.Completed => 100m,
            MachineOperationStatus.Skipped => 100m,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported operation status."),
        };
    }

    public static LiveProgressAggregate CalculateAggregateProgress(
        IEnumerable<LiveOperationAggregateInput> operations
    )
    {
        ArgumentNullException.ThrowIfNull(operations);

        LiveOperationAggregateInput[] items = operations.ToArray();

        if (items.Length == 0)
        {
            return new LiveProgressAggregate(
                ProgressPercentage: 0m,
                CompletedOperations: 0,
                TotalOperations: 0
            );
        }

        decimal totalContribution = items.Sum(item =>
            GetOperationContributionPercentage(item.Status, item.ProgressPercentage)
        );
        int completedOperations = items.Count(item =>
            item.Status is MachineOperationStatus.Completed or MachineOperationStatus.Skipped
        );
        decimal progressPercentage = decimal.Round(
            totalContribution / items.Length,
            2,
            MidpointRounding.AwayFromZero
        );

        return new LiveProgressAggregate(
            ProgressPercentage: progressPercentage,
            CompletedOperations: completedOperations,
            TotalOperations: items.Length
        );
    }

    public static int GetPositionBySequence<T>(
        IEnumerable<T> items,
        Func<T, int> sequenceSelector,
        Func<T, Guid> idSelector,
        Guid itemId
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sequenceSelector);
        ArgumentNullException.ThrowIfNull(idSelector);

        T[] orderedItems = items
            .OrderBy(sequenceSelector)
            .ThenBy(idSelector)
            .ToArray();

        int index = Array.FindIndex(orderedItems, item => idSelector(item) == itemId);

        if (index < 0)
        {
            throw new InvalidOperationException($"Item {itemId} was not found in the sequence.");
        }

        return index + 1;
    }

    private static decimal ClampPercentage(int progressPercentage)
    {
        return decimal.Clamp(progressPercentage, 0m, 100m);
    }
}

public readonly record struct LiveOperationAggregateInput(
    MachineOperationStatus Status,
    int ProgressPercentage
);

public readonly record struct LiveProgressAggregate(
    decimal ProgressPercentage,
    int CompletedOperations,
    int TotalOperations
);
