using System.ComponentModel.DataAnnotations;

namespace MachineMonitoring.Application.Configuration;

public sealed class OperationSimulatorOptions
{
    public const string SectionName = "OperationSimulator";

    [Range(1, 3600)]
    public int PollingIntervalSeconds { get; init; } = 5;

    [Range(1, 3600)]
    public int ProgressIntervalSeconds { get; init; } = 2;

    [Range(1, 99)]
    public int ProgressIncrement { get; init; } = 20;

    [Required]
    public string InitialPhase { get; init; } = "Preparing laser";
}
