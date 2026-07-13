using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Commands;

public sealed record CreateLaserCutOperationCommand(
    Guid WorkpieceId,
    string MachineId,
    Guid MaterialId,
    Guid NozzleId,
    Guid DrawingFileId,
    WorkpieceGeometryInput Geometry,
    decimal LaserPowerWatts,
    decimal CuttingSpeedMillimetersPerMinute,
    AssistGasType AssistGas,
    decimal GasPressureBar,
    decimal FocalOffsetMillimeters,
    int NumberOfPasses
);
