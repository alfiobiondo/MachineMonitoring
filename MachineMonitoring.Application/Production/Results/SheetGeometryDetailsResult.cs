using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Results;

public sealed record SheetGeometryDetailsResult(
    decimal WidthMillimeters,
    decimal HeightMillimeters,
    decimal ThicknessMillimeters
)
    : WorkpieceGeometryDetailsResult(
        Type: WorkpieceGeometryType.Sheet,
        ThicknessMillimeters: ThicknessMillimeters
    );
