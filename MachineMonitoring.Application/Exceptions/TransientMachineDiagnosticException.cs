namespace MachineMonitoring.Application.Exceptions;

public class TransientMachineDiagnosticException : Exception
{
    public string MachineId { get; }

    public TransientMachineDiagnosticException(
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
