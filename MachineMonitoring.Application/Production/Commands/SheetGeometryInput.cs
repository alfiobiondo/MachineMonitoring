namespace MachineMonitoring.Application.Production.Commands;

public sealed record SheetGeometryInput(
    decimal WidthMillimeters,
    decimal HeightMillimeters,
    decimal ThicknessMillimeters
) : WorkpieceGeometryInput;
