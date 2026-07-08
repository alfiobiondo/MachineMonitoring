using MachineMonitoring.Application;
using MachineMonitoring.Application.Reports;
using MachineMonitoring.Domain;
using MachineMonitoring.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace MachineMonitoring.Tests;

public class MachineManagerTests
{
    [Fact]
    public async Task CreateReportAsync_WithMachines_ShouldCreateReport()
    {
        // Arrange
        Machine[] machines =
        [
            CreateMachine(id: "M-001", status: MachineStatus.Running),
            CreateMachine(id: "M-002", status: MachineStatus.Idle),
        ];

        MachineManager manager = CreateManager(machines);

        // Act
        MachineReport report = await manager.CreateReportAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, report.Items.Count);
        Assert.Equal(2, report.StatusSummary.TotalCount);
        Assert.Equal(2, report.SuccessfulDiagnosticCount);
        Assert.Equal(0, report.FailedDiagnosticCount);
    }

    [Fact]
    public async Task CreateReportAsync_WithDifferentStatuses_ShouldCalculateSummary()
    {
        // Arrange
        Machine[] machines =
        [
            CreateMachine(id: "M-001", status: MachineStatus.Running),
            CreateMachine(id: "M-002", status: MachineStatus.Running),
            CreateMachine(id: "M-003", status: MachineStatus.Offline),
            CreateMachine(id: "M-004", status: MachineStatus.Maintenance),
        ];

        MachineManager manager = CreateManager(machines);

        // Act
        MachineReport report = await manager.CreateReportAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, report.StatusSummary.GetCount(MachineStatus.Running));

        Assert.Equal(1, report.StatusSummary.GetCount(MachineStatus.Offline));

        Assert.Equal(1, report.StatusSummary.GetCount(MachineStatus.Maintenance));

        Assert.Equal(0, report.StatusSummary.GetCount(MachineStatus.Alarm));
    }

    [Fact]
    public async Task CreateReportAsync_ShouldOrderMachinesByStatusPriority()
    {
        // Arrange
        Machine[] machines =
        [
            CreateMachine(id: "M-001", status: MachineStatus.Running),
            CreateMachine(id: "M-002", status: MachineStatus.Idle),
            CreateMachine(id: "M-003", status: MachineStatus.Offline),
            CreateMachine(id: "M-004", status: MachineStatus.Alarm),
            CreateMachine(id: "M-005", status: MachineStatus.Maintenance),
        ];

        MachineManager manager = CreateManager(machines);

        // Act
        MachineReport report = await manager.CreateReportAsync(CancellationToken.None);

        // Assert
        string[] orderedIds = report.Items.Select(item => item.Machine.Id).ToArray();

        Assert.Equal(["M-004", "M-003", "M-005", "M-002", "M-001"], orderedIds);
    }

    [Fact]
    public async Task CreateReportAsync_WithSameStatus_ShouldOrderMachinesById()
    {
        // Arrange
        Machine[] machines =
        [
            CreateMachine(id: "M-003", status: MachineStatus.Running),
            CreateMachine(id: "M-001", status: MachineStatus.Running),
            CreateMachine(id: "M-002", status: MachineStatus.Running),
        ];

        MachineManager manager = CreateManager(machines);

        // Act
        MachineReport report = await manager.CreateReportAsync(CancellationToken.None);

        // Assert
        string[] orderedIds = report.Items.Select(item => item.Machine.Id).ToArray();

        Assert.Equal(["M-001", "M-002", "M-003"], orderedIds);
    }

    [Fact]
    public async Task CreateReportAsync_WhenOneDiagnosticFails_ShouldCreatePartialReport()
    {
        // Arrange
        Machine[] machines =
        [
            CreateMachine(id: "M-001", status: MachineStatus.Running),
            CreateMachine(id: "M-003", status: MachineStatus.Offline),
        ];

        MachineManager manager = CreateManager(machines, failingMachineIds: ["M-003"]);

        // Act
        MachineReport report = await manager.CreateReportAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, report.Items.Count);
        Assert.Equal(1, report.SuccessfulDiagnosticCount);
        Assert.Equal(1, report.FailedDiagnosticCount);

        MachineReportItem failedItem = Assert.Single(report.Items, item => !item.HasDiagnostic);

        Assert.Equal("M-003", failedItem.Machine.Id);

        Assert.Null(failedItem.Diagnostic);

        Assert.Equal("Diagnostic information is unavailable.", failedItem.DiagnosticError);
    }

    [Fact]
    public async Task CreateReportAsync_WhenTwoDiagnosticFails_ShouldCreatePartialReport()
    {
        // Arrange
        Machine[] machines =
        [
            CreateMachine(id: "M-001", status: MachineStatus.Running),
            CreateMachine(id: "M-002", status: MachineStatus.Idle),
            CreateMachine(id: "M-003", status: MachineStatus.Offline),
            CreateMachine(id: "M-004", status: MachineStatus.Maintenance),
        ];

        MachineManager manager = CreateManager(machines, failingMachineIds: ["M-002", "M-003"]);

        // Act
        MachineReport report = await manager.CreateReportAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, report.SuccessfulDiagnosticCount);
        Assert.Equal(2, report.FailedDiagnosticCount);
        Assert.Equal(4, report.Items.Count);

        string[] failedIds = report
            .Items.Where(item => !item.HasDiagnostic)
            .Select(item => item.Machine.Id)
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal(["M-002", "M-003"], failedIds);
    }

    [Fact]
    public async Task CreateReportAsync_WhenDiagnosticSucceeds_ShouldAssociateItWithMachine()
    {
        // Arrange
        Machine[] machines = [CreateMachine(id: "M-010", status: MachineStatus.Running)];

        MachineManager manager = CreateManager(machines);

        // Act
        MachineReport report = await manager.CreateReportAsync(CancellationToken.None);

        // Assert
        MachineReportItem item = Assert.Single(report.Items);

        Assert.NotNull(item.Diagnostic);

        Assert.Equal(item.Machine.Id, item.Diagnostic.MachineId);

        Assert.Equal("Diagnostic for M-010", item.Diagnostic.Message);
    }

    [Fact]
    public async Task CreateReportAsync_WhenAlreadyCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        Machine[] machines = [CreateMachine(id: "M-001", status: MachineStatus.Running)];

        MachineManager manager = CreateManager(machines);

        using CancellationTokenSource source = new();

        source.Cancel();

        // Act
        Task<MachineReport> action = manager.CreateReportAsync(source.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action);
    }

    private static Machine CreateMachine(string id, MachineStatus status)
    {
        return new Machine(
            id: id,
            name: $"Machine {id}",
            status: status,
            location: "Test Area",
            serialNumber: $"SN-{id}"
        );
    }

    private static MachineManager CreateManager(
        IReadOnlyCollection<Machine> machines,
        IEnumerable<string>? failingMachineIds = null
    )
    {
        FakeMachineProvider provider = new(machines);

        FakeMachineDiagnosticService diagnosticService = new(failingMachineIds);

        MachineFormatter formatter = new(NullLogger<MachineFormatter>.Instance);

        return new MachineManager(
            machineProvider: provider,
            machineFormatter: formatter,
            machineDiagnosticService: diagnosticService,
            logger: NullLogger<MachineManager>.Instance
        );
    }
}
