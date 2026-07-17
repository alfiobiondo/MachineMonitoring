using System.ComponentModel.DataAnnotations;

namespace MachineMonitoring.Application.Configuration;

public sealed class OperationSimulatorOptions
{
    public const string SectionName = "OperationSimulator";

    [Range(1, 3600)]
    public int PollingIntervalSeconds { get; init; } = 5;

    [Range(1, 3600)]
    public int ProgressIntervalSeconds { get; init; } = 2;

    [Range(1, 100)]
    public int MinimumProgressIncrement { get; init; } = 10;

    [Range(1, 100)]
    public int MaximumProgressIncrement { get; init; } = 25;

    [Required]
    public string InitialPhase { get; init; } = "Preparing laser";
}
