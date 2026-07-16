using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Api.Operations;

public sealed record CreateMachineOperationRequest(
    Guid WorkpieceId,
    int SequenceNumber,
    string MachineId,
    Guid MaterialId,
    Guid NozzleId,
    Guid DrawingFileId,
    WorkpieceGeometryRequest Geometry,
    decimal LaserPowerWatts,
    decimal CuttingSpeedMillimetersPerMinute,
    AssistGasType AssistGas,
    decimal GasPressureBar,
    decimal FocalOffsetMillimeters,
    int NumberOfPasses
);
