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

    // [Fact]
    // public void Constructor_WithDiagnosticAndDiagnosticError_ShouldThrowArgumentException()
    // {
    //     // Arrange
    //     Machine machine = CreateMachine("M-004");

    //     MachineDiagnostic diagnostic = new(
    //         machineId: "M-004",
    //         message: "Machine is operating normally.",
    //         retrievedAt: DateTimeOffset.UtcNow
    //     );

    //     // Act
    //     Action action = () =>
    //         new MachineReportItem(
    //             machine: machine,
    //             description: "Machine is offline.",
    //             diagnostic: diagnostic,
    //             diagnosticError: "Diagnostic information is unavailable."
    //         );

    //     // Assert
    //     Assert.Throws<ArgumentException>(action);
    // }

    // [Fact]
    // public void Constructor_WithoutDiagnosticAndDiagnosticError_ShouldThrowArgumentException()
    // {
    //     // Arrange
    //     Machine machine = CreateMachine("M-005");

    //     // Act
    //     Action action = () =>
    //         new MachineReportItem(
    //             machine: machine,
    //             description: "Machine is offline.",
    //             diagnostic: null,
    //             diagnosticError: null
    //         );

    //     // Assert
    //     Assert.Throws<ArgumentException>(action);
    // }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Constructor_WithValidDiagnosticCombination_ShouldCreateItem(
        bool includeDiagnostic,
        bool includeError
    )
    {
        // Arrange
        Machine machine = CreateMachine("M-001");

        MachineDiagnostic? diagnostic = CreateDiagnostic(machine, includeDiagnostic);

        string? diagnosticError = CreateDiagnosticError(includeError);

        // Act
        MachineReportItem item = new(
            machine: machine,
            description: "Detailed description",
            diagnostic: diagnostic,
            diagnosticError: diagnosticError
        );

        // Assert
        Assert.Equal(includeDiagnostic, item.HasDiagnostic);

        Assert.Equal(diagnostic, item.Diagnostic);

        Assert.Equal(diagnosticError, item.DiagnosticError);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Constructor_WithInvalidDiagnosticCombination_ShouldThrowArgumentException(
        bool includeDiagnostic,
        bool includeError
    )
    {
        // Arrange
        Machine machine = CreateMachine("M-001");

        MachineDiagnostic? diagnostic = CreateDiagnostic(machine, includeDiagnostic);

        string? diagnosticError = CreateDiagnosticError(includeError);

        // Act
        Action action = () =>
            new MachineReportItem(
                machine: machine,
                description: "Detailed description",
                diagnostic: diagnostic,
                diagnosticError: diagnosticError
            );

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    // [Theory]
    // [InlineData(true, false, true)]
    // [InlineData(false, true, false)]
    // public void Constructor_WithDiagnosticCombination_ShouldHaveExpectedDiagnostic(
    //     bool includeDiagnostic,
    //     bool includeError,
    //     bool expectedHasDiagnostic
    // )
    // {
    //     // Arrange
    //     Machine machine = CreateMachine("M-001");

    //     MachineDiagnostic? diagnostic = CreateDiagnostic(machine, includeDiagnostic);

    //     string? diagnosticError = CreateDiagnosticError(includeError);

    //     // Act
    //     MachineReportItem item = new(
    //         machine: machine,
    //         description: "Detailed description",
    //         diagnostic: diagnostic,
    //         diagnosticError: diagnosticError
    //     );

    //     // Assert
    //     Assert.Equal(expectedHasDiagnostic, item.HasDiagnostic);
    // }

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

    private static MachineDiagnostic? CreateDiagnostic(Machine machine, bool includeDiagnostic)
    {
        if (!includeDiagnostic)
        {
            return null;
        }

        return new MachineDiagnostic(
            machineId: machine.Id,
            message: "Test diagnostic",
            retrievedAt: DateTimeOffset.UtcNow
        );
    }

    private static string? CreateDiagnosticError(bool includeError)
    {
        return includeError ? "Diagnostic information is unavailable." : null;
    }
}
