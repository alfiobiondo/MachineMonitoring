using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Results;

public abstract record WorkpieceGeometryDetailsResult(
    WorkpieceGeometryType Type,
    decimal ThicknessMillimeters
);
