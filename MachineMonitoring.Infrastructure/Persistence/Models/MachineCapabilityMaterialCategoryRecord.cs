using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class MachineCapabilityMaterialCategoryRecord
{
    public string MachineId { get; set; } = string.Empty;

    public MaterialCategory MaterialCategory { get; set; }

    public MachineCapabilitiesRecord MachineCapabilities { get; set; } = null!;
}
