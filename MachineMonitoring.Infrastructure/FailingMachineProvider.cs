using MachineMonitoring.Application;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Infrastructure;

public class FailingMachineProvider : IMachineProvider
{
    public Task<Machine> GetMachineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IOException technicalException = new("Simulated communication failure.");

        throw new MachineUnavailableException(
            "The configured machine could not be retrieved.",
            technicalException
        );
    }
}
