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
        if (Status != ProductionLotStatus.Planned)
        {
            throw new InvalidOperationException(
                $"Production lot {Code} cannot be started from status {Status}."
            );
        }

        Status = ProductionLotStatus.InProgress;
        StartedAt = startedAt;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (Status != ProductionLotStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Production lot {Code} cannot be completed from status {Status}."
            );
        }

        Status = ProductionLotStatus.Completed;
        CompletedAt = completedAt;
    }

    public void Cancel()
    {
        if (Status is ProductionLotStatus.Completed or ProductionLotStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Production lot {Code} cannot be cancelled from status {Status}."
            );
        }

        Status = ProductionLotStatus.Cancelled;
    }
}
