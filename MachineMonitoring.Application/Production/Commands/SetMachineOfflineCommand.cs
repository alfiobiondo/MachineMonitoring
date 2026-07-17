namespace MachineMonitoring.Application.Production.Commands;

public sealed record SetMachineOfflineCommand(string MachineId, string? Reason);
