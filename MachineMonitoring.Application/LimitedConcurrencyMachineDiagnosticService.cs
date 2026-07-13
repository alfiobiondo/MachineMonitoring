using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application;

public sealed class LimitedConcurrencyMachineDiagnosticService
    : ILimitedMachineDiagnosticService,
        IDisposable
{
    private readonly IRawMachineDiagnosticService _innerService;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<LimitedConcurrencyMachineDiagnosticService> _logger;

    public LimitedConcurrencyMachineDiagnosticService(
        IRawMachineDiagnosticService innerService,
        IOptions<DiagnosticOptions> options,
        ILogger<LimitedConcurrencyMachineDiagnosticService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(innerService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        int maxConcurrency = options.Value.MaxConcurrency;

        _innerService = innerService;
        _semaphore = new SemaphoreSlim(initialCount: maxConcurrency, maxCount: maxConcurrency);
        _logger = logger;
    }

    public async Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(machine);

        _logger.LogDebug("Machine {MachineId} is waiting for a diagnostic slot.", machine.Id);

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogDebug(
                "Machine {MachineId} acquired a diagnostic slot. "
                    + "{AvailableSlots} slots remain available.",
                machine.Id,
                _semaphore.CurrentCount
            );

            return await _innerService.GetDiagnosticAsync(machine, cancellationToken);
        }
        finally
        {
            _semaphore.Release();

            _logger.LogDebug(
                "Machine {MachineId} released its diagnostic slot. "
                    + "{AvailableSlots} slots are now available.",
                machine.Id,
                _semaphore.CurrentCount
            );
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
