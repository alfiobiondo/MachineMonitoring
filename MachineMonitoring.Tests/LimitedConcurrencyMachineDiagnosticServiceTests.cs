using MachineMonitoring.Application;
using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Domain;
using MachineMonitoring.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests;

public class LimitedConcurrencyMachineDiagnosticServiceTests
{
    [Fact]
    public async Task GetDiagnosticAsync_WithManyRequests_ShouldRespectMaximumConcurrency()
    {
        // Arrange
        const int maximumConcurrency = 3;

        TrackingMachineDiagnosticService innerService = new();

        DiagnosticOptions diagnosticOptions = new() { MaxConcurrency = maximumConcurrency };

        using LimitedConcurrencyMachineDiagnosticService service = new(
            innerService: innerService,
            options: Options.Create(diagnosticOptions),
            logger: NullLogger<LimitedConcurrencyMachineDiagnosticService>.Instance
        );

        Machine[] machines = Enumerable
            .Range(1, 10)
            .Select(index => CreateMachine($"M-{index:000}"))
            .ToArray();

        // Act
        Task[] tasks = machines
            .Select(machine => service.GetDiagnosticAsync(machine, CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(maximumConcurrency, innerService.MaximumObservedConcurrency);
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
