namespace MachineMonitoring.Api.Catalogs;

public sealed record NozzleResponse(
    Guid Id,
    string Code,
    string Type,
    decimal DiameterMillimeters,
    decimal MaximumPressureBar,
    decimal WearPercentage,
    bool IsAvailable
);
