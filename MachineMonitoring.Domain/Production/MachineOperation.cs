namespace MachineMonitoring.Domain.Production;

public sealed class MachineOperation
{
    public Guid Id { get; }

    public Guid WorkpieceId { get; }

    public string MachineId { get; }

    public MachineOperationType Type { get; }

    public MachineOperationStatus Status { get; private set; }

    public int ProgressPercentage { get; private set; }

    public string? CurrentPhase { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public MachineOperation(
        Guid id,
        Guid workpieceId,
        string machineId,
        MachineOperationType type,
        DateTimeOffset createdAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The operation ID cannot be empty.", nameof(id));
        }

        if (workpieceId == Guid.Empty)
        {
            throw new ArgumentException("The workpiece ID cannot be empty.", nameof(workpieceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        Id = id;
        WorkpieceId = workpieceId;
        MachineId = machineId;
        Type = type;
        CreatedAt = createdAt;

        Status = MachineOperationStatus.Queued;
        ProgressPercentage = 0;
    }

    public void Start(DateTimeOffset startedAt, string initialPhase)
    {
        if (Status != MachineOperationStatus.Queued)
        {
            throw new InvalidOperationException(
                $"Operation {Id} cannot be started from status {Status}."
            );
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(initialPhase);

        Status = MachineOperationStatus.Running;
        StartedAt = startedAt;
        CurrentPhase = initialPhase;
    }

    public void UpdateProgress(int progressPercentage, string currentPhase)
    {
        if (Status != MachineOperationStatus.Running)
        {
            throw new InvalidOperationException(
                $"Progress cannot be updated while operation {Id} is {Status}."
            );
        }

        if (progressPercentage is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progressPercentage),
                "Progress must be between 0 and 99 while the operation is running."
            );
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currentPhase);

        ProgressPercentage = progressPercentage;
        CurrentPhase = currentPhase;
    }

    public void Pause()
    {
        if (Status != MachineOperationStatus.Running)
        {
            throw new InvalidOperationException(
                $"Operation {Id} cannot be paused from status {Status}."
            );
        }

        Status = MachineOperationStatus.Paused;
    }

    public void Resume()
    {
        if (Status != MachineOperationStatus.Paused)
        {
            throw new InvalidOperationException(
                $"Operation {Id} cannot be resumed from status {Status}."
            );
        }

        Status = MachineOperationStatus.Running;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (Status != MachineOperationStatus.Running)
        {
            throw new InvalidOperationException(
                $"Operation {Id} cannot be completed from status {Status}."
            );
        }

        Status = MachineOperationStatus.Completed;
        ProgressPercentage = 100;
        CurrentPhase = "Completed";
        CompletedAt = completedAt;
    }

    public void Fail(string failureReason)
    {
        if (Status is not MachineOperationStatus.Running and not MachineOperationStatus.Paused)
        {
            throw new InvalidOperationException(
                $"Operation {Id} cannot fail from status {Status}."
            );
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        Status = MachineOperationStatus.Failed;
        FailureReason = failureReason;
    }

    public void Cancel()
    {
        if (
            Status
            is MachineOperationStatus.Completed
                or MachineOperationStatus.Failed
                or MachineOperationStatus.Cancelled
        )
        {
            throw new InvalidOperationException(
                $"Operation {Id} cannot be cancelled from status {Status}."
            );
        }

        Status = MachineOperationStatus.Cancelled;
    }

    public static MachineOperation Restore(
        Guid id,
        Guid workpieceId,
        string machineId,
        MachineOperationType type,
        MachineOperationStatus status,
        int progressPercentage,
        string? currentPhase,
        string? failureReason,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt
    )
    {
        MachineOperation operation = new(
            id: id,
            workpieceId: workpieceId,
            machineId: machineId,
            type: type,
            createdAt: createdAt
        );

        operation.Status = status;
        operation.ProgressPercentage = progressPercentage;
        operation.CurrentPhase = currentPhase;
        operation.FailureReason = failureReason;
        operation.StartedAt = startedAt;
        operation.CompletedAt = completedAt;

        return operation;
    }
}
