using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application;

public class MachineDiagnosticService : IMachineDiagnosticService
{
    private readonly ILogger<MachineDiagnosticService> _logger;

    public MachineDiagnosticService(ILogger<MachineDiagnosticService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public async Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(machine);

        _logger.LogDebug("Retrieving diagnostic information for machine {MachineId}.", machine.Id);

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        string message = machine.Status switch
        {
            MachineStatus.Alarm => "Immediate operator intervention required.",

            MachineStatus.Offline => "Machine communication is unavailable.",

            MachineStatus.Maintenance => "Scheduled maintenance is in progress.",

            MachineStatus.Idle => "Machine is available but not producing.",

            MachineStatus.Running => "Machine is operating normally.",

            _ => "Machine status is unknown.",
        };

        return new MachineDiagnostic(
            machineId: machine.Id,
            message: message,
            retrievedAt: DateTimeOffset.Now
        );
    }
}
