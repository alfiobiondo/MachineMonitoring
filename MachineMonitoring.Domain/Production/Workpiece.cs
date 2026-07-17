using MachineMonitoring.Domain.Exceptions;

namespace MachineMonitoring.Domain.Production;

public sealed class Workpiece
{
    public Guid Id { get; }

    public Guid ProductionLotId { get; }

    public int SequenceNumber { get; }

    public string Code { get; }

    public string MaterialCode { get; }

    public WorkpieceStatus Status { get; private set; }

    public bool IsSequenceActive { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Workpiece(
        Guid id,
        Guid productionLotId,
        int sequenceNumber,
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

        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                "The workpiece sequence number must be greater than zero."
            );
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(materialCode);

        Id = id;
        ProductionLotId = productionLotId;
        SequenceNumber = sequenceNumber;
        Code = code;
        MaterialCode = materialCode;
        CreatedAt = createdAt;
        Status = WorkpieceStatus.Pending;
        IsSequenceActive = false;
    }

    public void StartSequence(DateTimeOffset startedAt)
    {
        if (
            Status
            is WorkpieceStatus.Completed
                or WorkpieceStatus.Failed
                or WorkpieceStatus.Cancelled
                or WorkpieceStatus.Skipped
        )
        {
            throw new BusinessRuleViolationException(
                $"Workpiece {Code} cannot activate its sequence from status {Status}."
            );
        }

        IsSequenceActive = true;

        if (Status == WorkpieceStatus.Pending)
        {
            Status = WorkpieceStatus.Running;
            StartedAt = startedAt;
        }
    }

    public void DeactivateSequence()
    {
        IsSequenceActive = false;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (
            Status
            is WorkpieceStatus.Completed
                or WorkpieceStatus.Failed
                or WorkpieceStatus.Cancelled
                or WorkpieceStatus.Skipped
        )
        {
            throw new BusinessRuleViolationException(
                $"Workpiece {Code} cannot be completed from status {Status}."
            );
        }

        Status = WorkpieceStatus.Completed;
        IsSequenceActive = false;
        CompletedAt = completedAt;
    }

    public void Fail(DateTimeOffset failedAt)
    {
        if (
            Status
            is WorkpieceStatus.Completed
                or WorkpieceStatus.Failed
                or WorkpieceStatus.Cancelled
                or WorkpieceStatus.Skipped
        )
        {
            throw new BusinessRuleViolationException(
                $"Workpiece {Code} cannot fail from status {Status}."
            );
        }

        Status = WorkpieceStatus.Failed;
        IsSequenceActive = false;
        CompletedAt = failedAt;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (
            Status
            is WorkpieceStatus.Completed
                or WorkpieceStatus.Failed
                or WorkpieceStatus.Cancelled
                or WorkpieceStatus.Skipped
        )
        {
            throw new BusinessRuleViolationException(
                $"Workpiece {Code} cannot be cancelled from status {Status}."
            );
        }

        Status = WorkpieceStatus.Cancelled;
        IsSequenceActive = false;
        CompletedAt = cancelledAt;
    }

    public void Skip(DateTimeOffset skippedAt)
    {
        if (Status != WorkpieceStatus.Pending)
        {
            throw new BusinessRuleViolationException(
                $"Workpiece {Code} cannot be skipped from status {Status}."
            );
        }

        Status = WorkpieceStatus.Skipped;
        IsSequenceActive = false;
        CompletedAt = skippedAt;
    }

    public static Workpiece Restore(
        Guid id,
        Guid productionLotId,
        int sequenceNumber,
        string code,
        string materialCode,
        WorkpieceStatus status,
        bool isSequenceActive,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt
    )
    {
        Workpiece workpiece = new(
            id: id,
            productionLotId: productionLotId,
            sequenceNumber: sequenceNumber,
            code: code,
            materialCode: materialCode,
            createdAt: createdAt
        );

        workpiece.Status = status;
        workpiece.IsSequenceActive = isSequenceActive;
        workpiece.StartedAt = startedAt;
        workpiece.CompletedAt = completedAt;

        return workpiece;
    }
}
