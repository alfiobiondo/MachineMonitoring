namespace MachineMonitoring.Domain.Technology;

public sealed class LaserCutConfiguration
{
    public Guid Id { get; }

    public Guid OperationId { get; }

    public Guid MaterialId { get; }

    public Guid NozzleId { get; }

    public Guid DrawingFileId { get; }

    public IWorkpieceGeometry Geometry { get; }

    public WorkpieceGeometryType GeometryType => Geometry.Type;

    public decimal LaserPowerWatts { get; }

    public decimal CuttingSpeedMillimetersPerMinute { get; }

    public AssistGasType AssistGas { get; }

    public decimal GasPressureBar { get; }

    public decimal FocalOffsetMillimeters { get; }

    public int NumberOfPasses { get; }

    public DateTimeOffset CreatedAt { get; }

    public LaserCutConfiguration(
        Guid id,
        Guid operationId,
        Guid materialId,
        Guid nozzleId,
        Guid drawingFileId,
        IWorkpieceGeometry geometry,
        decimal laserPowerWatts,
        decimal cuttingSpeedMillimetersPerMinute,
        AssistGasType assistGas,
        decimal gasPressureBar,
        decimal focalOffsetMillimeters,
        int numberOfPasses,
        DateTimeOffset createdAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The laser configuration ID cannot be empty.", nameof(id));
        }

        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("The operation ID cannot be empty.", nameof(operationId));
        }

        if (materialId == Guid.Empty)
        {
            throw new ArgumentException("The material ID cannot be empty.", nameof(materialId));
        }

        if (nozzleId == Guid.Empty)
        {
            throw new ArgumentException("The nozzle ID cannot be empty.", nameof(nozzleId));
        }

        if (drawingFileId == Guid.Empty)
        {
            throw new ArgumentException(
                "The drawing file ID cannot be empty.",
                nameof(drawingFileId)
            );
        }

        ArgumentNullException.ThrowIfNull(geometry);

        if (laserPowerWatts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laserPowerWatts),
                "Laser power must be greater than zero."
            );
        }

        if (cuttingSpeedMillimetersPerMinute <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cuttingSpeedMillimetersPerMinute),
                "Cutting speed must be greater than zero."
            );
        }

        if (gasPressureBar <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gasPressureBar),
                "Gas pressure must be greater than zero."
            );
        }

        if (numberOfPasses <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numberOfPasses),
                "The number of passes must be greater than zero."
            );
        }

        Id = id;
        OperationId = operationId;
        MaterialId = materialId;
        NozzleId = nozzleId;
        DrawingFileId = drawingFileId;
        Geometry = geometry;
        LaserPowerWatts = laserPowerWatts;
        CuttingSpeedMillimetersPerMinute = cuttingSpeedMillimetersPerMinute;

        AssistGas = assistGas;
        GasPressureBar = gasPressureBar;
        FocalOffsetMillimeters = focalOffsetMillimeters;

        NumberOfPasses = numberOfPasses;
        CreatedAt = createdAt;
    }
}
