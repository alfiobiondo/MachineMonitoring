namespace MachineMonitoring.Application.Diagnostics;

public class MachineDiagnostic
{
    public string MachineId { get; }

    public string Message { get; }

    public DateTimeOffset RetrievedAt { get; }

    public MachineDiagnostic(string machineId, string message, DateTimeOffset retrievedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        MachineId = machineId;
        Message = message;
        RetrievedAt = retrievedAt;
    }
}
