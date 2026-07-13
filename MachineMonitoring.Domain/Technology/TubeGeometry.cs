namespace MachineMonitoring.Domain.Technology;

public sealed class TubeGeometry : IWorkpieceGeometry
{
    public WorkpieceGeometryType Type => WorkpieceGeometryType.Tube;

    public decimal OuterDiameterMillimeters { get; }

    public decimal ThicknessMillimeters { get; }

    public decimal LengthMillimeters { get; }

    public decimal InnerDiameterMillimeters => OuterDiameterMillimeters - ThicknessMillimeters * 2;

    public TubeGeometry(
        decimal outerDiameterMillimeters,
        decimal thicknessMillimeters,
        decimal lengthMillimeters
    )
    {
        if (outerDiameterMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outerDiameterMillimeters),
                "The tube outer diameter must be greater than zero."
            );
        }

        if (thicknessMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thicknessMillimeters),
                "The tube thickness must be greater than zero."
            );
        }

        if (lengthMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthMillimeters),
                "The tube length must be greater than zero."
            );
        }

        if (thicknessMillimeters * 2 >= outerDiameterMillimeters)
        {
            throw new ArgumentException(
                "The tube thickness must be less than half of the outer diameter.",
                nameof(thicknessMillimeters)
            );
        }

        OuterDiameterMillimeters = outerDiameterMillimeters;

        ThicknessMillimeters = thicknessMillimeters;

        LengthMillimeters = lengthMillimeters;
    }
}
