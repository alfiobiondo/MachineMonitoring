namespace MachineMonitoring.Api.Catalogs;

public sealed record MachineCapabilitiesResponse(
    string MachineId,
    decimal MaximumLaserPowerWatts,
    decimal MinimumThicknessMillimeters,
    decimal MaximumThicknessMillimeters,
    IReadOnlyCollection<string> SupportedMaterialCategories,
    IReadOnlyCollection<Guid> SupportedNozzleIds,
    IReadOnlyCollection<string> SupportedGeometryTypes,
    decimal? MaximumTubeDiameterMillimeters,
    decimal? MaximumTubeLengthMillimeters,
    decimal? MaximumSheetWidthMillimeters,
    decimal? MaximumSheetHeightMillimeters
);
