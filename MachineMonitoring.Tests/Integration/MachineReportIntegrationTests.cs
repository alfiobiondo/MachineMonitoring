using MachineMonitoring.Application;
using MachineMonitoring.Application.Reports;
using MachineMonitoring.Domain;
using MachineMonitoring.Infrastructure;
using MachineMonitoring.Infrastructure.Configuration;
using MachineMonitoring.Tests.Fakes;
using MachineMonitoring.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests.Integration;

public class MachineReportIntegrationTests
{
    [Fact]
    public async Task CreateReportAsync_FromJsonFile_ShouldCreateExpectedReport()
    {
        // Arrange
        string json = """
            [
              {
                "id": "M-001",
                "name": "Laser Cutter",
                "status": "Running",
                "location": "Production Hall A",
                "serialNumber": "SN-001"
              },
              {
                "id": "M-002",
                "name": "Tube Bender",
                "status": "Idle",
                "location": "Production Hall B",
                "serialNumber": "SN-002"
              },
              {
                "id": "M-003",
                "name": "Quality Control Station",
                "status": "Offline",
                "location": "Testing Area",
                "serialNumber": "SN-003"
              }
            ]
            """;

        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync(json);

        MachineDataOptions machineDataOptions = new() { FilePath = file.FilePath };

        JsonMachineProvider provider = new(
            Options.Create(machineDataOptions),
            NullLogger<JsonMachineProvider>.Instance
        );

        MachineFormatter formatter = new(NullLogger<MachineFormatter>.Instance);

        FakeMachineDiagnosticService diagnosticService = new(failingMachineIds: ["M-003"]);

        MachineManager manager = new(
            machineProvider: provider,
            machineFormatter: formatter,
            machineDiagnosticService: diagnosticService,
            logger: NullLogger<MachineManager>.Instance
        );

        // Act
        MachineReport report = await manager.CreateReportAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, report.Items.Count);

        Assert.Equal(1, report.StatusSummary.GetCount(MachineStatus.Running));

        Assert.Equal(1, report.StatusSummary.GetCount(MachineStatus.Idle));

        Assert.Equal(1, report.StatusSummary.GetCount(MachineStatus.Offline));

        Assert.Equal(2, report.SuccessfulDiagnosticCount);
        Assert.Equal(1, report.FailedDiagnosticCount);

        string[] orderedIds = report.Items.Select(item => item.Machine.Id).ToArray();

        Assert.Equal(["M-003", "M-002", "M-001"], orderedIds);

        MachineReportItem failedItem = Assert.Single(report.Items, item => !item.HasDiagnostic);

        Assert.Equal("M-003", failedItem.Machine.Id);
    }
}
