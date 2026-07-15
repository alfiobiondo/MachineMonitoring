namespace MachineMonitoring.Api.Operations;

public sealed record LaserCutConfigurationResponse(
    Guid Id,
    CatalogItemResponse Material,
    CatalogItemResponse Nozzle,
    DrawingFileSummaryResponse DrawingFile,
    WorkpieceGeometryResponse Geometry,
    decimal LaserPowerWatts,
    decimal CuttingSpeedMillimetersPerMinute,
    string AssistGas,
    decimal GasPressureBar,
    decimal FocalOffsetMillimeters,
    int NumberOfPasses,
    DateTimeOffset CreatedAt
);
