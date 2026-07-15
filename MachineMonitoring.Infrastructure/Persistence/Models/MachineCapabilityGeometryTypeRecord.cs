using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class MachineCapabilityGeometryTypeRecord
{
    public string MachineId { get; set; } = string.Empty;

    public WorkpieceGeometryType GeometryType { get; set; }

    public MachineCapabilitiesRecord MachineCapabilities { get; set; } = null!;
}
