namespace MachineMonitoring.Application.Production.Commands;

public sealed record StartMachineMaintenanceCommand(string MachineId, string? Reason);
