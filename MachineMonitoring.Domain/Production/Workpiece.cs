namespace MachineMonitoring.Domain.Production;

public sealed class Workpiece
{
    public Guid Id { get; }

    public Guid ProductionLotId { get; }

    public string Code { get; }

    public string MaterialCode { get; }

    public WorkpieceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public Workpiece(
        Guid id,
        Guid productionLotId,
        string code,
        string materialCode,
        DateTimeOffset createdAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The workpiece ID cannot be empty.", nameof(id));
        }

        if (productionLotId == Guid.Empty)
        {
            throw new ArgumentException(
                "The production lot ID cannot be empty.",
                nameof(productionLotId)
            );
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(materialCode);

        Id = id;
        ProductionLotId = productionLotId;
        Code = code;
        MaterialCode = materialCode;
        CreatedAt = createdAt;
        Status = WorkpieceStatus.Pending;
    }

    public void Start()
    {
        if (Status != WorkpieceStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Workpiece {Code} cannot be started from status {Status}."
            );
        }

        Status = WorkpieceStatus.InProgress;
    }

    public void Complete()
    {
        if (Status != WorkpieceStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Workpiece {Code} cannot be completed from status {Status}."
            );
        }

        Status = WorkpieceStatus.Completed;
    }

    public void Fail()
    {
        if (Status != WorkpieceStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Workpiece {Code} cannot fail from status {Status}."
            );
        }

        Status = WorkpieceStatus.Failed;
    }

    public void Cancel()
    {
        if (
            Status
            is WorkpieceStatus.Completed
                or WorkpieceStatus.Failed
                or WorkpieceStatus.Cancelled
        )
        {
            throw new InvalidOperationException(
                $"Workpiece {Code} cannot be cancelled from status {Status}."
            );
        }

        Status = WorkpieceStatus.Cancelled;
    }
}
