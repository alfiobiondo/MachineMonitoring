using MachineMonitoring.Domain.Exceptions;

namespace MachineMonitoring.Domain.Production;

public sealed class MachineRuntimeState
{
    public string MachineId { get; }

    public MachineRuntimeStatus Status { get; private set; }

    public Guid? CurrentOperationId { get; private set; }

    public DateTimeOffset LastChangedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public Guid? ActiveAlarmId { get; private set; }

    public int Version { get; private set; }

    public MachineRuntimeState(
        string machineId,
        MachineRuntimeStatus status,
        Guid? currentOperationId,
        DateTimeOffset lastChangedAt,
        string? failureReason,
        Guid? activeAlarmId,
        int version = 1
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        MachineId = machineId;
        Status = status;
        CurrentOperationId = currentOperationId;
        LastChangedAt = lastChangedAt;
        FailureReason = failureReason;
        ActiveAlarmId = activeAlarmId;
        Version = version;
    }

    public static MachineRuntimeState CreateAvailable(
        string machineId,
        DateTimeOffset changedAt,
        int version = 1
    )
    {
        return new MachineRuntimeState(
            machineId: machineId,
            status: MachineRuntimeStatus.Available,
            currentOperationId: null,
            lastChangedAt: changedAt,
            failureReason: null,
            activeAlarmId: null,
            version: version
        );
    }

    public bool CanAcceptWork() =>
        Status
            is MachineRuntimeStatus.Available
                or MachineRuntimeStatus.Paused
                or MachineRuntimeStatus.Running;

    public void StartOperation(Guid operationId, DateTimeOffset changedAt)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("The operation ID cannot be empty.", nameof(operationId));
        }

        EnsureNotInTerminalUnavailableState();

        if (CurrentOperationId is Guid currentOperationId && currentOperationId != operationId)
        {
            throw new BusinessRuleViolationException(
                $"Machine {MachineId} is already assigned to operation {currentOperationId}."
            );
        }

        Status = MachineRuntimeStatus.Running;
        CurrentOperationId = operationId;
        LastChangedAt = changedAt;
        FailureReason = null;
        BumpVersion();
    }

    public void PauseOperation(Guid operationId, DateTimeOffset changedAt)
    {
        EnsureAssignedTo(operationId);

        Status = MachineRuntimeStatus.Paused;
        LastChangedAt = changedAt;
        BumpVersion();
    }

    public void ResumeOperation(Guid operationId, DateTimeOffset changedAt)
    {
        EnsureAssignedTo(operationId);
        EnsureNotInTerminalUnavailableState();

        Status = MachineRuntimeStatus.Running;
        LastChangedAt = changedAt;
        BumpVersion();
    }

    public void Fault(
        Guid? operationId,
        Guid alarmId,
        string failureReason,
        DateTimeOffset changedAt
    )
    {
        if (alarmId == Guid.Empty)
        {
            throw new ArgumentException("The alarm ID cannot be empty.", nameof(alarmId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        if (operationId is Guid requestedOperationId)
        {
            if (
                CurrentOperationId is Guid currentOperationId
                && currentOperationId != requestedOperationId
            )
            {
                throw new BusinessRuleViolationException(
                    $"Machine {MachineId} is assigned to operation {currentOperationId}, not {requestedOperationId}."
                );
            }

            CurrentOperationId = requestedOperationId;
        }

        Status = MachineRuntimeStatus.Faulted;
        FailureReason = failureReason;
        ActiveAlarmId = alarmId;
        LastChangedAt = changedAt;
        BumpVersion();
    }

    public void ResolveFault(Guid? operationId, DateTimeOffset changedAt)
    {
        if (operationId is Guid requestedOperationId)
        {
            EnsureAssignedTo(requestedOperationId);
            Status = MachineRuntimeStatus.Paused;
        }
        else
        {
            Status = MachineRuntimeStatus.Available;
            CurrentOperationId = null;
        }

        FailureReason = null;
        ActiveAlarmId = null;
        LastChangedAt = changedAt;
        BumpVersion();
    }

    public void CompleteOperation(Guid operationId, DateTimeOffset changedAt)
    {
        EnsureAssignedTo(operationId);

        Status = MachineRuntimeStatus.Available;
        CurrentOperationId = null;
        FailureReason = null;
        ActiveAlarmId = null;
        LastChangedAt = changedAt;
        BumpVersion();
    }

    public void SetMaintenance(DateTimeOffset changedAt, string? reason)
    {
        EnsureNoActiveOperationAssignment();

        Status = MachineRuntimeStatus.Maintenance;
        FailureReason = reason;
        LastChangedAt = changedAt;
        BumpVersion();
    }

    public void SetOffline(DateTimeOffset changedAt, string? reason)
    {
        EnsureNoActiveOperationAssignment();

        Status = MachineRuntimeStatus.Offline;
        FailureReason = reason;
        LastChangedAt = changedAt;
        BumpVersion();
    }

    public void SetAvailable(DateTimeOffset changedAt)
    {
        Status = MachineRuntimeStatus.Available;
        CurrentOperationId = null;
        FailureReason = null;
        ActiveAlarmId = null;
        LastChangedAt = changedAt;
        BumpVersion();
    }

    public static MachineRuntimeState Restore(
        string machineId,
        MachineRuntimeStatus status,
        Guid? currentOperationId,
        DateTimeOffset lastChangedAt,
        string? failureReason,
        Guid? activeAlarmId,
        int version
    )
    {
        return new MachineRuntimeState(
            machineId: machineId,
            status: status,
            currentOperationId: currentOperationId,
            lastChangedAt: lastChangedAt,
            failureReason: failureReason,
            activeAlarmId: activeAlarmId,
            version: version
        );
    }

    private void EnsureAssignedTo(Guid operationId)
    {
        if (CurrentOperationId != operationId)
        {
            throw new BusinessRuleViolationException(
                $"Machine {MachineId} is not assigned to operation {operationId}."
            );
        }
    }

    private void EnsureNoActiveOperationAssignment()
    {
        if (CurrentOperationId is not null)
        {
            throw new BusinessRuleViolationException(
                $"Machine {MachineId} cannot change availability while operation {CurrentOperationId} is assigned."
            );
        }
    }

    private void EnsureNotInTerminalUnavailableState()
    {
        if (
            Status
            is MachineRuntimeStatus.Faulted
                or MachineRuntimeStatus.Maintenance
                or MachineRuntimeStatus.Offline
        )
        {
            throw new BusinessRuleViolationException(
                $"Machine {MachineId} cannot accept work while it is {Status}."
            );
        }
    }

    private void BumpVersion()
    {
        Version++;
    }
}
