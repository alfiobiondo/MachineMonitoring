using Microsoft.AspNetCore.SignalR;

namespace MachineMonitoring.Api.Hubs;

public sealed class MachineMonitoringHub : Hub
{
    public Task JoinMachineAsync(string machineId)
    {
        string groupName = CreateMachineGroupName(machineId);

        return Groups.AddToGroupAsync(Context.ConnectionId, groupName, Context.ConnectionAborted);
    }

    public Task LeaveMachineAsync(string machineId)
    {
        string groupName = CreateMachineGroupName(machineId);

        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            groupName,
            Context.ConnectionAborted
        );
    }

    public static string CreateMachineGroupName(string machineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        return $"machine:{machineId.Trim()}";
    }
}
