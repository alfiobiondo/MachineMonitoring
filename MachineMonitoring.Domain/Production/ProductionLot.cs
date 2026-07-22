using MachineMonitoring.Domain.Exceptions;

namespace MachineMonitoring.Domain.Production;

public sealed class ProductionLot
{
    public Guid Id { get; }

    public string Code { get; }

    public int PlannedQuantity { get; }

    public ProductionLotStatus Status { get; private set; }

    public ProductionLotExecutionMode ExecutionMode { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public ProductionLot(Guid id, string code, int plannedQuantity, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The production lot ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (plannedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plannedQuantity),
                "The planned quantity must be greater than zero."
            );
        }

        Id = id;
        Code = code;
        PlannedQuantity = plannedQuantity;
        CreatedAt = createdAt;
        Status = ProductionLotStatus.Planned;
        ExecutionMode = ProductionLotExecutionMode.None;
    }

    public void Start(DateTimeOffset startedAt)
    {
        StartManual(startedAt);
    }

    public void StartManual(DateTimeOffset startedAt)
    {
        StartCore(startedAt);
        ExecutionMode = ProductionLotExecutionMode.None;
    }

    public void StartLotSequence(DateTimeOffset startedAt)
    {
        StartCore(startedAt);
        ExecutionMode = ProductionLotExecutionMode.LotSequence;
    }

    public void StopLotSequence()
    {
        ExecutionMode = ProductionLotExecutionMode.None;
    }

    private void StartCore(DateTimeOffset startedAt)
    {
        if (Status is ProductionLotStatus.Completed or ProductionLotStatus.Cancelled or ProductionLotStatus.Failed)
        {
            throw new BusinessRuleViolationException(
                $"Production lot {Code} cannot be started from status {Status}."
            );
        }

        Status = ProductionLotStatus.Running;

        if (StartedAt is null)
        {
            StartedAt = startedAt;
        }
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (Status is ProductionLotStatus.Completed or ProductionLotStatus.Cancelled or ProductionLotStatus.Failed)
        {
            throw new BusinessRuleViolationException(
                $"Production lot {Code} cannot be completed from status {Status}."
            );
        }

        Status = ProductionLotStatus.Completed;
        ExecutionMode = ProductionLotExecutionMode.None;
        CompletedAt = completedAt;
    }

    public void Fail(DateTimeOffset failedAt)
    {
        if (Status is ProductionLotStatus.Completed or ProductionLotStatus.Cancelled or ProductionLotStatus.Failed)
        {
            throw new BusinessRuleViolationException(
                $"Production lot {Code} cannot fail from status {Status}."
            );
        }

        Status = ProductionLotStatus.Failed;
        ExecutionMode = ProductionLotExecutionMode.None;
        CompletedAt = failedAt;
    }

    public void Cancel()
    {
        if (
            Status
            is ProductionLotStatus.Completed
                or ProductionLotStatus.Cancelled
                or ProductionLotStatus.Failed
        )
        {
            throw new BusinessRuleViolationException(
                $"Production lot {Code} cannot be cancelled from status {Status}."
            );
        }

        Status = ProductionLotStatus.Cancelled;
        ExecutionMode = ProductionLotExecutionMode.None;
    }

    public static ProductionLot Restore(
        Guid id,
        string code,
        int plannedQuantity,
        ProductionLotStatus status,
        ProductionLotExecutionMode executionMode,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt
    )
    {
        ProductionLot lot = new(
            id: id,
            code: code,
            plannedQuantity: plannedQuantity,
            createdAt: createdAt
        );

        lot.Status = status;
        lot.ExecutionMode = executionMode;
        lot.StartedAt = startedAt;
        lot.CompletedAt = completedAt;

        return lot;
    }
}
