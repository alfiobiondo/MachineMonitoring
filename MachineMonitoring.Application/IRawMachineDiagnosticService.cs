using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Application;

public interface IRawMachineDiagnosticService
{
    Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    );
}
