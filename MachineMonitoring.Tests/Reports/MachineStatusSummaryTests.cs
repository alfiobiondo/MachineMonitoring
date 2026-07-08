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

    [Fact]
    public void GetCount_WhenStatusExists_ShouldReturnItsCount()
    {
        // Arrange
        Dictionary<MachineStatus, int> counts = new()
        {
            [MachineStatus.Running] = 3,
            [MachineStatus.Offline] = 1,
        };

        MachineStatusSummary summary = new(counts);

        // Act
        int runningCount = summary.GetCount(MachineStatus.Running);

        // Assert
        Assert.Equal(3, runningCount);
    }

    [Fact]
    public void GetCount_WhenStatusIsMissing_ShouldReturnZero()
    {
        // Arrange
        Dictionary<MachineStatus, int> counts = new() { [MachineStatus.Running] = 2 };

        MachineStatusSummary summary = new(counts);

        // Act
        int alarmCount = summary.GetCount(MachineStatus.Alarm);

        // Assert
        Assert.Equal(0, alarmCount);
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
}
