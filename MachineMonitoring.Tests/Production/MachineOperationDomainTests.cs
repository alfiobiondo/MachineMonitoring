using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Tests.Production;

public sealed class MachineOperationDomainTests
{
    [Fact]
    public void Constructor_WhenSequenceNumberIsZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MachineOperation(
                id: Guid.NewGuid(),
                workpieceId: Guid.NewGuid(),
                sequenceNumber: 0,
                machineId: "M-001",
                type: MachineOperationType.LaserCutting,
                createdAt: DateTimeOffset.UtcNow
            )
        );
    }
}
