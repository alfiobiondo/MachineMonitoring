using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Scenarios;

public class MachineReportScenario
{
    public required string Name { get; init; }

    public required Machine[] Machines { get; init; }

    public string[] FailingMachineIds { get; init; } = [];

    public required int ExpectedSuccessfulDiagnostics { get; init; }

    public required int ExpectedFailedDiagnostics { get; init; }

    public override string ToString()
    {
        return Name;
    }
}
