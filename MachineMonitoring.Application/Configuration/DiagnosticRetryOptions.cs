using System.ComponentModel.DataAnnotations;

namespace MachineMonitoring.Application.Configuration;

public class DiagnosticRetryOptions
{
    public const string SectionName = "DiagnosticRetry";

    [Range(0, 10)]
    public int MaxRetryAttempts { get; init; } = 2;

    [Range(0, 60_000)]
    public int DelayMilliseconds { get; init; } = 500;
}
