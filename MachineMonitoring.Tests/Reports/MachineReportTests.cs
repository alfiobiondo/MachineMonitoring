using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Application.Reports;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Reports;

public class MachineReportTests
{
    [Fact]
    public void Constructor_WithMatchingSummary_ShouldCreateReport()
    {
        // Arrange
        MachineReportItem[] items = [CreateSuccessfulItem("M-001"), CreateFailedItem("M-002")];

        Dictionary<MachineStatus, int> counts = new() { [MachineStatus.Running] = 2 };

        MachineStatusSummary summary = new(counts);

        // Act
        MachineReport report = new(
            generatedAt: DateTimeOffset.UtcNow,
            items: items,
            statusSummary: summary
        );

        // Assert
        Assert.Equal(2, report.Items.Count);
        Assert.Equal(1, report.SuccessfulDiagnosticCount);
        Assert.Equal(1, report.FailedDiagnosticCount);
    }

    [Fact]
    public void Constructor_WithSummaryCountDifferentFromItems_ShouldThrowArgumentException()
    {
        // Arrange
        MachineReportItem[] items = [CreateSuccessfulItem("M-001")];

        Dictionary<MachineStatus, int> counts = new() { [MachineStatus.Running] = 2 };

        MachineStatusSummary summary = new(counts);

        // Act
        Action action = () =>
            new MachineReport(
                generatedAt: DateTimeOffset.UtcNow,
                items: items,
                statusSummary: summary
            );

        // Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("statusSummary", exception.ParamName);
    }

    private static MachineReportItem CreateSuccessfulItem(string machineId)
    {
        Machine machine = CreateMachine(machineId);

        MachineDiagnostic diagnostic = new(
            machineId: machineId,
            message: $"Diagnostic for {machineId}",
            retrievedAt: DateTimeOffset.UtcNow
        );

        return new MachineReportItem(
            machine: machine,
            description: $"Description for {machineId}",
            diagnostic: diagnostic,
            diagnosticError: null
        );
    }

    private static MachineReportItem CreateFailedItem(string machineId)
    {
        Machine machine = CreateMachine(machineId);

        return new MachineReportItem(
            machine: machine,
            description: $"Description for {machineId}",
            diagnostic: null,
            diagnosticError: "Diagnostic information is unavailable."
        );
    }

    private static Machine CreateMachine(string id)
    {
        return new Machine(
            id: id,
            name: $"Machine {id}",
            status: MachineStatus.Running,
            location: "Test Area",
            serialNumber: $"SN-{id}"
        );
    }
}
