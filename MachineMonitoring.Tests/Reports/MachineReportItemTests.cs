using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Application.Reports;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Reports;

public class MachineReportItemTests
{
    [Fact]
    public void Constructor_WithMatchingDiagnostic_ShouldCreateValidItem()
    {
        // Arrange
        Machine machine = CreateMachine("M-001");

        MachineDiagnostic diagnostic = new(
            machineId: "M-001",
            message: "Machine is operating normally.",
            retrievedAt: DateTimeOffset.UtcNow
        );

        // Act
        MachineReportItem item = new(
            machine: machine,
            description: "Detailed description",
            diagnostic: diagnostic,
            diagnosticError: null
        );

        // Assert
        Assert.Same(machine, item.Machine);
        Assert.Same(diagnostic, item.Diagnostic);
        Assert.True(item.HasDiagnostic);
        Assert.Null(item.DiagnosticError);
    }

    [Fact]
    public void Constructor_WithDiagnosticForDifferentMachine_ShouldThrow()
    {
        // Arrange
        Machine machine = CreateMachine("M-001");

        MachineDiagnostic diagnostic = new(
            machineId: "M-999",
            message: "Some diagnostic",
            retrievedAt: DateTimeOffset.UtcNow
        );

        // Act
        Action action = () =>
            new MachineReportItem(
                machine: machine,
                description: "Detailed description",
                diagnostic: diagnostic,
                diagnosticError: null
            );

        // Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("diagnostic", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithDiagnosticError_ShouldCreatePartialItem()
    {
        // Arrange
        Machine machine = CreateMachine("M-003");

        // Act
        MachineReportItem item = new(
            machine: machine,
            description: "Machine is offline.",
            diagnostic: null,
            diagnosticError: "Diagnostic information is unavailable."
        );

        // Assert
        Assert.False(item.HasDiagnostic);
        Assert.Null(item.Diagnostic);
        Assert.Equal("Diagnostic information is unavailable.", item.DiagnosticError);
    }

    [Fact]
    public void Constructor_WithDiagnosticAndDiagnosticError_ShouldThrowArgumentException()
    {
        // Arrange
        Machine machine = CreateMachine("M-004");

        MachineDiagnostic diagnostic = new(
            machineId: "M-004",
            message: "Machine is operating normally.",
            retrievedAt: DateTimeOffset.UtcNow
        );

        // Act
        Action action = () =>
            new MachineReportItem(
                machine: machine,
                description: "Machine is offline.",
                diagnostic: diagnostic,
                diagnosticError: "Diagnostic information is unavailable."
            );

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_WithoutDiagnosticAndDiagnosticError_ShouldThrowArgumentException()
    {
        // Arrange
        Machine machine = CreateMachine("M-005");

        // Act
        Action action = () =>
            new MachineReportItem(
                machine: machine,
                description: "Machine is offline.",
                diagnostic: null,
                diagnosticError: null
            );

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    private static Machine CreateMachine(string id)
    {
        return new Machine(
            id: id,
            name: "Test Machine",
            status: MachineStatus.Running,
            location: "Test Area",
            serialNumber: "SN-TEST-001"
        );
    }
}
