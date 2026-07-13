namespace MachineMonitoring.Domain.Technology;

public interface IWorkpieceGeometry
{
    WorkpieceGeometryType Type { get; }

    decimal ThicknessMillimeters { get; }
}
