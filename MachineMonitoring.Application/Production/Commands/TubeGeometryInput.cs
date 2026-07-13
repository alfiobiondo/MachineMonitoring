namespace MachineMonitoring.Application.Production.Commands;

public sealed record TubeGeometryInput(
    decimal OuterDiameterMillimeters,
    decimal ThicknessMillimeters,
    decimal LengthMillimeters
) : WorkpieceGeometryInput;
