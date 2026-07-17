using MachineMonitoring.Domain.Exceptions;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Tests.Production;

public sealed class MachineRuntimeStateTests
{
    [Fact]
    public void StartOperation_FromAvailable_SetsRunningAndCurrentOperation()
    {
        MachineRuntimeState state = MachineRuntimeState.CreateAvailable(
            "M-001",
            DateTimeOffset.UtcNow
        );
        Guid operationId = Guid.NewGuid();

        state.StartOperation(operationId, DateTimeOffset.UtcNow);

        Assert.Equal(MachineRuntimeStatus.Running, state.Status);
        Assert.Equal(operationId, state.CurrentOperationId);
    }

    [Fact]
    public void PauseOperation_FromRunning_SetsPaused()
    {
        MachineRuntimeState state = MachineRuntimeState.CreateAvailable(
            "M-001",
            DateTimeOffset.UtcNow
        );
        Guid operationId = Guid.NewGuid();
        state.StartOperation(operationId, DateTimeOffset.UtcNow);

        state.PauseOperation(operationId, DateTimeOffset.UtcNow);

        Assert.Equal(MachineRuntimeStatus.Paused, state.Status);
        Assert.Equal(operationId, state.CurrentOperationId);
    }

    [Fact]
    public void ResumeOperation_FromPaused_SetsRunning()
    {
        MachineRuntimeState state = MachineRuntimeState.CreateAvailable(
            "M-001",
            DateTimeOffset.UtcNow
        );
        Guid operationId = Guid.NewGuid();
        state.StartOperation(operationId, DateTimeOffset.UtcNow);
        state.PauseOperation(operationId, DateTimeOffset.UtcNow);

        state.ResumeOperation(operationId, DateTimeOffset.UtcNow);

        Assert.Equal(MachineRuntimeStatus.Running, state.Status);
    }

    [Fact]
    public void Fault_FromRunning_SetsFaultedAndAlarm()
    {
        MachineRuntimeState state = MachineRuntimeState.CreateAvailable(
            "M-001",
            DateTimeOffset.UtcNow
        );
        Guid operationId = Guid.NewGuid();
        Guid alarmId = Guid.NewGuid();
        state.StartOperation(operationId, DateTimeOffset.UtcNow);

        state.Fault(operationId, alarmId, "Cooling failure", DateTimeOffset.UtcNow);

        Assert.Equal(MachineRuntimeStatus.Faulted, state.Status);
        Assert.Equal(operationId, state.CurrentOperationId);
        Assert.Equal(alarmId, state.ActiveAlarmId);
        Assert.Equal("Cooling failure", state.FailureReason);
    }

    [Fact]
    public void ResolveFault_WithOperation_SetsPaused()
    {
        MachineRuntimeState state = MachineRuntimeState.CreateAvailable(
            "M-001",
            DateTimeOffset.UtcNow
        );
        Guid operationId = Guid.NewGuid();
        state.StartOperation(operationId, DateTimeOffset.UtcNow);
        state.Fault(operationId, Guid.NewGuid(), "Cooling failure", DateTimeOffset.UtcNow);

        state.ResolveFault(operationId, DateTimeOffset.UtcNow);

        Assert.Equal(MachineRuntimeStatus.Paused, state.Status);
        Assert.Equal(operationId, state.CurrentOperationId);
        Assert.Null(state.ActiveAlarmId);
    }

    [Fact]
    public void CompleteOperation_FromRunning_SetsAvailable()
    {
        MachineRuntimeState state = MachineRuntimeState.CreateAvailable(
            "M-001",
            DateTimeOffset.UtcNow
        );
        Guid operationId = Guid.NewGuid();
        state.StartOperation(operationId, DateTimeOffset.UtcNow);

        state.CompleteOperation(operationId, DateTimeOffset.UtcNow);

        Assert.Equal(MachineRuntimeStatus.Available, state.Status);
        Assert.Null(state.CurrentOperationId);
    }

    [Theory]
    [InlineData(MachineRuntimeStatus.Maintenance)]
    [InlineData(MachineRuntimeStatus.Offline)]
    public void StartOperation_WhenMachineUnavailable_Throws(MachineRuntimeStatus status)
    {
        MachineRuntimeState state = MachineRuntimeState.Restore(
            machineId: "M-001",
            status: status,
            currentOperationId: null,
            lastChangedAt: DateTimeOffset.UtcNow,
            failureReason: null,
            activeAlarmId: null,
            version: 1
        );

        Assert.Throws<BusinessRuleViolationException>(() =>
            state.StartOperation(Guid.NewGuid(), DateTimeOffset.UtcNow)
        );
    }

    [Fact]
    public void StartOperation_WhenDifferentOperationAlreadyAssigned_Throws()
    {
        MachineRuntimeState state = MachineRuntimeState.CreateAvailable(
            "M-001",
            DateTimeOffset.UtcNow
        );
        state.StartOperation(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<BusinessRuleViolationException>(() =>
            state.StartOperation(Guid.NewGuid(), DateTimeOffset.UtcNow)
        );
    }
}
