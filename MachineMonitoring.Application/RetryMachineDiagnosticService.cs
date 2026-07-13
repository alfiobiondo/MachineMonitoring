using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application;

public sealed class RetryingMachineDiagnosticService : IRetryingMachineDiagnosticService
{
    private readonly ILimitedMachineDiagnosticService _innerService;
    private readonly DiagnosticRetryOptions _options;
    private readonly ILogger<RetryingMachineDiagnosticService> _logger;

    public RetryingMachineDiagnosticService(
        ILimitedMachineDiagnosticService innerService,
        IOptions<DiagnosticRetryOptions> options,
        ILogger<RetryingMachineDiagnosticService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(innerService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _innerService = innerService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(machine);

        int maximumAttempts = _options.MaxRetryAttempts + 1;

        for (int attemptNumber = 1; attemptNumber <= maximumAttempts; attemptNumber++)
        {
            try
            {
                _logger.LogDebug(
                    "Retrieving diagnostic for machine {MachineId}. "
                        + "Attempt {AttemptNumber} of {MaximumAttempts}.",
                    machine.Id,
                    attemptNumber,
                    maximumAttempts
                );

                return await _innerService.GetDiagnosticAsync(machine, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TransientMachineDiagnosticException exception)
                when (attemptNumber < maximumAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Transient diagnostic failure for machine {MachineId}. "
                        + "Attempt {AttemptNumber} of {MaximumAttempts} failed. "
                        + "Retrying after {RetryDelayMilliseconds} ms.",
                    machine.Id,
                    attemptNumber,
                    maximumAttempts,
                    _options.DelayMilliseconds
                );

                await Task.Delay(_options.DelayMilliseconds, cancellationToken);
            }
            catch (TransientMachineDiagnosticException exception)
            {
                throw new MachineDiagnosticUnavailableException(
                    machineId: machine.Id,
                    message: $"Diagnostic information for machine {machine.Id} "
                        + $"is unavailable after {maximumAttempts} attempts.",
                    innerException: exception
                );
            }
        }

        throw new InvalidOperationException("The diagnostic retry loop completed unexpectedly.");
    }
}
