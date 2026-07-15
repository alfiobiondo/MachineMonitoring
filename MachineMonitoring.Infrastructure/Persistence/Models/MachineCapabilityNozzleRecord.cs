namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class MachineCapabilityNozzleRecord
{
    public string MachineId { get; set; } = string.Empty;

    public Guid NozzleId { get; set; }

    public MachineCapabilitiesRecord MachineCapabilities { get; set; } = null!;
}
