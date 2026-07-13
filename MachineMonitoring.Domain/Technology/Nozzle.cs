namespace MachineMonitoring.Domain.Technology;

public sealed class Nozzle
{
    public Guid Id { get; }

    public string Code { get; }

    public NozzleType Type { get; }

    public decimal DiameterMillimeters { get; }

    public decimal MaximumPressureBar { get; }

    public bool IsAvailable { get; private set; }

    public decimal WearPercentage { get; private set; }

    public Nozzle(
        Guid id,
        string code,
        NozzleType type,
        decimal diameterMillimeters,
        decimal maximumPressureBar
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The nozzle ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (diameterMillimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diameterMillimeters),
                "The nozzle diameter must be greater than zero."
            );
        }

        if (maximumPressureBar <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPressureBar),
                "The maximum pressure must be greater than zero."
            );
        }

        Id = id;
        Code = code;
        Type = type;
        DiameterMillimeters = diameterMillimeters;
        MaximumPressureBar = maximumPressureBar;

        IsAvailable = true;
        WearPercentage = 0;
    }

    public void SetWearPercentage(decimal wearPercentage)
    {
        if (wearPercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wearPercentage),
                "Wear percentage must be between 0 and 100."
            );
        }

        WearPercentage = wearPercentage;

        if (WearPercentage >= 100)
        {
            IsAvailable = false;
        }
    }

    public void MarkUnavailable()
    {
        IsAvailable = false;
    }

    public void MarkAvailable()
    {
        if (WearPercentage >= 100)
        {
            throw new InvalidOperationException(
                $"Nozzle {Code} cannot be marked available because it is completely worn."
            );
        }

        IsAvailable = true;
    }
}
