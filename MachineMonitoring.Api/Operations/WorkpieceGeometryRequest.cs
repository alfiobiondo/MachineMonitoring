namespace MachineMonitoring.Api.Operations;

public sealed record WorkpieceGeometryRequest(
    string Type,
    decimal ThicknessMillimeters,
    decimal? OuterDiameterMillimeters,
    decimal? LengthMillimeters,
    decimal? WidthMillimeters,
    decimal? HeightMillimeters
);
