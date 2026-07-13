using MachineMonitoring.Application;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Fakes;

public sealed class CountingRetryingMachineDiagnosticService : IRetryingMachineDiagnosticService
{
    private int _callCount;

    public int CallCount => _callCount;

    public Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(machine);

        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _callCount);

        MachineDiagnostic diagnostic = new(
            machineId: machine.Id,
            message: $"Diagnostic for {machine.Id}",
            retrievedAt: DateTimeOffset.UtcNow
        );

        return Task.FromResult(diagnostic);
    }
}
