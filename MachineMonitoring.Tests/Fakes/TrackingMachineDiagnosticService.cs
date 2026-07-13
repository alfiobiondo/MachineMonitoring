using MachineMonitoring.Application;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Tests.Fakes;

public sealed class TrackingMachineDiagnosticService : IRawMachineDiagnosticService
{
    private int _currentConcurrency;
    private int _maximumObservedConcurrency;

    public int MaximumObservedConcurrency => _maximumObservedConcurrency;

    public async Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        int current = Interlocked.Increment(ref _currentConcurrency);

        UpdateMaximum(current);

        try
        {
            await Task.Delay(millisecondsDelay: 100, cancellationToken);

            return new MachineDiagnostic(
                machineId: machine.Id,
                message: $"Diagnostic for {machine.Id}",
                retrievedAt: DateTimeOffset.UtcNow
            );
        }
        finally
        {
            Interlocked.Decrement(ref _currentConcurrency);
        }
    }

    private void UpdateMaximum(int current)
    {
        while (true)
        {
            int observed = _maximumObservedConcurrency;

            if (current <= observed)
            {
                return;
            }

            int original = Interlocked.CompareExchange(
                ref _maximumObservedConcurrency,
                current,
                observed
            );

            if (original == observed)
            {
                return;
            }
        }
    }
}
