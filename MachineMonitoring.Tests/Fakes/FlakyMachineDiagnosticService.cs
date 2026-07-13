using MachineMonitoring.Application;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Fakes;

public sealed class FlakyMachineDiagnosticService : ILimitedMachineDiagnosticService
{
    private readonly int _failuresBeforeSuccess;
    private int _attemptCount;

    public int AttemptCount => _attemptCount;

    public FlakyMachineDiagnosticService(int failuresBeforeSuccess)
    {
        if (failuresBeforeSuccess < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failuresBeforeSuccess));
        }

        _failuresBeforeSuccess = failuresBeforeSuccess;
    }

    public Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(machine);

        cancellationToken.ThrowIfCancellationRequested();

        int currentAttempt = Interlocked.Increment(ref _attemptCount);

        if (currentAttempt <= _failuresBeforeSuccess)
        {
            throw new TransientMachineDiagnosticException(
                machineId: machine.Id,
                message: "Simulated transient failure."
            );
        }

        MachineDiagnostic diagnostic = new(
            machineId: machine.Id,
            message: "Diagnostic retrieved successfully.",
            retrievedAt: DateTimeOffset.UtcNow
        );

        return Task.FromResult(diagnostic);
    }
}
