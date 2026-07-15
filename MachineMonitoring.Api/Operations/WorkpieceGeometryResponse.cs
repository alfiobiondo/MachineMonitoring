namespace MachineMonitoring.Api.Operations;

public sealed record WorkpieceGeometryResponse(
    string Type,
    decimal ThicknessMillimeters,
    decimal? OuterDiameterMillimeters,
    decimal? InnerDiameterMillimeters,
    decimal? LengthMillimeters,
    decimal? WidthMillimeters,
    decimal? HeightMillimeters
);
