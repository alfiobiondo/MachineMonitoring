using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Application;

public interface ILimitedMachineDiagnosticService
{
    Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    );
}
