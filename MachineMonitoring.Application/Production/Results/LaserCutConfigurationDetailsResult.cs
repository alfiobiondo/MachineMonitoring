using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Results;

public sealed record LaserCutConfigurationDetailsResult(
    Guid Id,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    Guid NozzleId,
    string NozzleCode,
    Guid DrawingFileId,
    string DrawingFileName,
    WorkpieceGeometryDetailsResult Geometry,
    decimal LaserPowerWatts,
    decimal CuttingSpeedMillimetersPerMinute,
    AssistGasType AssistGas,
    decimal GasPressureBar,
    decimal FocalOffsetMillimeters,
    int NumberOfPasses,
    DateTimeOffset CreatedAt
);
