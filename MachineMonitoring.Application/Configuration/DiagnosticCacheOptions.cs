using System.ComponentModel.DataAnnotations;

namespace MachineMonitoring.Application.Configuration;

public class DiagnosticCacheOptions
{
    public const string SectionName = "DiagnosticCache";

    public bool Enabled { get; init; } = true;

    [Range(1, 3_600)]
    public int DurationSeconds { get; init; } = 5;
}
