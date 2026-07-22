using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application.Configuration;

public sealed class MachineIncidentSimulatorOptions
{
    public const string SectionName = "MachineIncidentSimulator";

    public bool Enabled { get; init; }

    public int PollingIntervalSeconds { get; init; } = 5;

    public int WarningProbabilityPercentage { get; init; }

    public int BlockingAlarmProbabilityPercentage { get; init; }

    public int MinimumSecondsBetweenIncidents { get; init; } = 30;
}

public sealed class MachineIncidentSimulatorOptionsValidator
    : IValidateOptions<MachineIncidentSimulatorOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        MachineIncidentSimulatorOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.PollingIntervalSeconds <= 0)
        {
            failures.Add($"{nameof(options.PollingIntervalSeconds)} must be greater than zero.");
        }

        ValidatePercentage(
            options.WarningProbabilityPercentage,
            nameof(options.WarningProbabilityPercentage),
            failures
        );
        ValidatePercentage(
            options.BlockingAlarmProbabilityPercentage,
            nameof(options.BlockingAlarmProbabilityPercentage),
            failures
        );

        if (
            options.WarningProbabilityPercentage + options.BlockingAlarmProbabilityPercentage
            > 100
        )
        {
            failures.Add(
                $"{nameof(options.WarningProbabilityPercentage)} and {nameof(options.BlockingAlarmProbabilityPercentage)} cannot exceed 100 when summed."
            );
        }

        if (options.MinimumSecondsBetweenIncidents < 0)
        {
            failures.Add($"{nameof(options.MinimumSecondsBetweenIncidents)} cannot be negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidatePercentage(
        int value,
        string propertyName,
        List<string> failures
    )
    {
        if (value is < 0 or > 100)
        {
            failures.Add($"{propertyName} must be between 0 and 100.");
        }
    }
}
