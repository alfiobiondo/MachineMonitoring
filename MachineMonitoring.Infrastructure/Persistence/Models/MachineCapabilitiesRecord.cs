using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class MachineCapabilitiesRecord
{
    public string MachineId { get; set; } = string.Empty;

    public decimal MaximumLaserPowerWatts { get; set; }

    public decimal MinimumThicknessMillimeters { get; set; }

    public decimal MaximumThicknessMillimeters { get; set; }

    public decimal? MaximumTubeDiameterMillimeters { get; set; }

    public decimal? MaximumTubeLengthMillimeters { get; set; }

    public decimal? MaximumSheetWidthMillimeters { get; set; }

    public decimal? MaximumSheetHeightMillimeters { get; set; }

    public ICollection<MachineCapabilityMaterialCategoryRecord> SupportedMaterialCategories { get; set; } =
        new List<MachineCapabilityMaterialCategoryRecord>();

    public ICollection<MachineCapabilityNozzleRecord> SupportedNozzles { get; set; } =
        new List<MachineCapabilityNozzleRecord>();

    public ICollection<MachineCapabilityGeometryTypeRecord> SupportedGeometryTypes { get; set; } =
        new List<MachineCapabilityGeometryTypeRecord>();
}
