using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Results;

public sealed record TubeGeometryDetailsResult(
    decimal OuterDiameterMillimeters,
    decimal ThicknessMillimeters,
    decimal LengthMillimeters,
    decimal InnerDiameterMillimeters
)
    : WorkpieceGeometryDetailsResult(
        Type: WorkpieceGeometryType.Tube,
        ThicknessMillimeters: ThicknessMillimeters
    );
