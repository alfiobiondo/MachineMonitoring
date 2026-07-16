using MachineMonitoring.Domain.Exceptions;

namespace MachineMonitoring.Domain.Production;

public sealed class ProductionLot
{
    public Guid Id { get; }

    public string Code { get; }

    public int PlannedQuantity { get; }

    public ProductionLotStatus Status { get; private set; }

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
    }

    public void Start(DateTimeOffset startedAt)
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
    }

    public static ProductionLot Restore(
        Guid id,
        string code,
        int plannedQuantity,
        ProductionLotStatus status,
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
        lot.StartedAt = startedAt;
        lot.CompletedAt = completedAt;

        return lot;
    }
}
