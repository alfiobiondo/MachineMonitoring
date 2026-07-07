namespace MachineMonitoring.Application.Exceptions;

public class MachineDiagnosticUnavailableException : Exception
{
    public string MachineId { get; }

    public MachineDiagnosticUnavailableException(
        string machineId,
        string message,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        MachineId = machineId;
    }
}
