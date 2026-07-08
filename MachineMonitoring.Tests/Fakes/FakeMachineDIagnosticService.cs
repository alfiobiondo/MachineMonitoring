using MachineMonitoring.Application;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Fakes;

public class FakeMachineDiagnosticService : IMachineDiagnosticService
{
    private readonly HashSet<string> _failingMachineIds;

    public FakeMachineDiagnosticService(IEnumerable<string>? failingMachineIds = null)
    {
        _failingMachineIds = failingMachineIds is null
            ? new HashSet<string>()
            : new HashSet<string>(failingMachineIds);
    }

    public Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(machine);

        cancellationToken.ThrowIfCancellationRequested();

        if (_failingMachineIds.Contains(machine.Id))
        {
            throw new MachineDiagnosticUnavailableException(
                machineId: machine.Id,
                message: $"Diagnostic information for machine {machine.Id} is unavailable."
            );
        }

        MachineDiagnostic diagnostic = new(
            machineId: machine.Id,
            message: $"Diagnostic for {machine.Id}",
            retrievedAt: DateTimeOffset.UtcNow
        );

        return Task.FromResult(diagnostic);
    }
}
