using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class LaserCutConfigurationRecord
{
    public Guid Id { get; set; }

    public Guid OperationId { get; set; }

    public Guid MaterialId { get; set; }

    public Guid NozzleId { get; set; }

    public Guid DrawingFileId { get; set; }

    public WorkpieceGeometryType GeometryType { get; set; }

    public decimal ThicknessMillimeters { get; set; }

    public decimal? TubeOuterDiameterMillimeters { get; set; }

    public decimal? TubeLengthMillimeters { get; set; }

    public decimal? SheetWidthMillimeters { get; set; }

    public decimal? SheetHeightMillimeters { get; set; }

    public decimal LaserPowerWatts { get; set; }

    public decimal CuttingSpeedMillimetersPerMinute { get; set; }

    public AssistGasType AssistGas { get; set; }

    public decimal GasPressureBar { get; set; }

    public decimal FocalOffsetMillimeters { get; set; }

    public int NumberOfPasses { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public MachineOperationRecord Operation { get; set; } = null!;
}
