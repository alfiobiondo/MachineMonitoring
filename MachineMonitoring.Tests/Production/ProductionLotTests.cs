using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Tests.Production;

public sealed class ProductionLotTests
{
    [Fact]
    public void NewProductionLot_DefaultsExecutionModeToNone()
    {
        ProductionLot lot = CreateLot();

        Assert.Equal(ProductionLotExecutionMode.None, lot.ExecutionMode);
    }

    [Fact]
    public void StartLotSequence_SetsExecutionModeToLotSequence()
    {
        ProductionLot lot = CreateLot();

        lot.StartLotSequence(DateTimeOffset.UtcNow);

        Assert.Equal(ProductionLotStatus.Running, lot.Status);
        Assert.Equal(ProductionLotExecutionMode.LotSequence, lot.ExecutionMode);
    }

    [Fact]
    public void StartManual_KeepsExecutionModeNone()
    {
        ProductionLot lot = CreateLot();

        lot.StartManual(DateTimeOffset.UtcNow);

        Assert.Equal(ProductionLotStatus.Running, lot.Status);
        Assert.Equal(ProductionLotExecutionMode.None, lot.ExecutionMode);
    }

    [Fact]
    public void Restore_PreservesExecutionMode()
    {
        ProductionLot lot = ProductionLot.Restore(
            id: Guid.NewGuid(),
            code: "LOT-001",
            plannedQuantity: 1,
            status: ProductionLotStatus.Running,
            executionMode: ProductionLotExecutionMode.LotSequence,
            createdAt: DateTimeOffset.UtcNow,
            startedAt: DateTimeOffset.UtcNow,
            completedAt: null
        );

        Assert.Equal(ProductionLotExecutionMode.LotSequence, lot.ExecutionMode);
    }

    [Fact]
    public void Complete_ClearsExecutionMode()
    {
        ProductionLot lot = CreateStartedLotSequence();

        lot.Complete(DateTimeOffset.UtcNow);

        Assert.Equal(ProductionLotExecutionMode.None, lot.ExecutionMode);
    }

    [Fact]
    public void Fail_ClearsExecutionMode()
    {
        ProductionLot lot = CreateStartedLotSequence();

        lot.Fail(DateTimeOffset.UtcNow);

        Assert.Equal(ProductionLotExecutionMode.None, lot.ExecutionMode);
    }

    [Fact]
    public void Cancel_ClearsExecutionMode()
    {
        ProductionLot lot = CreateStartedLotSequence();

        lot.Cancel();

        Assert.Equal(ProductionLotExecutionMode.None, lot.ExecutionMode);
    }

    private static ProductionLot CreateStartedLotSequence()
    {
        ProductionLot lot = CreateLot();
        lot.StartLotSequence(DateTimeOffset.UtcNow);
        return lot;
    }

    private static ProductionLot CreateLot()
    {
        return new ProductionLot(
            id: Guid.NewGuid(),
            code: $"LOT-{Guid.NewGuid():N}",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );
    }
}
