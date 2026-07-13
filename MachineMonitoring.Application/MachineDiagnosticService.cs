using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application;

public class MachineDiagnosticService : IRawMachineDiagnosticService
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

        _logger.LogDebug("Diagnostic retrieval started for machine {MachineId}.", machine.Id);

        int delayMilliseconds = machine.Id switch
        {
            "M-001" => 1200,
            "M-002" => 300,
            "M-003" => 900,
            "M-004" => 600,
            "M-005" => 500,
            _ => 700,
        };

        await Task.Delay(delayMilliseconds, cancellationToken);

        string message = machine.Status switch
        {
            MachineStatus.Alarm => "Immediate operator intervention required.",

            MachineStatus.Offline => "Machine communication is unavailable.",

            MachineStatus.Maintenance => "Scheduled maintenance is in progress.",

            MachineStatus.Idle => "Machine is available but not producing.",

            MachineStatus.Running => "Machine is operating normally.",

            _ => "Machine status is unknown.",
        };

        _logger.LogDebug("Diagnostic retrieval completed for machine {MachineId}.", machine.Id);

        return new MachineDiagnostic(
            machineId: machine.Id,
            message: message,
            retrievedAt: DateTimeOffset.Now
        );
    }
}
