namespace MachineMonitoring.Domain.Technology;

public sealed class MachineCapabilities
{
    private readonly HashSet<MaterialCategory> _supportedMaterialCategories;

    private readonly HashSet<Guid> _supportedNozzleIds;

    private readonly HashSet<WorkpieceGeometryType> _supportedGeometryTypes;

    public string MachineId { get; }

    public decimal MaximumLaserPowerWatts { get; }

    public decimal MinimumThicknessMillimeters { get; }

    public decimal MaximumThicknessMillimeters { get; }

    public decimal? MaximumTubeDiameterMillimeters { get; }

    public decimal? MaximumTubeLengthMillimeters { get; }

    public decimal? MaximumSheetWidthMillimeters { get; }

    public decimal? MaximumSheetHeightMillimeters { get; }

    public IReadOnlySet<MaterialCategory> SupportedMaterialCategories =>
        _supportedMaterialCategories;

    public IReadOnlySet<Guid> SupportedNozzleIds => _supportedNozzleIds;

    public IReadOnlySet<WorkpieceGeometryType> SupportedGeometryTypes => _supportedGeometryTypes;

    public MachineCapabilities(
        string machineId,
        decimal maximumLaserPowerWatts,
        decimal minimumThicknessMillimeters,
        decimal maximumThicknessMillimeters,
        IEnumerable<MaterialCategory> supportedMaterialCategories,
        IEnumerable<Guid> supportedNozzleIds,
        IEnumerable<WorkpieceGeometryType> supportedGeometryTypes,
        decimal? maximumTubeDiameterMillimeters = null,
        decimal? maximumTubeLengthMillimeters = null,
        decimal? maximumSheetWidthMillimeters = null,
        decimal? maximumSheetHeightMillimeters = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        ArgumentNullException.ThrowIfNull(supportedMaterialCategories);

        ArgumentNullException.ThrowIfNull(supportedNozzleIds);

        ArgumentNullException.ThrowIfNull(supportedGeometryTypes);

        if (maximumLaserPowerWatts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLaserPowerWatts));
        }

        if (minimumThicknessMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumThicknessMillimeters));
        }

        if (maximumThicknessMillimeters < minimumThicknessMillimeters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumThicknessMillimeters));
        }

        _supportedMaterialCategories = supportedMaterialCategories.ToHashSet();

        _supportedNozzleIds = supportedNozzleIds.ToHashSet();

        _supportedGeometryTypes = supportedGeometryTypes.ToHashSet();

        if (_supportedMaterialCategories.Count == 0)
        {
            throw new ArgumentException(
                "At least one supported material category is required.",
                nameof(supportedMaterialCategories)
            );
        }

        if (_supportedNozzleIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one supported nozzle is required.",
                nameof(supportedNozzleIds)
            );
        }

        if (_supportedGeometryTypes.Count == 0)
        {
            throw new ArgumentException(
                "At least one supported geometry type is required.",
                nameof(supportedGeometryTypes)
            );
        }

        if (_supportedNozzleIds.Contains(Guid.Empty))
        {
            throw new ArgumentException(
                "Supported nozzle IDs cannot contain an empty ID.",
                nameof(supportedNozzleIds)
            );
        }

        ValidateTubeCapabilities(maximumTubeDiameterMillimeters, maximumTubeLengthMillimeters);

        ValidateSheetCapabilities(maximumSheetWidthMillimeters, maximumSheetHeightMillimeters);

        MachineId = machineId;
        MaximumLaserPowerWatts = maximumLaserPowerWatts;

        MinimumThicknessMillimeters = minimumThicknessMillimeters;

        MaximumThicknessMillimeters = maximumThicknessMillimeters;

        MaximumTubeDiameterMillimeters = maximumTubeDiameterMillimeters;

        MaximumTubeLengthMillimeters = maximumTubeLengthMillimeters;

        MaximumSheetWidthMillimeters = maximumSheetWidthMillimeters;

        MaximumSheetHeightMillimeters = maximumSheetHeightMillimeters;
    }

    public bool SupportsMaterial(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);

        return material.IsEnabled && _supportedMaterialCategories.Contains(material.Category);
    }

    public bool SupportsNozzle(Nozzle nozzle)
    {
        ArgumentNullException.ThrowIfNull(nozzle);

        return nozzle.IsAvailable && _supportedNozzleIds.Contains(nozzle.Id);
    }

    public bool SupportsGeometry(IWorkpieceGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (!_supportedGeometryTypes.Contains(geometry.Type))
        {
            return false;
        }

        return geometry switch
        {
            TubeGeometry tube => SupportsTubeGeometry(tube),

            SheetGeometry sheet => SupportsSheetGeometry(sheet),

            _ => false,
        };
    }

    public bool SupportsThickness(decimal thicknessMillimeters)
    {
        return thicknessMillimeters >= MinimumThicknessMillimeters
            && thicknessMillimeters <= MaximumThicknessMillimeters;
    }

    public bool SupportsLaserPower(decimal laserPowerWatts)
    {
        return laserPowerWatts > 0 && laserPowerWatts <= MaximumLaserPowerWatts;
    }

    private bool SupportsTubeGeometry(TubeGeometry geometry)
    {
        return MaximumTubeDiameterMillimeters is not null
            && MaximumTubeLengthMillimeters is not null
            && geometry.OuterDiameterMillimeters <= MaximumTubeDiameterMillimeters
            && geometry.LengthMillimeters <= MaximumTubeLengthMillimeters;
    }

    private bool SupportsSheetGeometry(SheetGeometry geometry)
    {
        return MaximumSheetWidthMillimeters is not null
            && MaximumSheetHeightMillimeters is not null
            && geometry.WidthMillimeters <= MaximumSheetWidthMillimeters
            && geometry.HeightMillimeters <= MaximumSheetHeightMillimeters;
    }

    private void ValidateTubeCapabilities(decimal? maximumDiameter, decimal? maximumLength)
    {
        bool supportsTube = _supportedGeometryTypes.Contains(WorkpieceGeometryType.Tube);

        if (supportsTube && (maximumDiameter is null or <= 0 || maximumLength is null or <= 0))
        {
            throw new ArgumentException(
                "Tube limits are required when the machine supports tubes."
            );
        }
    }

    private void ValidateSheetCapabilities(decimal? maximumWidth, decimal? maximumHeight)
    {
        bool supportsSheet = _supportedGeometryTypes.Contains(WorkpieceGeometryType.Sheet);

        if (supportsSheet && (maximumWidth is null or <= 0 || maximumHeight is null or <= 0))
        {
            throw new ArgumentException(
                "Sheet limits are required when the machine supports sheets."
            );
        }
    }
}
