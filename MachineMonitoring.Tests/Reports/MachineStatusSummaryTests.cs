using MachineMonitoring.Application.Reports;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Reports;

public class MachineStatusSummaryTests
{
    [Fact]
    public void Constructor_WithValidCounts_ShouldCalculateTotalCount()
    {
        // Arrange
        Dictionary<MachineStatus, int> counts = new()
        {
            [MachineStatus.Running] = 2,
            [MachineStatus.Idle] = 1,
            [MachineStatus.Alarm] = 1,
        };

        // Act
        MachineStatusSummary summary = new(counts);

        // Assert
        Assert.Equal(4, summary.TotalCount);
    }

    [Theory]
    [InlineData(MachineStatus.Running, 3)]
    [InlineData(MachineStatus.Offline, 1)]
    [InlineData(MachineStatus.Idle, 0)]
    [InlineData(MachineStatus.Alarm, 0)]
    [InlineData(MachineStatus.Maintenance, 0)]
    public void GetCount_WithDifferentStatuses_ShouldReturnExpectedCount(
        MachineStatus status,
        int expectedCount
    )
    {
        // Arrange
        Dictionary<MachineStatus, int> counts = new()
        {
            [MachineStatus.Running] = 3,
            [MachineStatus.Offline] = 1,
        };

        MachineStatusSummary summary = new(counts);

        // Act
        int actualCount = summary.GetCount(status);

        // Assert
        Assert.Equal(expectedCount, actualCount);
    }

    [Fact]
    public void Constructor_WithNegativeCount_ShouldThrowArgumentException()
    {
        // Arrange
        Dictionary<MachineStatus, int> counts = new() { [MachineStatus.Running] = -1 };

        // Act
        Action action = () => new MachineStatusSummary(counts);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_WithNegativeCount_ShouldIdentifyCountsParameter()
    {
        // Arrange
        Dictionary<MachineStatus, int> counts = new() { [MachineStatus.Running] = -1 };

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new MachineStatusSummary(counts)
        );

        // Assert
        Assert.Equal("counts", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(TotalCountData))]
    public void Constructor_WithDifferentCounts_ShouldCalculateTotal(
        IReadOnlyDictionary<MachineStatus, int> counts,
        int expectedTotal
    )
    {
        // Act
        MachineStatusSummary summary = new(counts);

        // Assert
        Assert.Equal(expectedTotal, summary.TotalCount);
    }

    public static TheoryData<IReadOnlyDictionary<MachineStatus, int>, int> TotalCountData =>
        new()
        {
            {
                new Dictionary<MachineStatus, int>
                {
                    [MachineStatus.Running] = 2,
                    [MachineStatus.Idle] = 1,
                },
                3
            },
            {
                new Dictionary<MachineStatus, int>
                {
                    [MachineStatus.Alarm] = 4,
                    [MachineStatus.Maintenance] = 2,
                },
                6
            },
            { new Dictionary<MachineStatus, int>(), 0 },
        };
}
