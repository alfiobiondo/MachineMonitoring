namespace MachineMonitoring.Domain.Technology;

public sealed class SheetGeometry : IWorkpieceGeometry
{
    public WorkpieceGeometryType Type => WorkpieceGeometryType.Sheet;

    public decimal WidthMillimeters { get; }

    public decimal HeightMillimeters { get; }

    public decimal ThicknessMillimeters { get; }

    public SheetGeometry(
        decimal widthMillimeters,
        decimal heightMillimeters,
        decimal thicknessMillimeters
    )
    {
        if (widthMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(widthMillimeters),
                "The sheet width must be greater than zero."
            );
        }

        if (heightMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightMillimeters),
                "The sheet height must be greater than zero."
            );
        }

        if (thicknessMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thicknessMillimeters),
                "The sheet thickness must be greater than zero."
            );
        }

        WidthMillimeters = widthMillimeters;
        HeightMillimeters = heightMillimeters;
        ThicknessMillimeters = thicknessMillimeters;
    }
}
