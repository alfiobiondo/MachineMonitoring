using MachineMonitoring.Domain;
using MachineMonitoring.Tests.Scenarios;

namespace MachineMonitoring.Tests.TestData;

public static class MachineReportTestData
{
    public static TheoryData<MachineReportScenario> Scenarios =>
        new()
        {
            new MachineReportScenario
            {
                Name = "All diagnostics succeed",
                Machines =
                [
                    CreateMachine(id: "M-001", status: MachineStatus.Running),
                    CreateMachine(id: "M-002", status: MachineStatus.Idle),
                ],
                ExpectedSuccessfulDiagnostics = 2,
                ExpectedFailedDiagnostics = 0,
            },
            new MachineReportScenario
            {
                Name = "One diagnostic fails",
                Machines =
                [
                    CreateMachine(id: "M-001", status: MachineStatus.Running),
                    CreateMachine(id: "M-003", status: MachineStatus.Offline),
                ],
                FailingMachineIds = ["M-003"],
                ExpectedSuccessfulDiagnostics = 1,
                ExpectedFailedDiagnostics = 1,
            },
        };

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
}
