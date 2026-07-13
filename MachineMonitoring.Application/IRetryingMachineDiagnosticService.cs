using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Application;

public interface IRetryingMachineDiagnosticService
{
    Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    );
}
